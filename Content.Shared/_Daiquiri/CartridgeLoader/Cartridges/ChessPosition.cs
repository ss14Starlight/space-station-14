using System;
using System.Collections.Generic;

namespace Content.Shared._Daiquiri.CartridgeLoader.Cartridges;

/// <summary>
/// One move in UCI terms. Squares are board indices with 0 = a8 and 63 = h1, matching
/// <see cref="ChessGameState.Board"/>.
/// </summary>
public readonly record struct ChessMove(int From, int To, char Promotion = '\0')
{
    public string ToUci()
    {
        var uci = SquareName(From) + SquareName(To);
        if (Promotion == '\0')
            return uci;

        // Appending a char with + makes Roslyn emit a ReadOnlySpan<char> concat, which the
        // content sandbox forbids. Insert() keeps the generated IL free of span constructors.
        return uci.Insert(uci.Length, Promotion.ToString());
    }

    public static string SquareName(int index)
    {
        var file = ((char) ('a' + index % 8)).ToString();
        var rank = ((char) ('8' - index / 8)).ToString();
        return file + rank;
    }
}

/// <summary>
/// Minimal standard-chess rules engine used by chess.nt. The server rebuilds the position
/// from the initial FEN plus Lichess' UCI move list, and the client uses the same engine to
/// highlight the squares a selected piece may move to.
/// </summary>
public sealed class ChessPosition
{
    public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    private readonly char[] _board = new char[64];

    public ChessPosition()
    {
        // FEN parsing only writes the pieces. Empty squares must be dots, not the
        // default '\0', otherwise pawns see every empty square as an enemy piece and
        // generate diagonal captures instead of forward moves.
        Array.Fill(_board, '.');
    }

    public bool WhiteToMove = true;
    public bool WhiteKingSide;
    public bool WhiteQueenSide;
    public bool BlackKingSide;
    public bool BlackQueenSide;

    /// <summary>Target square of a possible en passant capture, or -1.</summary>
    public int EnPassant = -1;

    public string BoardString => new(_board);

    public char PieceAt(int index)
    {
        return index is >= 0 and < 64 ? _board[index] : '.';
    }

    public ChessPosition Clone()
    {
        var copy = new ChessPosition
        {
            WhiteToMove = WhiteToMove,
            WhiteKingSide = WhiteKingSide,
            WhiteQueenSide = WhiteQueenSide,
            BlackKingSide = BlackKingSide,
            BlackQueenSide = BlackQueenSide,
            EnPassant = EnPassant,
        };

        Array.Copy(_board, copy._board, 64);
        return copy;
    }

    public static ChessPosition FromFen(string? fen)
    {
        if (string.IsNullOrWhiteSpace(fen) ||
            string.Equals(fen.Trim(), "startpos", StringComparison.OrdinalIgnoreCase))
        {
            fen = StartFen;
        }

        var position = new ChessPosition();
        var parts = fen.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var index = 0;
        foreach (var symbol in parts[0])
        {
            if (symbol == '/')
                continue;

            if (char.IsDigit(symbol))
            {
                index += symbol - '0';
                continue;
            }

            if (index is >= 0 and < 64)
                position._board[index] = symbol;

            index++;
        }

        if (parts.Length > 1)
            position.WhiteToMove = !parts[1].StartsWith("b", StringComparison.OrdinalIgnoreCase);

        if (parts.Length > 2)
        {
            position.WhiteKingSide = parts[2].Contains('K');
            position.WhiteQueenSide = parts[2].Contains('Q');
            position.BlackKingSide = parts[2].Contains('k');
            position.BlackQueenSide = parts[2].Contains('q');
        }

        if (parts.Length > 3)
            position.EnPassant = ParseSquare(parts[3]);

        return position;
    }

    /// <summary>
    /// Builds a position from the public state the server sends to the PDA.
    /// </summary>
    public static ChessPosition FromState(string? board, string? turn, string? castling, string? enPassant)
    {
        var position = new ChessPosition();
        if (!string.IsNullOrEmpty(board))
        {
            for (var i = 0; i < 64 && i < board.Length; i++)
                position._board[i] = board[i];
        }
        else
        {
            Array.Fill(position._board, '.');
        }

        position.WhiteToMove = !string.Equals(turn, "black", StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(castling))
        {
            position.WhiteKingSide = castling.Contains('K');
            position.WhiteQueenSide = castling.Contains('Q');
            position.BlackKingSide = castling.Contains('k');
            position.BlackQueenSide = castling.Contains('q');
        }

        position.EnPassant = ParseSquare(enPassant);
        return position;
    }

    public string CastlingString()
    {
        var result = string.Empty;
        if (WhiteKingSide)
            result += "K";
        if (WhiteQueenSide)
            result += "Q";
        if (BlackKingSide)
            result += "k";
        if (BlackQueenSide)
            result += "q";
        return result;
    }

    public string EnPassantSquare()
    {
        return EnPassant < 0 ? string.Empty : ChessMove.SquareName(EnPassant);
    }

    /// <summary>Legal moves for the side that is to move.</summary>
    public List<ChessMove> GetLegalMoves()
    {
        return GetLegalMoves(WhiteToMove);
    }

    public List<ChessMove> GetLegalMoves(bool white)
    {
        var pseudo = new List<ChessMove>(64);
        GeneratePseudoLegal(white, pseudo);

        var legal = new List<ChessMove>(pseudo.Count);
        foreach (var move in pseudo)
        {
            var copy = Clone();
            copy.Apply(move);
            if (!copy.IsKingAttacked(white))
                legal.Add(move);
        }

        return legal;
    }

    public List<ChessMove> GetLegalMovesFrom(int from)
    {
        var result = new List<ChessMove>();
        foreach (var move in GetLegalMoves())
        {
            if (move.From == from)
                result.Add(move);
        }

        return result;
    }

    /// <summary>
    /// Applies a move that is already known to be legal (for example from Lichess).
    /// </summary>
    public bool TryApplyUci(string uci)
    {
        if (uci.Length is < 4 or > 5)
            return false;

        var from = ParseSquare(uci[..2]);
        var to = ParseSquare(uci.Substring(2, 2));
        if (from < 0 || to < 0)
            return false;

        var promotion = uci.Length == 5 ? char.ToLowerInvariant(uci[4]) : '\0';
        Apply(new ChessMove(from, to, promotion));
        return true;
    }

    public void Apply(ChessMove move)
    {
        var piece = _board[move.From];
        if (piece == '.')
            return;

        var lower = char.ToLowerInvariant(piece);
        var white = char.IsUpper(piece);

        // En passant: a pawn moves diagonally onto an empty square.
        if (lower == 'p' && move.From % 8 != move.To % 8 && _board[move.To] == '.')
            _board[move.To + (white ? 8 : -8)] = '.';

        // Castling: the king moves two files and the rook jumps over it.
        if (lower == 'k' && Math.Abs(move.To % 8 - move.From % 8) == 2)
        {
            if (move.To > move.From)
            {
                _board[move.From + 1] = _board[move.From + 3];
                _board[move.From + 3] = '.';
            }
            else
            {
                _board[move.From - 1] = _board[move.From - 4];
                _board[move.From - 4] = '.';
            }
        }

        _board[move.From] = '.';
        _board[move.To] = move.Promotion == '\0'
            ? piece
            : white
                ? char.ToUpperInvariant(move.Promotion)
                : char.ToLowerInvariant(move.Promotion);

        if (lower == 'k')
        {
            if (white)
            {
                WhiteKingSide = false;
                WhiteQueenSide = false;
            }
            else
            {
                BlackKingSide = false;
                BlackQueenSide = false;
            }
        }
        else if (lower == 'r')
        {
            switch (move.From)
            {
                case 63:
                    WhiteKingSide = false;
                    break;
                case 56:
                    WhiteQueenSide = false;
                    break;
                case 7:
                    BlackKingSide = false;
                    break;
                case 0:
                    BlackQueenSide = false;
                    break;
            }
        }

        // Capturing a rook on its original square also removes the right.
        switch (move.To)
        {
            case 63:
                WhiteKingSide = false;
                break;
            case 56:
                WhiteQueenSide = false;
                break;
            case 7:
                BlackKingSide = false;
                break;
            case 0:
                BlackQueenSide = false;
                break;
        }

        EnPassant = -1;
        if (lower == 'p' && Math.Abs(move.To - move.From) == 16)
            EnPassant = (move.From + move.To) / 2;

        WhiteToMove = !WhiteToMove;
    }

    public bool IsKingAttacked(bool white)
    {
        var king = FindKing(white);
        return king >= 0 && IsAttacked(king, !white);
    }

    public int FindKing(bool white)
    {
        var target = white ? 'K' : 'k';
        for (var i = 0; i < 64; i++)
        {
            if (_board[i] == target)
                return i;
        }

        return -1;
    }

    public bool IsAttacked(int square, bool byWhite)
    {
        if (square is < 0 or > 63)
            return false;

        var file = square % 8;
        var rank = square / 8;

        // Pawns.
        var pawnRank = rank + (byWhite ? 1 : -1);
        if (pawnRank is >= 0 and < 8)
        {
            for (var df = -1; df <= 1; df += 2)
            {
                var f = file + df;
                if (f is < 0 or > 7)
                    continue;

                var piece = _board[pawnRank * 8 + f];
                if (char.ToLowerInvariant(piece) == 'p' && char.IsUpper(piece) == byWhite)
                    return true;
            }
        }

        // Knights.
        foreach (var (df, dr) in KnightOffsets)
        {
            var f = file + df;
            var r = rank + dr;
            if (f is < 0 or > 7 || r is < 0 or > 7)
                continue;

            var piece = _board[r * 8 + f];
            if (char.ToLowerInvariant(piece) == 'n' && char.IsUpper(piece) == byWhite)
                return true;
        }

        // King.
        foreach (var (df, dr) in KingOffsets)
        {
            var f = file + df;
            var r = rank + dr;
            if (f is < 0 or > 7 || r is < 0 or > 7)
                continue;

            var piece = _board[r * 8 + f];
            if (char.ToLowerInvariant(piece) == 'k' && char.IsUpper(piece) == byWhite)
                return true;
        }

        // Sliding pieces.
        return RayAttacked(square, byWhite, 1, 0, 'r') ||
               RayAttacked(square, byWhite, -1, 0, 'r') ||
               RayAttacked(square, byWhite, 0, 1, 'r') ||
               RayAttacked(square, byWhite, 0, -1, 'r') ||
               RayAttacked(square, byWhite, 1, 1, 'b') ||
               RayAttacked(square, byWhite, 1, -1, 'b') ||
               RayAttacked(square, byWhite, -1, 1, 'b') ||
               RayAttacked(square, byWhite, -1, -1, 'b');
    }

    private bool RayAttacked(int square, bool byWhite, int df, int dr, char slider)
    {
        var file = square % 8 + df;
        var rank = square / 8 + dr;

        while (file is >= 0 and < 8 && rank is >= 0 and < 8)
        {
            var piece = _board[rank * 8 + file];
            if (piece != '.')
            {
                if (char.IsUpper(piece) != byWhite)
                    return false;

                var lower = char.ToLowerInvariant(piece);
                return lower == slider || lower == 'q';
            }

            file += df;
            rank += dr;
        }

        return false;
    }

    private void GeneratePseudoLegal(bool white, List<ChessMove> moves)
    {
        for (var index = 0; index < 64; index++)
        {
            var piece = _board[index];
            if (piece == '.' || char.IsUpper(piece) != white)
                continue;

            var file = index % 8;
            var rank = index / 8;

            switch (char.ToLowerInvariant(piece))
            {
                case 'p':
                    GeneratePawnMoves(index, file, rank, white, moves);
                    break;
                case 'n':
                    GenerateStepMoves(index, file, rank, white, moves, KnightOffsets);
                    break;
                case 'k':
                    GenerateStepMoves(index, file, rank, white, moves, KingOffsets);
                    GenerateCastlingMoves(index, file, rank, white, moves);
                    break;
                case 'b':
                    GenerateSlideMoves(index, file, rank, white, moves, BishopDirections);
                    break;
                case 'r':
                    GenerateSlideMoves(index, file, rank, white, moves, RookDirections);
                    break;
                case 'q':
                    GenerateSlideMoves(index, file, rank, white, moves, BishopDirections);
                    GenerateSlideMoves(index, file, rank, white, moves, RookDirections);
                    break;
            }
        }
    }

    private void GeneratePawnMoves(int index, int file, int rank, bool white, List<ChessMove> moves)
    {
        var direction = white ? -1 : 1;
        var startRank = white ? 6 : 1;
        var promotionRank = white ? 0 : 7;

        var oneRank = rank + direction;
        if (oneRank is >= 0 and < 8)
        {
            var one = oneRank * 8 + file;
            if (_board[one] == '.')
            {
                AddPawnMove(index, one, promotionRank, moves);

                var twoRank = rank + direction * 2;
                if (rank == startRank && _board[twoRank * 8 + file] == '.')
                    moves.Add(new ChessMove(index, twoRank * 8 + file));
            }
        }

        for (var df = -1; df <= 1; df += 2)
        {
            var f = file + df;
            if (f is < 0 or > 7 || oneRank is < 0 or > 7)
                continue;

            var target = oneRank * 8 + f;
            var targetPiece = _board[target];
            if (targetPiece != '.' && char.IsUpper(targetPiece) != white)
                AddPawnMove(index, target, promotionRank, moves);
            else if (target == EnPassant && targetPiece == '.')
                moves.Add(new ChessMove(index, target));
        }
    }

    private static void AddPawnMove(int from, int to, int promotionRank, List<ChessMove> moves)
    {
        if (to / 8 != promotionRank)
        {
            moves.Add(new ChessMove(from, to));
            return;
        }

        moves.Add(new ChessMove(from, to, 'q'));
        moves.Add(new ChessMove(from, to, 'r'));
        moves.Add(new ChessMove(from, to, 'b'));
        moves.Add(new ChessMove(from, to, 'n'));
    }

    private void GenerateStepMoves(
        int index,
        int file,
        int rank,
        bool white,
        List<ChessMove> moves,
        ReadOnlySpan<(int File, int Rank)> offsets)
    {
        foreach (var (df, dr) in offsets)
        {
            var f = file + df;
            var r = rank + dr;
            if (f is < 0 or > 7 || r is < 0 or > 7)
                continue;

            var target = r * 8 + f;
            var piece = _board[target];
            if (piece == '.' || char.IsUpper(piece) != white)
                moves.Add(new ChessMove(index, target));
        }
    }

    private void GenerateSlideMoves(
        int index,
        int file,
        int rank,
        bool white,
        List<ChessMove> moves,
        ReadOnlySpan<(int File, int Rank)> directions)
    {
        foreach (var (df, dr) in directions)
        {
            var f = file + df;
            var r = rank + dr;

            while (f is >= 0 and < 8 && r is >= 0 and < 8)
            {
                var target = r * 8 + f;
                var piece = _board[target];

                if (piece == '.')
                {
                    moves.Add(new ChessMove(index, target));
                }
                else
                {
                    if (char.IsUpper(piece) != white)
                        moves.Add(new ChessMove(index, target));

                    break;
                }

                f += df;
                r += dr;
            }
        }
    }

    private void GenerateCastlingMoves(int index, int file, int rank, bool white, List<ChessMove> moves)
    {
        var kingSquare = white ? 60 : 4;
        if (index != kingSquare)
            return;

        var enemy = !white;
        var kingSide = white ? WhiteKingSide : BlackKingSide;
        var queenSide = white ? WhiteQueenSide : BlackQueenSide;
        var rook = white ? 'R' : 'r';

        if (kingSide &&
            _board[kingSquare + 1] == '.' &&
            _board[kingSquare + 2] == '.' &&
            _board[kingSquare + 3] == rook &&
            !IsAttacked(kingSquare, enemy) &&
            !IsAttacked(kingSquare + 1, enemy) &&
            !IsAttacked(kingSquare + 2, enemy))
        {
            moves.Add(new ChessMove(kingSquare, kingSquare + 2));
        }

        if (queenSide &&
            _board[kingSquare - 1] == '.' &&
            _board[kingSquare - 2] == '.' &&
            _board[kingSquare - 3] == '.' &&
            _board[kingSquare - 4] == rook &&
            !IsAttacked(kingSquare, enemy) &&
            !IsAttacked(kingSquare - 1, enemy) &&
            !IsAttacked(kingSquare - 2, enemy))
        {
            moves.Add(new ChessMove(kingSquare, kingSquare - 2));
        }
    }

    private static readonly (int File, int Rank)[] KnightOffsets =
    {
        (1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2),
    };

    private static readonly (int File, int Rank)[] KingOffsets =
    {
        (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1),
    };

    private static readonly (int File, int Rank)[] BishopDirections =
    {
        (1, 1), (1, -1), (-1, 1), (-1, -1),
    };

    private static readonly (int File, int Rank)[] RookDirections =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1),
    };

    public static int ParseSquare(string? square)
    {
        if (string.IsNullOrEmpty(square) || square.Length != 2)
            return -1;

        var file = square[0] - 'a';
        var rank = square[1] - '1';
        if (file is < 0 or > 7 || rank is < 0 or > 7)
            return -1;

        return (7 - rank) * 8 + file;
    }
}
