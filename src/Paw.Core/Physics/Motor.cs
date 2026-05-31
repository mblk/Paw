namespace Paw.Core.Physics;

public class Motor : Force
{
    public float Speed;

    public override int Rows => 1;

    public Motor(Body? bodyA, Body bodyB, float targetSpeed, float maxTorque)
    {
        Reset();
        AddToBodies(bodyA, bodyB);

        fMax[0] = maxTorque;
        fMin[0] = -maxTorque;

        Speed = targetSpeed;
    }

    public override bool Initialize()
    {
        return true;
    }

    public override void ComputeConstraint(float alpha)
    {
        const float dt = 1.0f / 60.0f;

        // Compute delta angular position between the two bodies
        float dAngleA = (BodyA is not null ? (BodyA.Position.Z - BodyA.Initial.Z) : 0.0f);
        float dAngleB = BodyB!.Position.Z - BodyB.Initial.Z;
        float deltaAngle = dAngleA - dAngleB;

        // Constraint tries to reach desired angular speed
        C[0] = deltaAngle - Speed * dt;
    }

    public override void ComputeDerivatives(Body body)
    {
        // Compute the first and second derivatives for the desired body
        if (body == BodyA)
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