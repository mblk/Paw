using Paw.Core.Utils;

namespace Paw.Core.Physics;

public class Spring : Force
{
    public vec2 RA;
    public vec2 RB;
    public float Rest;

    public override int Rows => 1;

    public Spring(BodyRef bodyA, BodyRef bodyB, vec2 rA, vec2 rB, float stiffness)
    {
        Reset();
        //AddToBodies(bodyA, bodyB);
        BodyA = bodyA;
        BodyB = bodyB;

        Stiffness[0] = stiffness;

        RA = rA;
        RB = rB;


        //Rest = (Transform2D.LocalToWorld(bodyA.Position, rA) - Transform2D.LocalToWorld(bodyB.Position, rB)).Length();
        //Console.WriteLine($"Sprint Rest: {Rest}");
    }

    public override void OneTimeInit(bool hasBodyA, bool hasBodyB, in Body bodyA, in Body bodyB)
    {
        Rest = (Transform2D.LocalToWorld(bodyA.Position, RA) - Transform2D.LocalToWorld(bodyB.Position, RB)).Length();
        Console.WriteLine($"Spring Rest: {Rest}");
    }

    public override bool PerTickInit(bool hasBodyA, bool hasBodyB, in Body bodyA, in Body bodyB)
    {
        return true;
    }

    public override void ComputeConstraint(bool hasBodyA, bool hasBodyB, in Body bodyA, in Body bodyB, float alpha)
    {
        // Compute constraint function at current state C(x)
        C[0] = (Transform2D.LocalToWorld(bodyA.Position, RA) - Transform2D.LocalToWorld(bodyB.Position, RB)).Length() - Rest;
    }

    public override void ComputeDerivatives(bool isBodyA, in Body bodyA, in Body bodyB)
    {
        // Compute the first and second derivatives for the desired body
        mat2 S = new mat2(0, -1, 1, 0);
        mat2 I = new mat2(1, 0, 0, 1);

        vec2 d = Transform2D.LocalToWorld(bodyA.Position, RA) - Transform2D.LocalToWorld(bodyB.Position, RB);
        float dlen2 = vec2.Dot(d, d);
        if (dlen2 == 0)
            return;

        float dlen = MathF.Sqrt(dlen2);
        vec2 n = d / dlen;
        mat2 dxx = (I - mat2.Outer(n, n)) / dlen;

        if (isBodyA)
        {
            vec2 Sr = Transform2D.Rotate(bodyA.Position.Z, S * RA);
            vec2 r = Transform2D.Rotate(bodyA.Position.Z, RA);
            vec2 dxr = dxx * Sr;
            float drr = -vec2.Dot(n, r) - vec2.Dot(n, r);

            J[0] = new vec3(n, vec2.Dot(n, Sr));
            H[0] = new mat3(dxx.Row1.X, dxx.Row1.Y, dxr.X,
                            dxx.Row2.X, dxx.Row2.Y, dxr.Y,
                            dxr.X, dxr.Y, drr);
        }
        else
        {
            vec2 Sr = Transform2D.Rotate(bodyB.Position.Z, S * RB);
            vec2 r = Transform2D.Rotate(bodyB.Position.Z, RB);
            vec2 dxr = dxx * Sr;
            float drr = vec2.Dot(n, r) + vec2.Dot(n, r);

            J[0] = new vec3(-n, vec2.Dot(n, -Sr));
            H[0] = new mat3(dxx.Row1.X, dxx.Row1.Y, dxr.X,
                            dxx.Row2.X, dxx.Row2.Y, dxr.Y,
                            dxr.X, dxr.Y, drr);
        }
    }
}
