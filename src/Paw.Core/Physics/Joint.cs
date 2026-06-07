using Paw.Core.Utils;

namespace Paw.Core.Physics;

public struct Joint
{
    public vec2 RA;
    public vec2 RB;
    public vec3 C0;
    public float TorqueArm;
    public float RestAngle;

    public static int Rows => 3;

    public void OneTimeInit(ref Constraint constraint,
                            BodyRef bodyRefA, BodyRef bodyRefB, bool hasBodyA, bool hasBodyB, in Body bodyA, in Body bodyB,
                            vec2 rA, vec2 rB, vec3 stiffness, float fracture = float.PositiveInfinity)
    {
        constraint.Reset();

        constraint.Type = ConstraintType.Joint;

        constraint.BodyA = bodyRefA;
        constraint.BodyB = bodyRefB;

        constraint.Stiffness[0] = stiffness.X;
        constraint.Stiffness[1] = stiffness.Y;
        constraint.Stiffness[2] = stiffness.Z;

        constraint.fMin[2] = -fracture;
        constraint.fMax[2] = fracture;
        constraint.Fracture[2] = fracture;

        RA = rA;
        RB = rB;

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

    public bool PerTickInit(ref Constraint constraint, bool hasBodyA, bool hasBodyB, in Body bodyA, in Body bodyB)
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

        return constraint.Stiffness[0] != 0f || constraint.Stiffness[1] != 0f || constraint.Stiffness[2] != 0f;
    }

    public void ComputeConstraint(ref Constraint constraint, bool hasBodyA, bool hasBodyB, in Body bodyA, in Body bodyB, float alpha)
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
            if (float.IsInfinity(constraint.Stiffness[i]))
                constraint.C[i] = Cn[i] - C0[i] * alpha;
            else
                constraint.C[i] = Cn[i];
        }
    }

    public void ComputeDerivatives(ref Constraint constraint, bool isBodyA, in Body bodyA, in Body bodyB)
    {
        // Compute the first and second derivatives for the desired body
        if (isBodyA)
        {
            vec2 r = Transform2D.Rotate(bodyA.Position.Z, RA);

            constraint.J[0] = new vec3(1.0f, 0.0f, -r.Y);
            constraint.J[1] = new vec3(0.0f, 1.0f, r.X);
            constraint.J[2] = new vec3(0.0f, 0.0f, TorqueArm);

            constraint.H[0] = new mat3() { M33 = -r.X };
            constraint.H[1] = new mat3() { M33 = -r.Y };
            constraint.H[2] = default;
        }
        else
        {
            vec2 r = Transform2D.Rotate(bodyB.Position.Z, RB);

            constraint.J[0] = new vec3(-1.0f, 0.0f, r.Y);
            constraint.J[1] = new vec3(0.0f, -1.0f, -r.X);
            constraint.J[2] = new vec3(0.0f, 0.0f, -TorqueArm);

            constraint.H[0] = new mat3() { M33 = r.X };
            constraint.H[1] = new mat3() { M33 = r.Y };
            constraint.H[2] = default;
        }
    }
}