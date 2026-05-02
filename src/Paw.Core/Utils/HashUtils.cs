using System.Diagnostics;

namespace Paw.Core.Utils;

public static class HashUtils
{
    public static ulong HashString64(string s) // FNV-1a
    {
        unchecked
        {
            const ulong offsetBasis = 0xCBF29CE484222325;
            const ulong prime = 0x100000001B3UL;

            Debug.Assert(offsetBasis == 14695981039346656037UL);
            Debug.Assert(prime == 1099511628211UL);

            ulong hash = offsetBasis;

            for (int i = 0; i < s.Length; i++)
            {
                hash ^= s[i];
                hash *= prime;
            }

            return Avalanche64(hash);
        }
    }

    public static ulong Combine64(ulong a, ulong b)
    {
        unchecked
        {
            return Avalanche64(a ^ (b + 0x9E3779B97F4A7C15UL + (a << 6) + (a >> 2)));
        }
    }

    private static ulong Avalanche64(ulong x)
    {
        unchecked
        {
            x ^= x >> 30;
            x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27;
            x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return x;
        }
    }
}
