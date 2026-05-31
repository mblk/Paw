using System.Diagnostics;

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

        [Conditional("DEBUG")]
        public void VerifyFinite()
        {
            if (!float.IsFinite(value))
            {
                Debugger.Break();
                throw new InvalidOperationException("Value not finite");
            }
        }
    }

    extension(Vector2 v)
    {
        public Vector2 SnapToPixel()
        {
            return new Vector2(SnapToPixel(v.X), SnapToPixel(v.Y));
        }

        public Vector2 Rotate(float angle)
        {
            float sin = MathF.Sin(angle);
            float cos = MathF.Cos(angle);

            return new Vector2(
                x: v.X * cos - v.Y * sin,
                y: v.X * sin + v.Y * cos
            );
        }

        public Vector2 RotateAround(Vector2 center, float angle)
        {
            Vector2 vrel = v - center;
            Vector2 vrot = vrel.Rotate(angle);
            return center + vrot;
        }

        [Conditional("DEBUG")]
        public void VerifyFinite()
        {
            if (!float.IsFinite(v.X) || !float.IsFinite(v.Y))
            {
                Debugger.Break();
                throw new InvalidOperationException("Vector elements not finite");
            }
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

        [Conditional("DEBUG")]
        public void VerifyFinite()
        {
            if (!float.IsFinite(v.X) || !float.IsFinite(v.Y) || !float.IsFinite(v.Z))
            {
                Debugger.Break();
                throw new InvalidOperationException("Vector elements not finite");
            }
        }
    }

    extension(Vector4 v)
    {
        public Vector2 XY => new(v.X, v.Y);
        public Vector3 XYZ => new(v.X, v.Y, v.Z);

        [Conditional("DEBUG")]
        public void VerifyFinite()
        {
            if (!float.IsFinite(v.X) || !float.IsFinite(v.Y) || !float.IsFinite(v.Z) || !float.IsFinite(v.W))
            {
                Debugger.Break();
                throw new InvalidOperationException("Vector elements not finite");
            }
        }
    }
}

public static class Transform2D
{
    // q = x,y,angle
    public static vec2 LocalToWorld(vec3 q, vec2 vLocal)
    {
        return mat2.Rotation(q.Z) * vLocal + q.XY;
    }

    public static vec2 WorldToLocal(vec3 q, vec2 vWorld)
    {
        return mat2.Rotation(-q.Z) * (vWorld - q.XY);
    }

    public static vec2 Rotate(float angle, vec2 v)
    {
        return mat2.Rotation(angle) * v;
    }
}