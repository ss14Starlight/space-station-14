using System;
using System.Collections.Generic;
using System.Diagnostics;
using Content.Shared._Daiquiri.CartridgeLoader.Cartridges;

namespace Content.Server._Daiquiri.CartridgeLoader.Cartridges;

/// <summary>
/// Small self-contained engine for the casual chess.nt bot page. It deliberately does not
/// use Lichess: AI games created by /api/challenge/ai are rejected by the Board API, so a
/// local search is the only way to keep the bot playable from the in-game board.
/// </summary>
internal static class ChessBotEngine
{
    private const int MateScore = 100_000;
    private const int Infinity = 1_000_000;

    /// <summary>Picks a move for the side that is to move. Null means the game is over.</summary>
    public static ChessMove? ChooseMove(ChessPosition position, int level)
    {
        var moves = position.GetLegalMoves();
        if (moves.Count == 0)
            return null;

        level = Math.Clamp(level, 1, 8);
        var random = new Random();

        // Level 1 is deliberately unpredictable. The other levels use a shallow search
        // with decreasing noise, which keeps the eight buttons meaningfully different
        // without making the server spend seconds on a single move.
        if (level == 1)
            return moves[random.Next(moves.Count)];

        var depth = level switch
        {
            2 or 3 => 1,
            4 or 5 => 2,
            6 or 7 => 3,
            _ => 4,
        };

        var noise = level switch
        {
            2 => 140,
            3 => 70,
            4 => 35,
            5 => 18,
            6 => 10,
            7 => 5,
            _ => 0,
        };

        var budgetMs = level >= 8 ? 900 : level >= 6 ? 600 : 350;
        var watch = Stopwatch.StartNew();
        OrderMoves(position, moves);

        var best = moves[0];
        var bestScore = int.MinValue;

        foreach (var move in moves)
        {
            if (watch.ElapsedMilliseconds > budgetMs)
                break;

            var copy = position.Clone();
            copy.Apply(move);

            var score = -Negamax(copy, depth - 1, -Infinity, Infinity, 1, watch, budgetMs);
            score += random.Next(-noise, noise + 1);

            if (score > bestScore)
            {
                bestScore = score;
                best = move;
            }
        }

        return best;
    }

    private static int Negamax(
        ChessPosition position,
        int depth,
        int alpha,
        int beta,
        int ply,
        Stopwatch watch,
        int budgetMs)
    {
        if (watch.ElapsedMilliseconds > budgetMs)
            return Evaluate(position);

        var moves = position.GetLegalMoves();
        if (moves.Count == 0)
        {
            return position.IsKingAttacked(position.WhiteToMove)
                ? -MateScore + ply
                : 0;
        }

        if (depth <= 0)
            return Evaluate(position);

        OrderMoves(position, moves);

        var best = -Infinity;
        foreach (var move in moves)
        {
            var copy = position.Clone();
            copy.Apply(move);

            var score = -Negamax(copy, depth - 1, -beta, -alpha, ply + 1, watch, budgetMs);
            if (score > best)
                best = score;
            if (best > alpha)
                alpha = best;
            if (alpha >= beta)
                break;
        }

        return best;
    }

    private static void OrderMoves(ChessPosition position, List<ChessMove> moves)
    {
        moves.Sort((left, right) => MoveOrderScore(position, right).CompareTo(MoveOrderScore(position, left)));
    }

    private static int MoveOrderScore(ChessPosition position, ChessMove move)
    {
        var score = 0;
        var target = position.PieceAt(move.To);
        if (target != '.')
            score += PieceValue(target) * 10 - PieceValue(position.PieceAt(move.From));

        if (move.Promotion != '\0')
            score += PieceValue(move.Promotion) * 10;

        return score;
    }

    private static int Evaluate(ChessPosition position)
    {
        var score = 0;

        for (var index = 0; index < 64; index++)
        {
            var piece = position.PieceAt(index);
            if (piece == '.')
                continue;

            var white = char.IsUpper(piece);
            var value = PieceValue(piece);
            var file = index % 8;
            var rank = index / 8;
            var center = 3 - Math.Abs(file - 3) + (3 - Math.Abs(rank - 3));

            switch (char.ToLowerInvariant(piece))
            {
                case 'p':
                    value += white ? (6 - rank) * 8 : (rank - 1) * 8;
                    break;
                case 'n':
                case 'b':
                    value += center * 4;
                    break;
                case 'r':
                    value += center * 2;
                    break;
                case 'q':
                    value += center;
                    break;
            }

            score += white ? value : -value;
        }

        return position.WhiteToMove ? score : -score;
    }

    private static int PieceValue(char piece)
    {
        return char.ToLowerInvariant(piece) switch
        {
            'p' => 100,
            'n' => 320,
            'b' => 330,
            'r' => 500,
            'q' => 900,
            'k' => 20_000,
            _ => 0,
        };
    }
}
