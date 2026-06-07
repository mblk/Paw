using System.Runtime.CompilerServices;

namespace Paw.Core.Physics;

public enum ConstraintType
{
    Invalid = 0,

    Manifold,
    Joint,
    Spring,
    Motor,
}

public struct Constraint
{
    public const int MAX_ROWS = 4;

    [InlineArray(MAX_ROWS)]
    public struct FloatRows
    {
        private float _element0;
    }

    [InlineArray(MAX_ROWS)]
    public struct Vec3Rows
    {
        private vec3 _element0;
    }

    [InlineArray(MAX_ROWS)]
    public struct Mat3Rows
    {
        private mat3 _element0;
    }

    public uint Gen;
    public bool Used;

    public ConstraintType Type;

    public BodyRef BodyA; // optional
    public BodyRef BodyB; // mandatory

    public Vec3Rows J;
    public Mat3Rows H;
    public FloatRows C;
    public FloatRows fMin;
    public FloatRows fMax;
    public FloatRows Stiffness;
    public FloatRows Fracture;
    public FloatRows Penalty;
    public FloatRows Lambda;

    public Constraint()
    {
        Reset();
    }

    public void Reset()
    {
        Type = ConstraintType.Invalid;

        BodyA = default;
        BodyB = default;

        // Set some reasonable defaults
        for (int i = 0; i < MAX_ROWS; i++)
        {
            J[i] = default;
            H[i] = default;
            C[i] = default;
            Stiffness[i] = float.PositiveInfinity;
            fMax[i] = float.PositiveInfinity;
            fMin[i] = float.NegativeInfinity;
            Fracture[i] = float.PositiveInfinity;
            Penalty[i] = 0f;
            Lambda[i] = 0f;
        }
    }
}
