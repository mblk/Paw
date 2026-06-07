using System.Diagnostics;
using System.Dynamic;

namespace Paw.Core.Physics;

public abstract class Force
{
    public const int MAX_ROWS = 4;

    public BodyRef BodyA; // optional
    public BodyRef BodyB; // mandatory

    public vec3[] J = new vec3[MAX_ROWS];
    public mat3[] H = new mat3[MAX_ROWS];
    public float[] C = new float[MAX_ROWS];
    public float[] fMin = new float[MAX_ROWS];
    public float[] fMax = new float[MAX_ROWS];
    public float[] Stiffness = new float[MAX_ROWS];
    public float[] Fracture = new float[MAX_ROWS];
    public float[] Penalty = new float[MAX_ROWS];
    public float[] Lambda = new float[MAX_ROWS];

    public void Reset()
    {
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

    //public void AddToBodies(Body? bodyA, Body? bodyB)
    //{
    //    if (bodyA is not null)
    //    {
    //        BodyA = bodyA;
    //        bodyA.Forces.Add(this);
    //    }

    //    if (bodyB is not null)
    //    {
    //        BodyB = bodyB;
    //        bodyB.Forces.Add(this);
    //    }
    //}

    //public void RemoveFromBodies()
    //{
    //    if (BodyA is not null)
    //    {
    //        bool r = BodyA.Forces.Remove(this);
    //        Debug.Assert(r);
    //    }

    //    if (BodyB is not null)
    //    {
    //        bool r = BodyB.Forces.Remove(this);
    //        Debug.Assert(r);
    //    }
    //}

    public abstract int Rows { get; }

    public abstract void OneTimeInit(bool hasBodyA, bool hasBodyB, in Body bodyA, in Body bodyB);
    public abstract bool PerTickInit(bool hasBodyA, bool hasBodyB, in Body bodyA, in Body bodyB);

    public abstract void ComputeConstraint(bool hasBodyA, bool hasBodyB, in Body bodyA, in Body bodyB, float alpha);
    public abstract void ComputeDerivatives(bool isBodyA, in Body bodyA, in Body bodyB);
}
