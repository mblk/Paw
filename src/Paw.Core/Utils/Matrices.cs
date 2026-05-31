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

    public readonly vec2 Column1 => new(M11, M21);
    public readonly vec2 Column2 => new(M12, M22);
    public readonly vec2 Row1 => new(M11, M12);
    public readonly vec2 Row2 => new(M21, M22);

    public Matrix2x2() { }

    public Matrix2x2(float m11, float m12, float m21, float m22)
    {
        M11 = m11;
        M12 = m12;
        M21 = m21;
        M22 = m22;
    }

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

    public static Matrix2x2 Transpose(Matrix2x2 mat) => new()
    {
        M11 = mat.M11,
        M22 = mat.M22,
        M12 = mat.M21,
        M21 = mat.M12,
    };

    public static Matrix2x2 Abs(Matrix2x2 mat) => new()
    {
        M11 = MathF.Abs(mat.M11),
        M12 = MathF.Abs(mat.M12),
        M21 = MathF.Abs(mat.M21),
        M22 = MathF.Abs(mat.M22),
    };

    public static Matrix2x2 Outer(Vector2 a, Vector2 b)
    {
        var row1 = b * a.X;
        var row2 = b * a.Y;

        return new Matrix2x2()
        {
            M11 = row1.X,
            M12 = row1.Y,

            M21 = row2.X,
            M22 = row2.Y,
        };
    }

    public static Vector2 operator *(Matrix2x2 lhs, Vector2 rhs) => new(
            lhs.M11 * rhs.X + lhs.M12 * rhs.Y,
            lhs.M21 * rhs.X + lhs.M22 * rhs.Y);

    public static Matrix2x2 operator *(Matrix2x2 lhs, Matrix2x2 rhs) => new()
    {
        M11 = lhs.M11 * rhs.M11 + lhs.M12 * rhs.M21,
        M12 = lhs.M11 * rhs.M12 + lhs.M12 * rhs.M22,
        M21 = lhs.M21 * rhs.M11 + lhs.M22 * rhs.M21,
        M22 = lhs.M21 * rhs.M12 + lhs.M22 * rhs.M22,
    };

    public static Matrix2x2 operator *(Matrix2x2 lhs, float rhs) => new()
    {
        M11 = lhs.M11 * rhs,
        M12 = lhs.M12 * rhs,

        M21 = lhs.M21 * rhs,
        M22 = lhs.M22 * rhs,
    };

    public static Matrix2x2 operator /(Matrix2x2 lhs, float rhs) => new()
    {
        M11 = lhs.M11 / rhs,
        M12 = lhs.M12 / rhs,

        M21 = lhs.M21 / rhs,
        M22 = lhs.M22 / rhs,
    };

    public static Matrix2x2 operator +(Matrix2x2 lhs, Matrix2x2 rhs) => new()
    {
        M11 = lhs.M11 + rhs.M11,
        M12 = lhs.M12 + rhs.M12,

        M21 = lhs.M21 + rhs.M21,
        M22 = lhs.M22 + rhs.M22,
    };

    public static Matrix2x2 operator -(Matrix2x2 lhs, Matrix2x2 rhs) => new()
    {
        M11 = lhs.M11 - rhs.M11,
        M12 = lhs.M12 - rhs.M12,

        M21 = lhs.M21 - rhs.M21,
        M22 = lhs.M22 - rhs.M22,
    };
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

    public Matrix3x3() { }

    public Matrix3x3(float m11, float m12, float m13, float m21, float m22, float m23, float m31, float m32, float m33)
    {
        M11 = m11;
        M12 = m12;
        M13 = m13;
        M21 = m21;
        M22 = m22;
        M23 = m23;
        M31 = m31;
        M32 = m32;
        M33 = m33;
    }

    public readonly vec3 Column1 => new(M11, M21, M31);
    public readonly vec3 Column2 => new(M12, M22, M32);
    public readonly vec3 Column3 => new(M13, M23, M33);
    public readonly vec3 Row1 => new(M11, M12, M13);
    public readonly vec3 Row2 => new(M21, M22, M23);
    public readonly vec3 Row3 => new(M31, M32, M33);

    public static Matrix3x3 Diagonal(float m11, float m22, float m33)
    {
        return new Matrix3x3()
        {
            M11 = m11,
            M22 = m22,
            M33 = m33,
        };
    }

    public static Matrix3x3 Outer(vec3 a, vec3 b)
    {
        vec3 row1 = b * a.X;
        vec3 row2 = b * a.Y;
        vec3 row3 = b * a.Z;

        return new Matrix3x3()
        {
            M11 = row1.X,
            M12 = row1.Y,
            M13 = row1.Z,

            M21 = row2.X,
            M22 = row2.Y,
            M23 = row2.Z,

            M31 = row3.X,
            M32 = row3.Y,
            M33 = row3.Z,
        };
    }

    public static Vector3 operator *(Matrix3x3 lhs, Vector3 rhs) => new(
            lhs.M11 * rhs.X + lhs.M12 * rhs.Y + lhs.M13 * rhs.Z,
            lhs.M21 * rhs.X + lhs.M22 * rhs.Y + lhs.M23 * rhs.Z,
            lhs.M31 * rhs.X + lhs.M32 * rhs.Y + lhs.M33 * rhs.Z);

    public static Matrix3x3 operator *(Matrix3x3 lhs, float rhs) => new()
    {
        M11 = lhs.M11 * rhs,
        M12 = lhs.M12 * rhs,
        M13 = lhs.M13 * rhs,

        M21 = lhs.M21 * rhs,
        M22 = lhs.M22 * rhs,
        M23 = lhs.M23 * rhs,

        M31 = lhs.M31 * rhs,
        M32 = lhs.M32 * rhs,
        M33 = lhs.M33 * rhs,
    };

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

    public static Matrix3x3 operator +(Matrix3x3 lhs, Matrix3x3 rhs) => new()
    {
        M11 = lhs.M11 + rhs.M11,
        M12 = lhs.M12 + rhs.M12,
        M13 = lhs.M13 + rhs.M13,

        M21 = lhs.M21 + rhs.M21,
        M22 = lhs.M22 + rhs.M22,
        M23 = lhs.M23 + rhs.M23,

        M31 = lhs.M31 + rhs.M31,
        M32 = lhs.M32 + rhs.M32,
        M33 = lhs.M33 + rhs.M33,
    };

    public static Matrix3x3 operator -(Matrix3x3 lhs, Matrix3x3 rhs) => new()
    {
        M11 = lhs.M11 - rhs.M11,
        M12 = lhs.M12 - rhs.M12,
        M13 = lhs.M13 - rhs.M13,

        M21 = lhs.M21 - rhs.M21,
        M22 = lhs.M22 - rhs.M22,
        M23 = lhs.M23 - rhs.M23,

        M31 = lhs.M31 - rhs.M31,
        M32 = lhs.M32 - rhs.M32,
        M33 = lhs.M33 - rhs.M33,
    };

}