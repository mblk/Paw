using System.Diagnostics;

namespace Paw.Core.Utils.DataStructures;

/// <summary>
/// Densely packed generational pool.
/// </summary>
/// <typeparam name="T"></typeparam>
public class PackedGenPool<T>
    where T : unmanaged
{
    private struct Slot
    {
        public bool Used;
        public uint Gen;
        public int DenseIndex;

        public override readonly string ToString()
        {
            return $"{(Used ? "used" : "free")} Gen={Gen} DenseIndex={DenseIndex}";
        }
    }

    private readonly int _poolId = PoolIdManager.GetNextId();

    /// <summary>
    /// Sparse slots
    /// </summary>
    private Slot[] _slots;

    /// <summary>
    /// Dense data
    /// </summary>
    private T[] _dense;

    /// <summary>
    /// Dense index to slot index mapping
    /// </summary>
    private int[] _denseToSlot;

    /// <summary>
    /// Stack of free slot indices
    /// </summary>
    private int[] _freeSlots;

    private int _count;
    private int _freeCount;

    public int PoolId => _poolId;
    public int Count => _count;

    public PackedGenPool(int initialSize = 128)
    {
        if (initialSize < 1)
            throw new ArgumentOutOfRangeException(nameof(initialSize), "Must be 1 or greater");

        _slots = new Slot[initialSize];
        _dense = new T[initialSize];
        _denseToSlot = new int[initialSize];
        _freeSlots = new int[initialSize];

        Clear();
        Debug.Assert(_count == 0);
        Debug.Assert(_freeCount == initialSize);
    }

    /// <summary>
    /// Important: Only safe until the next call to Alloc/Free/Clear!
    /// </summary>
    public ReadOnlySpan<T> AsReadOnlySpan()
    {
        return _dense.AsSpan(0, _count);
    }

    /// <summary>
    /// Important: Only safe until the next call to Alloc/Free/Clear!
    /// </summary>
    public Span<T> AsSpan()
    {
        return _dense.AsSpan(0, _count);
    }

    public GenIndex Alloc(T data)
    {
        if (_freeCount < 1)
        {
            Grow();
            Debug.Assert(_freeCount >= 1);
        }

        int denseIndex = _count++;
        int slotIndex = _freeSlots[--_freeCount];

        ref Slot slot = ref _slots[slotIndex];
        Debug.Assert(slot.Used == false);
        Debug.Assert(slot.Gen < uint.MaxValue);
        Debug.Assert(slot.DenseIndex == -1);

        slot.Used = true;
        slot.Gen++;
        slot.DenseIndex = denseIndex;

        _dense[denseIndex] = data;
        _denseToSlot[denseIndex] = slotIndex;

        return new GenIndex(_poolId, slotIndex, slot.Gen);
    }

    public void Free(GenIndex index)
    {
        if (!IsValid(index))
            throw new ArgumentException($"Invalid index: {index}");

        Debug.Assert(_count > 0);

        int slotIndex = index.Index;
        Debug.Assert(0 <= slotIndex && slotIndex < _slots.Length);
        ref Slot slot = ref _slots[slotIndex];

        int denseIndex = slot.DenseIndex;
        Debug.Assert(0 <= denseIndex && denseIndex < _dense.Length);
        Debug.Assert(_denseToSlot[denseIndex] == slotIndex);

        int lastDenseIndex = _count - 1;
        Debug.Assert(0 <= lastDenseIndex && lastDenseIndex < _dense.Length);

        if (denseIndex != lastDenseIndex)
        {
            int remappedSlotIndex = _denseToSlot[lastDenseIndex];
            Debug.Assert(0 <= remappedSlotIndex && remappedSlotIndex < _slots.Length);

            // move data
            _dense[denseIndex] = _dense[lastDenseIndex];

            // remap slot
            ref Slot remappedSlot = ref _slots[remappedSlotIndex];
            Debug.Assert(remappedSlot.Used == true);
            Debug.Assert(remappedSlot.DenseIndex == lastDenseIndex);
            remappedSlot.DenseIndex = denseIndex;
            _denseToSlot[denseIndex] = remappedSlotIndex;
        }

        // clear last data entry
        _dense[lastDenseIndex] = default;
        _denseToSlot[lastDenseIndex] = -1;
        _count--;

        // free slot
        slot.Used = false;
        slot.DenseIndex = -1;
        if (slot.Gen < uint.MaxValue)
        {
            _freeSlots[_freeCount++] = slotIndex;
        }
    }

    public bool IsValid(GenIndex index)
    {
        if (index.PoolId != _poolId)
            return false;

        if (index.Gen == 0)
            return false;

        if (index.Index < 0 || index.Index >= _slots.Length)
            return false;

        ref Slot slot = ref _slots[index.Index];

        if (!slot.Used)
            return false;

        if (slot.Gen != index.Gen)
            return false;

        return true;
    }

    public T Get(GenIndex index)
    {
        return _dense[GetDenseIndex(index)];
    }

    /// <summary>
    /// Important: Only safe until the next call to Alloc/Free/Clear!
    /// </summary>
    public ref T GetRef(GenIndex index)
    {
        return ref _dense[GetDenseIndex(index)];
    }

    public void Set(GenIndex index, T data)
    {
        _dense[GetDenseIndex(index)] = data;
    }

    public void Clear()
    {
        int size = _slots.Length;

        _count = 0;
        _freeCount = 0;

        for (int i = size - 1; i >= 0; i--)
        {
            // Note: Not changing _slots[i].Gen as it would revive retired slots.
            ref Slot slot = ref _slots[i];
            slot.Used = false;
            slot.DenseIndex = -1;               // invalid

            _dense[i] = default;

            _denseToSlot[i] = -1;               // invalid

            if (slot.Gen < uint.MaxValue)
            {
                _freeSlots[_freeCount++] = i;   // (N-1) ... 0
            }
        }
    }

    private int GetDenseIndex(GenIndex index)
    {
        if (!IsValid(index))
            throw new ArgumentException($"Invalid index: {index}");

        return _slots[index.Index].DenseIndex;
    }

    private void Grow()
    {
        int oldSize = _slots.Length;
        int newSize = GetExpandedSize(oldSize);

#if DEBUG
        Console.WriteLine($"PackedGenPool growing {oldSize} -> {newSize}");
#endif

        Array.Resize(ref _slots, newSize);
        Array.Resize(ref _dense, newSize);
        Array.Resize(ref _denseToSlot, newSize);
        Array.Resize(ref _freeSlots, newSize);

        for (int i = newSize - 1; i >= oldSize; i--)
        {
            _slots[i].DenseIndex = -1;      // invalid
            _denseToSlot[i] = -1;           // invalid
            _freeSlots[_freeCount++] = i;
        }
    }

    private static int GetExpandedSize(int currentSize)
    {
        if (currentSize >= Array.MaxLength)
            throw new InvalidOperationException("PackedGenPool cannot grow any further.");

        if (currentSize > Array.MaxLength / 2)
            return Array.MaxLength;

        return currentSize * 2;
    }
}
