using System.Numerics;

namespace Paw.Core.Utils;

public static class MathUtils
{
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

    extension(float value)
    {
        public float SnapToPixel()
        {
            return MathF.Round(value);
        }

        public float Clamp(float min, float max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        public float DegToRad()
        {
            return value * MathF.PI / 180f;
        }

        public float RadToDeg()
        {
            return value * 180f / MathF.PI;
        }
    }

    extension(Vector2 p)
    {
        public Vector2 SnapToPixel()
        {
            return new Vector2(SnapToPixel(p.X), SnapToPixel(p.Y));
        }

        public Vector2 Rotate(float angle)
        {
            float sin = MathF.Sin(angle);
            float cos = MathF.Cos(angle);

            return new Vector2(
                x: p.X * cos - p.Y * sin,
                y: p.X * sin + p.Y * cos
            );
        }

        public Vector2 RotateAround(Vector2 center, float angle)
        {
            Vector2 vrel = p - center;
            Vector2 vrot = vrel.Rotate(angle);
            return center + vrot;
        }
    }

    extension(Vector3 v)
    {
        public Vector2 XY => new(v.X, v.Y);

        public Vector3 Signs => new(MathF.Sign(v.X),
                                    MathF.Sign(v.Y),
                                    MathF.Sign(v.Z));

        public Vector3 Clamp(float min, float max) => new(v.X.Clamp(min, max),
                                                          v.Y.Clamp(min, max),
                                                          v.Z.Clamp(min, max));
    }

    extension(Vector4 v)
    {
        public Vector2 XY => new(v.X, v.Y);
        public Vector3 XYZ => new(v.X, v.Y, v.Z);
    }
}