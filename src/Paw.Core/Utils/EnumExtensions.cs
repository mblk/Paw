using System.Diagnostics;
using System.Numerics;

namespace Paw.Core.Utils;

public static class EnumExtensions
{
    public static T Next<T>(this T current, int delta)
        where T : struct, Enum
    {
        var allValues = Enum.GetValues<T>();
        var currentIndex = Array.IndexOf(allValues, current);
        Debug.Assert(currentIndex != -1);
        var newIndex = currentIndex + delta;

        // check bounds
        while (newIndex < 0) newIndex += allValues.Length;
        newIndex %= allValues.Length;

        var newValue = allValues[newIndex];
        return newValue;
    }
}
