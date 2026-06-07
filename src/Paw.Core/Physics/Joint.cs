using Paw.Core.Utils;

namespace Paw.Core.Physics;

public class Joint : Force
{
    public vec2 RA;
    public vec2 RB;
    public vec3 C0;
    public float TorqueArm;
    public float RestAngle;

    public override int Rows => 3;

    public Joint(BodyRef bodyA, BodyRef bodyB, vec2 rA, vec2 rB, vec3 stiffness, float fracture = float.PositiveInfinity)
    {
        Reset();
        //AddToBodies(bodyA, bodyB);

        BodyA = bodyA;
        BodyB = bodyB;

        RA = rA;
        RB = rB;

        Stiffness[0] = stiffness.X;
        Stiffness[1] = stiffness.Y;
        Stiffness[2] = stiffness.Z;

        fMin[2] = -fracture;
        fMax[2] = fracture;
        Fracture[2] = fracture;

        //if (bodyA != default)
        //{
        //    RestAngle = bodyA.Position.Z - bodyB.Position.Z;
        //    TorqueArm = (bodyA.Size + bodyB.Size).LengthSquared(); // why LengthSquared?
        //}
        //else
        //{
        //    RestAngle = 0f - bodyB.Position.Z;
        //    TorqueArm = bodyB.Size.LengthSquared();
        //}
    }

    public override void OneTimeInit(bool hasBodyA, bool hasBodyB, in Body bodyA, in Body bodyB)
    {
        if (hasBodyA)
        {
            RestAngle = bodyA.Position.Z - bodyB.Position.Z;
            TorqueArm = (bodyA.Size + bodyB.Size).LengthSquared(); // why LengthSquared?
        }
        else
        {
            RestAngle = 0f - bodyB.Position.Z;
            TorqueArm = bodyB.Size.LengthSquared();
        }
    }

    public override bool PerTickInit(bool hasBodyA, bool hasBodyB, in Body bodyA, in Body bodyB)
    {
        // Store constraint function at beginnning of timestep C(x-)
        // Note: if bodyA is null, it is assumed that the joint connects a body to the world space position rA

        if (hasBodyA)
        {
            vec2 cXY = Transform2D.LocalToWorld(bodyA.Position, RA) - Transform2D.LocalToWorld(bodyB.Position, RB);
            float cZ = (bodyA.Position.Z - bodyB.Position.Z - RestAngle) * TorqueArm;
            C0 = new vec3(cXY, cZ);
        }
        else
        {
            vec2 cXY = RA - Transform2D.LocalToWorld(bodyB.Position, RB);
            float cZ = (0f - bodyB.Position.Z - RestAngle) * TorqueArm;
            C0 = new vec3(cXY, cZ);
        }

        return Stiffness[0] != 0f || Stiffness[1] != 0f || Stiffness[2] != 0f;
    }

    public override void ComputeConstraint(bool hasBodyA, bool hasBodyB, in Body bodyA, in Body bodyB, float alpha)
    {
        // Compute constraint function at current state C(x)
        vec3 Cn;

        if (hasBodyA)
        {
            vec2 cXY = Transform2D.LocalToWorld(bodyA.Position, RA) - Transform2D.LocalToWorld(bodyB.Position, RB);
            float cZ = (bodyA.Position.Z - bodyB.Position.Z - RestAngle) * TorqueArm;
            Cn = new vec3(cXY, cZ);
        }
        else
        {
            vec2 cXY = RA - Transform2D.LocalToWorld(bodyB.Position, RB);
            float cZ = (0f - bodyB.Position.Z - RestAngle) * TorqueArm;
            Cn = new vec3(cXY, cZ);
        }

        for (int i = 0; i < Rows; i++)
        {
            // Store stabilized constraint function, if a hard constraint (Eq. 18)
            if (float.IsInfinity(Stiffness[i]))
                C[i] = Cn[i] - C0[i] * alpha;
            else
                C[i] = Cn[i];
        }
    }

    public override void ComputeDerivatives(bool isBodyA, in Body bodyA, in Body bodyB)
    {
        // Compute the first and second derivatives for the desired body
        if (isBodyA)
        {
            vec2 r = Transform2D.Rotate(bodyA.Position.Z, RA);

            J[0] = new vec3(1.0f, 0.0f, -r.Y);
            J[1] = new vec3(0.0f, 1.0f, r.X);
            J[2] = new vec3(0.0f, 0.0f, TorqueArm);

            H[0] = new mat3() { M33 = -r.X };
            H[1] = new mat3() { M33 = -r.Y };
            H[2] = default;
        }
        else
        {
            vec2 r = Transform2D.Rotate(bodyB.Position.Z, RB);

            J[0] = new vec3(-1.0f, 0.0f, r.Y);
            J[1] = new vec3(0.0f, -1.0f, -r.X);
            J[2] = new vec3(0.0f, 0.0f, -TorqueArm);

            H[0] = new mat3() { M33 = r.X };
            H[1] = new mat3() { M33 = r.Y };
            H[2] = default;
        }
    }
}