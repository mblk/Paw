// Copied and modified from box2D-lite: https://github.com/erincatto/box2d-lite

/*
    MIT License

    Copyright (c) 2019 Erin Catto

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using Paw.Core.Utils;

using static Paw.Core.Physics.Manifold;

namespace Paw.Core.Physics;

public static class Collision
{
    // Box vertex and edge numbering:
    //
    //        ^ y
    //        |
    //        e1
    //   v2 ------ v1
    //    |        |
    // e2 |        | e4  --> x
    //    |        |
    //   v3 ------ v4
    //        e3

    public enum Axis
    {
        FACE_A_X,
        FACE_A_Y,
        FACE_B_X,
        FACE_B_Y
    }

    public struct ClipVertex
    {
        public vec2 V;
        public FeaturePair FP;
    }

    public static int Collide(Body bodyA, Body bodyB, Contact[] contacts) // OBB-vs-OBB collision test
    {
        // Setup
        vec2 hsA = bodyA.Size * 0.5f;
        vec2 hsB = bodyB.Size * 0.5f;

        vec2 posA = bodyA.Position.XY;
        vec2 posB = bodyB.Position.XY;

        mat2 rotA = mat2.Rotation(bodyA.Position.Z);
        mat2 rotB = mat2.Rotation(bodyB.Position.Z);

        mat2 rotAT = mat2.Transpose(rotA); // inv rot
        mat2 rotBT = mat2.Transpose(rotB); // inv rot

        vec2 dp = posB - posA; // A to B
        vec2 dA = rotAT * dp; // A to B in A-space
        vec2 dB = rotBT * dp; // A to B in B-space

        mat2 C = rotAT * rotB; // rotation of B in A-space = describes coordinate system axis of B in A-space
        mat2 absC = mat2.Abs(C); // sign is irrelevant for projections
        mat2 absCT = mat2.Transpose(absC); // rotation of A in B-space

        // Separating Axis Test
        // faceA/faceB = Separation/penetration values along axes of A/B

        // Box A faces
        vec2 faceA = vec2.Abs(dA) - hsA - absC * hsB; // `absC * hsB` => half size of B projected to axes of A
        if (faceA.X > 0.0f || // there is a gap on the x-axis of A
            faceA.Y > 0.0f)   // there is a gap on the y-axis of A
            return 0;

        // Box B faces
        vec2 faceB = vec2.Abs(dB) - absCT * hsA - hsB;
        if (faceB.X > 0.0f || faceB.Y > 0.0f)
            return 0;

        // Find best axis (highest separation, smallest penetration)
        Axis axis;
        //float separation;   // separation/penetration on the best axis
        vec2 normal;        // The normal points from A to B in world space
        {
            // Box A faces
            axis = Axis.FACE_A_X;
            float separation = faceA.X;
            normal = dA.X > 0f ? rotA.Column1 : -rotA.Column1; // Col1 is (1,0) for zero-angle

            const float relativeTol = 0.95f;
            const float absoluteTol = 0.01f;

            if (faceA.Y > relativeTol * separation + absoluteTol * hsA.Y)
            {
                axis = Axis.FACE_A_Y;
                separation = faceA.Y;
                normal = dA.Y > 0.0f ? rotA.Column2 : -rotA.Column2; // Col2 is (1,0) for zero-angle
            }

            // Box B faces
            if (faceB.X > relativeTol * separation + absoluteTol * hsB.X)
            {
                axis = Axis.FACE_B_X;
                separation = faceB.X;
                normal = dB.X > 0.0f ? rotB.Column1 : -rotB.Column1;
            }

            if (faceB.Y > relativeTol * separation + absoluteTol * hsB.Y)
            {
                axis = Axis.FACE_B_Y;
                separation = faceB.Y;
                normal = dB.Y > 0.0f ? rotB.Column2 : -rotB.Column2;
            }
        }

        // Setup clipping plane data based on the separating axis
        vec2 frontNormal, sideNormal;
        Span<ClipVertex> incidentEdge = stackalloc ClipVertex[2];
        float front, negSide, posSide;
        EdgeNumbers negEdge, posEdge;

        // Compute the clipping lines and the line segment to be clipped.
        switch (axis)
        {
            case Axis.FACE_A_X:
            {
                frontNormal = normal;
                front = vec2.Dot(posA, frontNormal) + hsA.X;
                sideNormal = rotA.Column2;
                float side = vec2.Dot(posA, sideNormal);
                negSide = -side + hsA.Y;
                posSide = side + hsA.Y;
                negEdge = EdgeNumbers.EDGE3;
                posEdge = EdgeNumbers.EDGE1;
                ComputeIncidentEdge(incidentEdge, hsB, posB, rotB, frontNormal);
                break;
            }

            case Axis.FACE_A_Y:
            {
                frontNormal = normal;
                front = vec2.Dot(posA, frontNormal) + hsA.Y;
                sideNormal = rotA.Column1;
                float side = vec2.Dot(posA, sideNormal);
                negSide = -side + hsA.X;
                posSide = side + hsA.X;
                negEdge = EdgeNumbers.EDGE2;
                posEdge = EdgeNumbers.EDGE4;
                ComputeIncidentEdge(incidentEdge, hsB, posB, rotB, frontNormal);
                break;
            }

            case Axis.FACE_B_X:
            {
                frontNormal = -normal;
                front = vec2.Dot(posB, frontNormal) + hsB.X;
                sideNormal = rotB.Column2;
                float side = vec2.Dot(posB, sideNormal);
                negSide = -side + hsB.Y;
                posSide = side + hsB.Y;
                negEdge = EdgeNumbers.EDGE3;
                posEdge = EdgeNumbers.EDGE1;
                ComputeIncidentEdge(incidentEdge, hsA, posA, rotA, frontNormal);
                break;
            }

            case Axis.FACE_B_Y:
            {
                frontNormal = -normal;
                front = vec2.Dot(posB, frontNormal) + hsB.Y;
                sideNormal = rotB.Column1;
                float side = vec2.Dot(posB, sideNormal);
                negSide = -side + hsB.X;
                posSide = side + hsB.X;
                negEdge = EdgeNumbers.EDGE2;
                posEdge = EdgeNumbers.EDGE4;
                ComputeIncidentEdge(incidentEdge, hsA, posA, rotA, frontNormal);
                break;
            }

            default: throw new NotImplementedException();
        }

        // clip other face with 5 box planes (1 face plane, 4 edge planes)
        Span<ClipVertex> clipPoints1 = stackalloc ClipVertex[2];
        Span<ClipVertex> clipPoints2 = stackalloc ClipVertex[2];
        int np;

        // Clip to box side 1
        np = ClipSegmentToLine(clipPoints1, incidentEdge, -sideNormal, negSide, negEdge);
        if (np < 2)
            return 0;

        // Clip to negative box side 1
        np = ClipSegmentToLine(clipPoints2, clipPoints1, sideNormal, posSide, posEdge);
        if (np < 2)
            return 0;

        // Now clipPoints2 contains the clipping points.
        // Due to roundoff, it is possible that clipping removes all points.

        int numContacts = 0;
        for (int i = 0; i < 2; ++i)
        {
            float separation = vec2.Dot(frontNormal, clipPoints2[i].V) - front;

            if (separation <= 0)
            {
                contacts[numContacts].Normal = -normal;

                // slide contact point onto reference face (easy to cull)
                contacts[numContacts].RA = rotAT * (clipPoints2[i].V - frontNormal * separation - posA);
                contacts[numContacts].RB = rotBT * (clipPoints2[i].V - posB);
                contacts[numContacts].FP = clipPoints2[i].FP;

                if (axis == Axis.FACE_B_X || axis == Axis.FACE_B_Y)
                {
                    Flip(ref contacts[numContacts].FP);
                    contacts[numContacts].RA = rotAT * (clipPoints2[i].V - posA);
                    contacts[numContacts].RB = rotBT * (clipPoints2[i].V - frontNormal * separation - posB);
                }

                numContacts++;
            }
        }

        return numContacts;
    }

    private static void Flip(ref FeaturePair fp)
    {
        EdgeNumbers temp = fp.E.InEdge1;
        fp.E.InEdge1 = fp.E.InEdge2;
        fp.E.InEdge2 = temp;

        temp = fp.E.OutEdge1;
        fp.E.OutEdge1 = fp.E.OutEdge2;
        fp.E.OutEdge2 = temp;
    }

    private static void ComputeIncidentEdge(Span<ClipVertex> c, vec2 hs, vec2 pos, mat2 rot, vec2 normal)
    {
        // The normal is from the reference box. Convert it to the incident box's frame and flip sign.
        mat2 rotT = mat2.Transpose(rot);
        vec2 n = -(rotT * normal);
        vec2 nAbs = vec2.Abs(n);

        if (nAbs.X > nAbs.Y)
        {
            if (MathF.Sign(n.X) > 0.0f)
            {
                c[0].V = new vec2(hs.X, -hs.Y);

                c[0].FP.E.InEdge2 = EdgeNumbers.EDGE3;
                c[0].FP.E.OutEdge2 = EdgeNumbers.EDGE4;

                c[1].V = new vec2(hs.X, hs.Y);
                c[1].FP.E.InEdge2 = EdgeNumbers.EDGE4;
                c[1].FP.E.OutEdge2 = EdgeNumbers.EDGE1;
            }
            else
            {
                c[0].V = new vec2(-hs.X, hs.Y);
                c[0].FP.E.InEdge2 = EdgeNumbers.EDGE1;
                c[0].FP.E.OutEdge2 = EdgeNumbers.EDGE2;

                c[1].V = new vec2(-hs.X, -hs.Y);
                c[1].FP.E.InEdge2 = EdgeNumbers.EDGE2;
                c[1].FP.E.OutEdge2 = EdgeNumbers.EDGE3;
            }
        }
        else
        {
            if (MathF.Sign(n.Y) > 0.0f)
            {
                c[0].V = new vec2(hs.X, hs.Y);
                c[0].FP.E.InEdge2 = EdgeNumbers.EDGE4;
                c[0].FP.E.OutEdge2 = EdgeNumbers.EDGE1;

                c[1].V = new vec2(-hs.X, hs.Y);
                c[1].FP.E.InEdge2 = EdgeNumbers.EDGE1;
                c[1].FP.E.OutEdge2 = EdgeNumbers.EDGE2;
            }
            else
            {
                c[0].V = new vec2(-hs.X, -hs.Y);
                c[0].FP.E.InEdge2 = EdgeNumbers.EDGE2;
                c[0].FP.E.OutEdge2 = EdgeNumbers.EDGE3;

                c[1].V = new vec2(hs.X, -hs.Y);
                c[1].FP.E.InEdge2 = EdgeNumbers.EDGE3;
                c[1].FP.E.OutEdge2 = EdgeNumbers.EDGE4;
            }
        }

        c[0].V = pos + rot * c[0].V;
        c[1].V = pos + rot * c[1].V;
    }

    private static int ClipSegmentToLine(Span<ClipVertex> vOut,
                                         ReadOnlySpan<ClipVertex> vIn,
                                         vec2 normal,
                                         float offset,
                                         EdgeNumbers clipEdge)
    {
        // Start with no output points
        int numOut = 0;

        // Calculate the distance of end points to the line
        float distance0 = vec2.Dot(normal, vIn[0].V) - offset;
        float distance1 = vec2.Dot(normal, vIn[1].V) - offset;

        // If the points are behind the plane
        if (distance0 <= 0.0f) vOut[numOut++] = vIn[0];
        if (distance1 <= 0.0f) vOut[numOut++] = vIn[1];

        // If the points are on different sides of the plane
        if (distance0 * distance1 < 0.0f)
        {
            // Find intersection point of edge and plane
            float interp = distance0 / (distance0 - distance1);
            vOut[numOut].V = vIn[0].V + (vIn[1].V - vIn[0].V) * interp;
            if (distance0 > 0.0f)
            {
                vOut[numOut].FP = vIn[0].FP;
                vOut[numOut].FP.E.InEdge1 = clipEdge;
                vOut[numOut].FP.E.InEdge2 = EdgeNumbers.NO_EDGE;
            }
            else
            {
                vOut[numOut].FP = vIn[1].FP;
                vOut[numOut].FP.E.OutEdge1 = clipEdge;
                vOut[numOut].FP.E.OutEdge2 = EdgeNumbers.NO_EDGE;
            }
            numOut++;
        }

        return numOut;
    }
}