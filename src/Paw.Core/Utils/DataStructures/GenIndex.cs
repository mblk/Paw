namespace Paw.Core.Utils.DataStructures;

public readonly struct GenIndex
{
    public readonly int PoolId;
    public readonly int Index;
    public readonly uint Gen;

    public GenIndex(int poolId, int index, uint gen)
    {
        PoolId = poolId;
        Index = index;
        Gen = gen;
    }

    public override readonly string ToString()
    {
        return $"Pool={PoolId} Index={Index} Gen={Gen}";
    }
}
