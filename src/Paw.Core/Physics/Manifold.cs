using Paw.Core.Utils;
using System.Runtime.InteropServices;

namespace Paw.Core.Physics;

public class Manifold : Force, IPoolable
{
    // Used to track contact features between frames
    [StructLayout(LayoutKind.Explicit)]
    public struct FeaturePair
    {
        [FieldOffset(0)]
        public Edges E;

        [FieldOffset(0)]
        public int Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Edges
    {
        public EdgeNumbers InEdge1;
        public EdgeNumbers OutEdge1;
        public EdgeNumbers InEdge2;
        public EdgeNumbers OutEdge2;
    }

    public enum EdgeNumbers : byte
    {
        NO_EDGE = 0,
        EDGE1,
        EDGE2,
        EDGE3,
        EDGE4
    };

    public struct Contact
    {
        public FeaturePair FP;
        public vec2 RA;         // in A-space
        public vec2 RB;         // in B-space
        public vec2 Normal;     // in world space

        public vec3 JAn, JBn, JAt, JBt;
        public vec2 C0;
        public bool Stick;
    }

    public Contact[] Contacts = new Contact[2];
    public int NumContacts;
    public float Friction;

    public Manifold()
    {
        Reset();
    }

    public new void Reset()
    {
        base.Reset();

        fMax[0] = fMax[2] = 0f;
        fMin[0] = fMin[2] = float.NegativeInfinity;

        Contacts[0] = default;
        Contacts[1] = default;

        NumContacts = 0;
        Friction = 0;
    }

    public override int Rows => NumContacts * 2;

    public override bool Initialize()
    {
        // Compute friction
        Friction = MathF.Sqrt(BodyA!.Friction * BodyB!.Friction);

        // Store previous contact state
        ReadOnlySpan<Contact> oldContacts = [Contacts[0], Contacts[1]]; // uses stackalloc
        ReadOnlySpan<float> oldPenalty = [Penalty[0], Penalty[1], Penalty[2], Penalty[3]];
        ReadOnlySpan<float> oldLambda = [Lambda[0], Lambda[1], Lambda[2], Lambda[3]];
        ReadOnlySpan<bool> oldStick = [Contacts[0].Stick, Contacts[1].Stick];
        int oldNumContacts = NumContacts;

        // Compute new contacts
        NumContacts = Collision.Collide(BodyA!, BodyB!, Contacts);

        // Merge old contact data with new contacts
        for (int i = 0; i < NumContacts; i++)
        {
            Penalty[i * 2 + 0] = Penalty[i * 2 + 1] = 0.0f;
            Lambda[i * 2 + 0] = Lambda[i * 2 + 1] = 0.0f;

            for (int j = 0; j < oldNumContacts; j++)
            {
                if (Contacts[i].FP.Value == oldContacts[j].FP.Value)
                {
                    Penalty[i * 2 + 0] = oldPenalty[j * 2 + 0];
                    Penalty[i * 2 + 1] = oldPenalty[j * 2 + 1];
                    Lambda[i * 2 + 0] = oldLambda[j * 2 + 0];
                    Lambda[i * 2 + 1] = oldLambda[j * 2 + 1];
                    Contacts[i].Stick = oldStick[j];

                    // If static friction in last frame, use the old contact points
                    if (oldStick[j])
                    {
                        Contacts[i].RA = oldContacts[j].RA;
                        Contacts[i].RB = oldContacts[j].RB;
                    }
                }
            }
        }

        for (int i = 0; i < NumContacts; i++)
        {
            // Compute the contact basis (Eq. 15)
            vec2 normal = Contacts[i].Normal;
            vec2 tangent = new vec2(normal.Y, -normal.X);
            mat2 basis = new mat2()
            {
                M11 = normal.X,
                M12 = normal.Y,
                M21 = tangent.X,
                M22 = tangent.Y,
            };

            vec2 rAW = Contacts[i].RA.Rotate(BodyA!.Position.Z);
            vec2 rBW = Contacts[i].RB.Rotate(BodyB!.Position.Z);

            // Precompute the constraint and derivatives at C(x-), since we use a truncated Taylor series for contacts (Sec 4).
            // Note that we discard the second order term, since it is insignificant for contacts
            Contacts[i].JAn = new vec3(+basis.M11, +basis.M12, +vec2.Cross(rAW, normal));
            Contacts[i].JBn = new vec3(-basis.M11, -basis.M12, -vec2.Cross(rBW, normal));
            Contacts[i].JAt = new vec3(+basis.M21, +basis.M22, +vec2.Cross(rAW, tangent));
            Contacts[i].JBt = new vec3(-basis.M21, -basis.M22, -vec2.Cross(rBW, tangent));

            Contacts[i].C0 = basis * (BodyA!.Position.XY + rAW - BodyB!.Position.XY - rBW) + new vec2(SolverConfig.COLLISION_MARGIN, 0);
        }

        return NumContacts > 0;
    }

    public override void ComputeConstraint(float alpha)
    {
        for (int i = 0; i < NumContacts; i++)
        {
            // Compute the Taylor series approximation of the constraint function C(x) (Sec 4)
            vec3 dpA = BodyA!.Position - BodyA!.Initial;
            vec3 dpB = BodyB!.Position - BodyB!.Initial;

            C[i * 2 + 0] = Contacts[i].C0.X * (1 - alpha) + vec3.Dot(Contacts[i].JAn, dpA) + vec3.Dot(Contacts[i].JBn, dpB);
            C[i * 2 + 1] = Contacts[i].C0.Y * (1 - alpha) + vec3.Dot(Contacts[i].JAt, dpA) + vec3.Dot(Contacts[i].JBt, dpB);

            // Update the friction bounds using the latest lambda values
            float frictionBound = MathF.Abs(Lambda[i * 2 + 0]) * Friction;
            fMax[i * 2 + 1] = frictionBound;
            fMin[i * 2 + 1] = -frictionBound;

            // Check if the contact is sticking, so that on the next frame we can use the old contact points for better static friction handling
            Contacts[i].Stick = MathF.Abs(Lambda[i * 2 + 1]) < frictionBound && MathF.Abs(Contacts[i].C0.Y) < SolverConfig.STICK_THRESH;
        }
    }

    public override void ComputeDerivatives(Body body)
    {
        // Just store precomputed derivatives in J for the desired body
        for (int i = 0; i < NumContacts; i++)
        {
            if (body == BodyA)
            {
                J[i * 2 + 0] = Contacts[i].JAn;
                J[i * 2 + 1] = Contacts[i].JAt;
            }
            else
            {
                J[i * 2 + 0] = Contacts[i].JBn;
                J[i * 2 + 1] = Contacts[i].JBt;
            }
        }
    }
}