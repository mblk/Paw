namespace Paw.Core.Physics;

public class Motor : Force
{
    public float Speed;

    public override int Rows => 1;

    public Motor(BodyRef bodyA, BodyRef bodyB, float targetSpeed, float maxTorque)
    {
        Reset();

        //AddToBodies(bodyA, bodyB);
        BodyA = bodyA;
        BodyB = bodyB;

        fMax[0] = maxTorque;
        fMin[0] = -maxTorque;

        Speed = targetSpeed;
    }

    public override void OneTimeInit(bool hasBodyA, bool hasBodyB, in Body bodyA, in Body bodyB)
    {
    }

    public override bool PerTickInit(bool hasBodyA, bool hasBodyB, in Body bodyA, in Body bodyB)
    {
        return true;
    }

    public override void ComputeConstraint(bool hasBodyA, bool hasBodyB, in Body bodyA, in Body bodyB, float alpha)
    {
        const float dt = 1.0f / 60.0f;

        // Compute delta angular position between the two bodies
        float dAngleA = (hasBodyA ? (bodyA.Position.Z - bodyA.Initial.Z) : 0.0f);
        float dAngleB = bodyB.Position.Z - bodyB.Initial.Z;
        float deltaAngle = dAngleA - dAngleB;

        // Constraint tries to reach desired angular speed
        C[0] = deltaAngle - Speed * dt;
    }

    public override void ComputeDerivatives(bool isBodyA, in Body bodyA, in Body bodyB)
    {
        // Compute the first and second derivatives for the desired body
        if (isBodyA)
        {
            J[0] = new vec3(0.0f, 0.0f, 1.0f);
            H[0] = default;
        }
        else
        {
            J[0] = new vec3(0.0f, 0.0f, -1.0f);
            H[0] = default;
        }
    }
}