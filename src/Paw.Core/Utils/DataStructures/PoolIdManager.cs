using System.Threading;

namespace Paw.Core.Utils.DataStructures;

public static class PoolIdManager
{
    private static int _nextPoolId = 1;

    public static int GetNextId() => Interlocked.Increment(ref _nextPoolId);
}
