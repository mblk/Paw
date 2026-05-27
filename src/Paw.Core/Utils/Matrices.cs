namespace Paw.Core.Utils;

/// <summary>
/// 2x2 Matrix type
/// </summary>
public struct Matrix2x2
{
    /// <summary>The first element of the first row.</summary>
    public float M11;

    /// <summary>The second element of the first row.</summary>
    public float M12;

    /// <summary>The first element of the second row.</summary>
    public float M21;

    /// <summary>The second element of the second row.</summary>
    public float M22;

    /// <summary>
    /// Creates rotation matrix for specified angle. Positive values rotate CCW.
    /// </summary>
    public static Matrix2x2 Rotation(float angle)
    {
        float c = MathF.Cos(angle);
        float s = MathF.Sin(angle);

        return new Matrix2x2
        {
            M11 = c,
            M12 = -s,
            M21 = s,
            M22 = c,
        };
    }

    public static Vector2 operator *(Matrix2x2 lhs, Vector2 rhs) => new(
            lhs.M11 * rhs.X + lhs.M12 * rhs.Y,
            lhs.M21 * rhs.X + lhs.M22 * rhs.Y);
}

public struct Matrix3x3
{
    /// <summary>The first element of the first row.</summary>
    public float M11;

    /// <summary>The second element of the first row.</summary>
    public float M12;

    /// <summary>The third element of the first row.</summary>
    public float M13;

    /// <summary>The first element of the second row.</summary>
    public float M21;

    /// <summary>The second element of the second row.</summary>
    public float M22;

    /// <summary>The third element of the second row.</summary>
    public float M23;

    /// <summary>The first element of the third row.</summary>
    public float M31;

    /// <summary>The second element of the third row.</summary>
    public float M32;

    /// <summary>The third element of the third row.</summary>
    public float M33;

    public static Matrix3x3 Diagonal(float m11, float m22, float m33)
    {
        return new Matrix3x3()
        {
            M11 = m11,
            M22 = m22,
            M33 = m33,
        };
    }

    public static Matrix3x3 operator /(Matrix3x3 lhs, float rhs) => new()
    {
        M11 = lhs.M11 / rhs,
        M12 = lhs.M12 / rhs,
        M13 = lhs.M13 / rhs,
        M21 = lhs.M21 / rhs,
        M22 = lhs.M22 / rhs,
        M23 = lhs.M23 / rhs,
        M31 = lhs.M31 / rhs,
        M32 = lhs.M32 / rhs,
        M33 = lhs.M33 / rhs,
    };

    public static Vector3 operator *(Matrix3x3 lhs, Vector3 rhs) => new(
            lhs.M11 * rhs.X + lhs.M12 * rhs.Y + lhs.M13 * rhs.Z,
            lhs.M21 * rhs.X + lhs.M22 * rhs.Y + lhs.M23 * rhs.Z,
            lhs.M31 * rhs.X + lhs.M32 * rhs.Y + lhs.M33 * rhs.Z);
}