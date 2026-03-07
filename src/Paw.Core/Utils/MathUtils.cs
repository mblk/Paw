using System.Numerics;

namespace Paw.Core.Utils;

public static class MathUtils
{
    //extension(float t)
    //{
    //    public float Lerp(float a, float b)
    //    {
    //        return a + t * (b - a);
    //    }
    //}

    public static float Lerp(float a, float b, float t)
    {
        return a + t * (b - a);
    }

    public static float InverseLerp(float a, float b, float value)
    {
        return (value - a) / (b - a);
    }

    public static float Remap(float inA, float inB, float outA, float outB, float value)
    {
        return outA + (value - inA) / (inB - inA) * (outB - outA);
    }

    public static float Clamp(this float value, float min, float max)
    {
        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }

    public static Vector2 Rotate(this Vector2 input, float angle)
    {
        float sin = MathF.Sin(angle);
        float cos = MathF.Cos(angle);

        return new Vector2(
            x: input.X * cos - input.Y * sin,
            y: input.X * sin + input.Y * cos
        );
    }

    public static Vector2 RotateAround(this Vector2 input, Vector2 center, float angle)
    {
        Vector2 v = input - center;
        Vector2 vrot = v.Rotate(angle);
        return center + vrot;
    }
}
