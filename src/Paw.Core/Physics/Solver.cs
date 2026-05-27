using Paw.Core.Utils;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Paw.Core.Physics;

public class Solver
{
    // config
    public float Dt;
    public vec3 Gravity;
    public int Iterations;

    public float Alpha;
    public float Beta;
    public float Gamma;

    public bool PostStabilize;

    // state
    public readonly List<Body> Bodies = [];
    public readonly List<Force> Forces = [];

    public Solver()
    {
        SetDefaultConfig();
        Reset();
    }

    public void SetDefaultConfig()
    {
        Dt = 1.0f / 60.0f;
        Gravity = new vec3(0f, -9.81f, 0f);
        Iterations = 10;

        // Note: in the paper, beta is suggested to be [1, 1000]. Technically, the best choice will
        // depend on the length, mass, and constraint function scales (ie units) of your simulation,
        // along with your strategy for incrementing the penalty parameters.
        // If the value is not in the right range, you may see slower convergance for complex scenes.
        Beta = 100000.0f;

        // Alpha controls how much stabilization is applied. Higher values give slower and smoother
        // error correction, and lower values are more responsive and energetic. Tune this depending
        // on your desired constraint error response.
        Alpha = 0.99f;

        // Gamma controls how much the penalty and lambda values are decayed each step during warmstarting.
        // This should always be < 1 so that the penalty values can decrease (unless you use a different
        // penalty parameter strategy which does not require decay).
        Gamma = 0.99f;

        // Post stabilization applies an extra iteration to fix positional error.
        // This removes the need for the alpha parameter, which can make tuning a little easier.
        PostStabilize = true;
    }

    public void Reset()
    {
        Bodies.Clear();
        Forces.Clear();

        Bodies.Add(new Body(
            size: new vec2(1, 1),
            density: 1.0f,
            friction: 0.5f,
            position: new Vector3(5, 5, 0),
            velocity: vec3.Zero
            ));

        Bodies.Add(new Body(
            size: new vec2(2, 1),
            density: 1.0f,
            friction: 0.5f,
            position: new Vector3(-5, 4, 15f.DegToRad()),
            velocity: vec3.Zero
            ));

        Bodies.Add(new Body(
            size: new vec2(20, 1),
            density: 0f,
            friction: 0.5f,
            position: new Vector3(0, -5f, 0),
            velocity: vec3.Zero
            ));
    }

    public bool Pick(vec2 worldPosition, [NotNullWhen(true)] out Body? pickedBody, out vec2 pickedBodyLocalPosition)
    {
        foreach (var body in Bodies)
        {
            mat2 invRot = mat2.Rotation(-body.Position.Z);
            vec2 localPosition = invRot * (worldPosition - body.Position.XY);
            vec2 halfSize = body.Size * 0.5f;

            if (-halfSize.X <= localPosition.X && localPosition.X <= halfSize.X &&
                -halfSize.Y <= localPosition.Y && localPosition.Y <= halfSize.Y)
            {
                pickedBody = body;
                pickedBodyLocalPosition = localPosition;
                return true;
            }
        }

        pickedBody = default;
        pickedBodyLocalPosition = default;
        return false;
    }

    private readonly List<Force> _forcesToDelete = [];

    public void Step()
    {
        // Perform broadphase collision detection
        // This is a naive O(n^2) approach, but it is sufficient for small numbers of bodies in this sample.
        for (int indexA = 0; indexA < Bodies.Count; indexA++)
        {
            Body bodyA = Bodies[indexA];
            for (int indexB = indexA + 1; indexB < Bodies.Count; indexB++)
            {
                Body bodyB = Bodies[indexB];
                vec2 dp = bodyA.Position.XY - bodyB.Position.XY;
                float r = bodyA.Radius + bodyB.Radius;

                if (vec2.Dot(dp, dp) < r * r && !bodyA.IsConstrainedTo(bodyB))
                    Forces.Add(new Manifold(bodyA, bodyB));
            }
        }

        // Initialize and warmstart forces
        foreach (var force in Forces)
        {
            // Initialization can including caching anything that is constant over the step
            if (!force.Initialize())
            {
                // Force has returned false meaning it is inactive, so remove it from the solver
                _forcesToDelete.Add(force);
            }
            else
            {
                // ...
            }
        }

        foreach (var force in _forcesToDelete)
        {
            bool r = Forces.Remove(force);
            Debug.Assert(r);
            force.RemoveFromBodies();
        }
        _forcesToDelete.Clear();

        // Initialize and warmstart bodies (ie primal variables)
        foreach (var body in Bodies)
        {
            // Don't let bodies rotate too fast
            body.Velocity.Z = body.Velocity.Z.Clamp(-50f, +50f);

            // Compute inertial position (Eq 2)
            body.Inertial = body.Position + body.Velocity * Dt;
            if (body.Mass > 0f)
                body.Inertial += Gravity * Dt * Dt;

            // Adaptive warmstart (See original VBD paper)
            vec3 accel = (body.Velocity - body.PrevVelocity) / Dt;
            vec3 accelExt = accel * Gravity.Signs;
            vec3 accelWeight = (accelExt / vec3.Abs(Gravity)).Clamp(0f, 1f);
            if (!float.IsFinite(accelWeight.X)) accelWeight.X = 0f;
            if (!float.IsFinite(accelWeight.Y)) accelWeight.Y = 0f;
            if (!float.IsFinite(accelWeight.Z)) accelWeight.Z = 0f;

            // Save initial position (x-) and compute warmstarted position (See original VBD paper)
            body.Initial = body.Position;

            body.Position = body.Mass > 0f
                ? body.Position + body.Velocity * Dt + Gravity * (accelWeight * Dt * Dt)
                : body.Position + body.Velocity * Dt;
        }

        // Main solver loop
        // If using post stabilization, we'll use one extra iteration for the stabilization
        int totalIterations = Iterations + (PostStabilize ? 1 : 0);

        for (int it = 0; it < totalIterations; it++)
        {
            // If using post stabilization, either remove all or none of the pre-existing constraint error
            float currentAlpha = Alpha;
            if (PostStabilize)
                currentAlpha = it < Iterations ? 1.0f : 0.0f;

            // Primal update
            foreach (var body in Bodies)
            {
                // Skip static / kinematic bodies
                if (body.Mass <= 0f)
                    continue;

                // Initialize left and right hand sides of the linear system (Eqs. 5, 6)
                mat3 M = mat3.Diagonal(body.Mass, body.Mass, body.Moment);
                mat3 lhs = M / (Dt * Dt);
                vec3 rhs = M / (Dt * Dt) * (body.Position - body.Inertial);

                // Iterate over all forces acting on the body
                // ...
                // ...
                // ...

                // Solve the SPD linear system using LDL and apply the update (Eq. 4)
                body.Position -= Solve(lhs, rhs);
            }

            // Dual update, only for non stabilized iterations in the case of post stabilization
            // If doing more than one post stabilization iteration, we can still do a dual update,
            // but make sure not to persist the penalty or lambda updates done during the stabilization iterations for the next frame.
            if (it < Iterations)
            {
                // ...
                // ...
                // ...
            }

            // If we are are the final iteration before post stabilization, compute velocities (BDF1)
            if (it == Iterations - 1)
            {
                foreach (var body in Bodies)
                {
                    body.PrevVelocity = body.Velocity;
                    if (body.Mass > 0f)
                        body.Velocity = (body.Position - body.Initial) / Dt;
                }
            }
        }
    }

    private static vec3 Solve(mat3 a, vec3 b) // For SPD (symmetric positive definite) LSE Ax=b, return x
    {
        const float epsilon = 1e-6f;

        // Must be symmetric
        Debug.Assert(MathF.Abs(a.M12 - a.M21) <= epsilon);
        Debug.Assert(MathF.Abs(a.M13 - a.M31) <= epsilon);
        Debug.Assert(MathF.Abs(a.M23 - a.M32) <= epsilon);

        // Inputs must be finite
        Debug.Assert(float.IsFinite(a.M11));
        Debug.Assert(float.IsFinite(a.M12));
        Debug.Assert(float.IsFinite(a.M13));
        Debug.Assert(float.IsFinite(a.M21));
        Debug.Assert(float.IsFinite(a.M22));
        Debug.Assert(float.IsFinite(a.M23));
        Debug.Assert(float.IsFinite(a.M31));
        Debug.Assert(float.IsFinite(a.M32));
        Debug.Assert(float.IsFinite(a.M33));
        Debug.Assert(float.IsFinite(b.X));
        Debug.Assert(float.IsFinite(b.Y));
        Debug.Assert(float.IsFinite(b.Z));

        // Basic sanity
        Debug.Assert(a.M11 > 0f);
        Debug.Assert(a.M22 > 0f);
        Debug.Assert(a.M33 > 0f);

        // Compute LDL^T decomposition
        float D1 = a.M11;
        float L21 = a.M21 / a.M11;
        float L31 = a.M31 / a.M11;
        float D2 = a.M22 - L21 * L21 * D1;
        float L32 = (a.M32 - L21 * L31 * D1) / D2;
        float D3 = a.M33 - (L31 * L31 * D1 + L32 * L32 * D2);

        // SPD requires positive pivots
        Debug.Assert(D1 > epsilon);
        Debug.Assert(D2 > epsilon);
        Debug.Assert(D3 > epsilon);

        // Forward substitution: Solve Ly = b
        float y1 = b.X;
        float y2 = b.Y - L21 * y1;
        float y3 = b.Z - L31 * y1 - L32 * y2;

        // Diagonal solve: Solve Dz = y
        float z1 = y1 / D1;
        float z2 = y2 / D2;
        float z3 = y3 / D3;

        // Backward substitution: Solve L^T x = z
        float x3 = z3;
        float x2 = z2 - L32 * x3;
        float x1 = z1 - L21 * x2 - L31 * x3;

        Debug.Assert(float.IsFinite(x1));
        Debug.Assert(float.IsFinite(x2));
        Debug.Assert(float.IsFinite(x3));

        return new vec3(x1, x2, x3);
    }
}
