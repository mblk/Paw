using System.Diagnostics;

namespace Paw.Core.Physics;

public abstract class Force
{
    public const int MaxRows = 4;

    public Body? BodyA;
    public Body? BodyB;

    public vec3[] J = new vec3[MaxRows];
    public mat3[] H = new mat3[MaxRows];
    public float[] C = new float[MaxRows];
    public float[] fMin = new float[MaxRows];
    public float[] fMax = new float[MaxRows];
    public float[] Stiffness = new float[MaxRows];
    public float[] Fracture = new float[MaxRows];
    public float[] Penalty = new float[MaxRows];
    public float[] Lambda = new float[MaxRows];

    protected Force(Body? bodyA, Body? bodyB)
    {
        if (bodyA is not null)
        {
            BodyA = bodyA;
            bodyA.Forces.Add(this);
        }

        if (bodyB is not null)
        {
            BodyB = bodyB;
            bodyB.Forces.Add(this);
        }

        // Set some reasonable defaults
        for (int i = 0; i < MaxRows; i++)
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

    public void RemoveFromBodies()
    {
        if (BodyA is not null)
        {
            bool r = BodyA.Forces.Remove(this);
            Debug.Assert(r);
        }

        if (BodyB is not null)
        {
            bool r = BodyB.Forces.Remove(this);
            Debug.Assert(r);
        }
    }

    public abstract int Rows { get; }
    public abstract bool Initialize();
    public abstract void ComputeConstraint(float alpha);
    public abstract void ComputeDerivatives(Body body);
}

public class Manifold : Force
{
    public Manifold(Body bodyA, Body bodyB)
        : base(bodyA, bodyB)
    {
    }

    public override int Rows => throw new NotImplementedException();

    public override bool Initialize()
    {
        return false;
    }

    public override void ComputeConstraint(float alpha)
    {
        throw new NotImplementedException();
    }

    public override void ComputeDerivatives(Body body)
    {
        throw new NotImplementedException();
    }
}