using System.Diagnostics;

namespace Paw.Core.Utils;

public interface IPoolable
{
    void Reset();
}

public class ObjectPool<T>
    where T : class, IPoolable, new()
{
    private readonly List<T> _allObjects;
    private readonly Stack<T> _available;

    public ObjectPool(int initialSize = 16)
    {
        _allObjects = new List<T>(initialSize);
        _available = new Stack<T>(initialSize);

        for (int i = 0; i < initialSize; i++)
        {
            T obj = new T();
            obj.Reset();
            _allObjects.Add(obj);
            _available.Push(obj);
        }
    }

    public T Get()
    {
        if (_available.TryPop(out T? obj))
        {
            obj.Reset(); // remove?
            return obj;
        }

        Console.WriteLine($"ObjectPool creating new {typeof(T).Name} ...");
        obj = new T();
        obj.Reset();

        _allObjects.Add(obj);

        return obj;
    }

    public void Return(T obj)
    {
        Debug.Assert(_allObjects.Contains(obj));

        obj.Reset();
        _available.Push(obj);
    }
}
