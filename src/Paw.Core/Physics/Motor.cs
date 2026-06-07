namespace Paw.Core.Physics;

public struct Motor
{
    public float Speed;

    public static int Rows => 1;

    public void OneTimeInit(ref Constraint constraint,
                            BodyRef bodyRefA, BodyRef bodyRefB, bool hasBodyA, bool hasBodyB, in Body bodyA, in Body bodyB,
                            float targetSpeed, float maxTorque)
    {
        constraint.Reset();

        constraint.Type = ConstraintType.Motor;

        constraint.BodyA = bodyRefA;
        constraint.BodyB = bodyRefB;

        constraint.fMax[0] = maxTorque;
        constraint.fMin[0] = -maxTorque;

        Speed = targetSpeed;
    }

    public bool PerTickInit(ref Constraint constraint, bool hasBodyA, bool hasBodyB, in Body bodyA, in Body bodyB)
    {
        return true;
    }

    public void ComputeConstraint(ref Constraint constraint, bool hasBodyA, bool hasBodyB, in Body bodyA, in Body bodyB, float alpha)
    {
        const float dt = 1.0f / 60.0f;

        // Compute delta angular position between the two bodies
        float dAngleA = (hasBodyA ? (bodyA.Position.Z - bodyA.Initial.Z) : 0.0f);
        float dAngleB = bodyB.Position.Z - bodyB.Initial.Z;
        float deltaAngle = dAngleA - dAngleB;

        // Constraint tries to reach desired angular speed
        constraint.C[0] = deltaAngle - Speed * dt;
    }

    public void ComputeDerivatives(ref Constraint constraint, bool isBodyA, in Body bodyA, in Body bodyB)
    {
        // Compute the first and second derivatives for the desired body
        if (isBodyA)
        {
            constraint.J[0] = new vec3(0.0f, 0.0f, 1.0f);
            constraint.H[0] = default;
        }
        else
        {
            constraint.J[0] = new vec3(0.0f, 0.0f, -1.0f);
            constraint.H[0] = default;
        }
    }
}