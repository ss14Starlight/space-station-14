using System.Numerics;
using System.Runtime.CompilerServices;

namespace Content.Client._Starlight.Collections;

/// <summary>
/// Fixed-capacity ring buffer for <see cref="Vector2"/> values.
/// O(1) push/pop from both ends, no allocations during operation.
/// </summary>
public sealed class RingBuffer<T>(int capacity) where T : struct
{
    private T[] _buf = new T[capacity];
    private int _head;

    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get; private set;
    }

    public int Capacity => _buf.Length;

    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _buf[(_head + index) % _buf.Length];
    }

    public void PushBack(T value)
    {
        var idx = (_head + Count) % _buf.Length;
        _buf[idx] = value;

        if (Count < _buf.Length)
            Count++;
        else
            _head = (_head + 1) % _buf.Length;
    }

    public void PopFront()
    {
        if (Count == 0)
            return;

        _head = (_head + 1) % _buf.Length;
        Count--;
    }

    public void Clear()
    {
        _head = 0;
        Count = 0;
    }

    public void Resize(int newCapacity)
    {
        if (newCapacity == _buf.Length)
            return;

        var newBuf = new T[newCapacity];
        var toCopy = Math.Min(Count, newCapacity);
        for (var i = 0; i < toCopy; i++)
            newBuf[i] = this[i];

        _buf = newBuf;
        _head = 0;
        Count = toCopy;
    }
}
