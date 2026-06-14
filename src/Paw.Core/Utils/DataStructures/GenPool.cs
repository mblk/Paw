namespace Paw.Core.Utils.DataStructures;

public class GenPool<T>
    where T : unmanaged
{
    private struct GenItem
    {
        public bool Used;
        public uint Gen;
        public T Data;
    }

    private readonly int _poolId = PoolIdManager.GetNextId();

    private GenItem[] _items;

    public int PoolId => _poolId;

    public GenPool(int initialSize = 128)
    {
        if (initialSize < 1)
            throw new ArgumentOutOfRangeException(nameof(initialSize), "Must be 1 or greater");

        _items = new GenItem[initialSize];
    }

    public bool IsValid(GenIndex @ref)
    {
        if (@ref.PoolId != _poolId)
            return false;

        if (@ref.Gen == 0)
            return false;

        if (@ref.Index < 0 || @ref.Index >= _items.Length)
            return false;

        ref GenItem item = ref _items[@ref.Index];

        if (!item.Used)
            return false;

        if (item.Gen != @ref.Gen)
            return false;

        return true;
    }

    public T Get(GenIndex @ref)
    {
        if (!IsValid(@ref))
            throw new ArgumentException("Get: invalid ref");

        return _items[@ref.Index].Data;
    }

    /// <summary>
    /// Warning: Reference is only valid until next call to any Pool Method.
    /// </summary>
    public ref T GetRef(GenIndex @ref)
    {
        if (!IsValid(@ref))
            throw new ArgumentException("GetRef: invalid ref");

        return ref _items[@ref.Index].Data;
    }

    public void Set(GenIndex @ref, T data)
    {
        if (!IsValid(@ref))
            throw new ArgumentException("Set: invalid ref");

        _items[@ref.Index].Data = data;
    }

    private int _borrowCount = 0;

    public delegate void BorrowAction(ref T data);

    public void Borrow(GenIndex @ref, BorrowAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!IsValid(@ref))
            throw new ArgumentException("Borrow: invalid ref");

        ref T data = ref _items[@ref.Index].Data;

        _borrowCount++;
        try
        {
            action(ref data);
        }
        finally
        {
            _borrowCount--;
        }
    }

    public GenIndex Alloc(T data)
    {
        ThrowIfBorrowed();

        int index = GetFreeIndex();

        ref GenItem item = ref _items[index];

        item.Used = true;
        item.Gen++;
        item.Data = data;

        return new GenIndex(_poolId, index, item.Gen);
    }

    public void Free(GenIndex @ref)
    {
        ThrowIfBorrowed();

        if (!IsValid(@ref))
            throw new ArgumentException("Free: invalid ref");

        ref GenItem item = ref _items[@ref.Index];

        item.Used = false;
    }

    private void ThrowIfBorrowed()
    {
        if (_borrowCount != 0)
            throw new InvalidOperationException($"GenPool cannot be modified while references are borrowed");
    }

    private int GetFreeIndex()
    {
        for (int i = 0; i < _items.Length; i++)
        {
            if (!_items[i].Used && _items[i].Gen < uint.MaxValue)
                return i;
        }

        int oldSize = _items.Length;
        int newSize = GetExpandedSize(oldSize);
        Array.Resize(ref _items, newSize);

        return oldSize;
    }

    private static int GetExpandedSize(int currentSize)
    {
        if (currentSize >= Array.MaxLength)
            throw new InvalidOperationException("GenPool cannot grow any further.");

        if (currentSize > Array.MaxLength / 2)
            return Array.MaxLength;

        return currentSize * 2;
    }
}
