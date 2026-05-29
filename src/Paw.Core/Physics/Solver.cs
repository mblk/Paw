using Paw.Core.Utils;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Paw.Core.Physics;

public static class SolverConfig
{
    public const float PENALTY_MIN = 1.0f;              // Minimum penalty parameter
    public const float PENALTY_MAX = 1000000000.0f;     // Maximum penalty parameter
    public const float COLLISION_MARGIN = 0.0005f;      // Margin for collision detection to avoid flickering contacts
    public const float STICK_THRESH = 0.01f;            // Position threshold for sticking contacts (ie static friction)
}

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

    // ...
    private readonly ObjectPool<Manifold> _manifoldPool = new();

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
            size: new vec2(2, 1),
            density: 1.0f,
            friction: 0.5f,
            position: new Vector3(-5, 4, 15f.DegToRad()),
            velocity: vec3.Zero
            ));

        // Ground
        Bodies.Add(new Body(
            size: new vec2(30, 1),
            density: 0f,
            friction: 0.5f,
            position: new Vector3(0, -5f, 0),
            velocity: vec3.Zero
            ));

        // Triangle
        {
            var bLeft = new Body(
                size: new vec2(1, 1),
                density: 1.0f,
                friction: 0.5f,
                position: new Vector3(5, 5, 0f),
                velocity: vec3.Zero
                );

            var bRight = new Body(
                size: new vec2(1, 1),
                density: 1.0f,
                friction: 0.5f,
                position: new Vector3(10, 5, 0f),
                velocity: vec3.Zero
                );

            var bTop = new Body(
                size: new vec2(1, 1),
                density: 1.0f,
                friction: 0.5f,
                position: new Vector3(7.5f, 10f, 0f),
                velocity: vec3.Zero
                );


            void AddJointInMiddle(Body a, Body b)
            {
                var jStiffness = new vec3(100f, 100f, 0f);
                //var jStiffness = new vec3(float.PositiveInfinity, float.PositiveInfinity, 0f);
                var jFracture = float.PositiveInfinity;

                vec2 vAB = b.Position.XY - a.Position.XY;

                vec2 rA = vAB * 0.5f;
                vec2 rB = -(vAB * 0.5f);

                var j = new Joint(a, b, rA, rB, jStiffness, jFracture);

                Forces.Add(j);
            }

            AddJointInMiddle(bLeft, bRight);
            AddJointInMiddle(bLeft, bTop);
            AddJointInMiddle(bTop, bRight);

            Bodies.Add(bLeft);
            Bodies.Add(bRight);
            Bodies.Add(bTop);
        }
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
                {
                    var manifold = _manifoldPool.Get();
                    manifold.AddToBodies(bodyA, bodyB);
                    Forces.Add(manifold);

                    //Forces.Add(new Manifold(bodyA, bodyB));
                }
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
                for (int i = 0; i < force.Rows; i++)
                {
                    if (PostStabilize)
                    {
                        // With post stabilization, we can reuse the full lambda from the previous step,
                        // and only need to reduce the penalty parameters
                        force.Penalty[i] = (force.Penalty[i] * Gamma).Clamp(SolverConfig.PENALTY_MIN, SolverConfig.PENALTY_MAX);
                    }
                    else
                    {
                        // Warmstart the dual variables and penalty parameters (Eq. 19)
                        // Penalty is safely clamped to a minimum and maximum value
                        force.Lambda[i] = force.Lambda[i] * Alpha * Gamma;
                        force.Penalty[i] = (force.Penalty[i] * Gamma).Clamp(SolverConfig.PENALTY_MIN, SolverConfig.PENALTY_MAX);
                    }

                    // If it's not a hard constraint, we don't let the penalty exceed the material stiffness
                    force.Penalty[i] = MathF.Min(force.Penalty[i], force.Stiffness[i]);
                }
            }
        }

        foreach (var force in _forcesToDelete)
        {
            bool r = Forces.Remove(force);
            Debug.Assert(r);

            force.RemoveFromBodies();

            if (force is Manifold manifold)
                _manifoldPool.Return(manifold);
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
                foreach (var force in body.Forces)
                {
                    // Compute constraint and its derivatives
                    force.ComputeConstraint(currentAlpha);
                    force.ComputeDerivatives(body);

                    for (int i = 0; i < force.Rows; i++)
                    {
                        // Use lambda as 0 if it's not a hard constraint
                        float lambda = float.IsInfinity(force.Stiffness[i]) ? force.Lambda[i] : 0.0f;

                        // Compute the clamped force magnitude (Sec 3.2)
                        float f = (force.Penalty[i] * force.C[i] + lambda).Clamp(force.fMin[i], force.fMax[i]);

                        // Compute the diagonally lumped geometric stiffness term (Sec 3.5)
                        mat3 G = mat3.Diagonal(force.H[i].Column1.Length(),
                                               force.H[i].Column2.Length(),
                                               force.H[i].Column3.Length()) * MathF.Abs(f);

                        // Accumulate force (Eq. 13) and hessian (Eq. 17)
                        {
                            var aa = force.J[i] * f;

                            if (!float.IsFinite(aa.X) || !float.IsFinite(aa.Y) || !float.IsFinite(aa.Z))
                            {
                                Console.WriteLine();
                            }
                        }

                        rhs += force.J[i] * f;
                        lhs += mat3.Outer(force.J[i], force.J[i] * force.Penalty[i]) + G;
                    }
                }

                // Solve the SPD linear system using LDL and apply the update (Eq. 4)
                body.Position -= Solve(lhs, rhs);
            }

            // Dual update, only for non stabilized iterations in the case of post stabilization
            // If doing more than one post stabilization iteration, we can still do a dual update,
            // but make sure not to persist the penalty or lambda updates done during the stabilization iterations for the next frame.
            if (it < Iterations)
            {
                foreach (var force in Forces)
                {
                    // Compute constraint
                    force.ComputeConstraint(currentAlpha);

                    for (int i = 0; i < force.Rows; i++)
                    {
                        // Use lambda as 0 if it's not a hard constraint
                        float lambda = float.IsInfinity(force.Stiffness[i]) ? force.Lambda[i] : 0.0f;

                        // Update lambda (Eq 11)
                        force.Lambda[i] = (force.Penalty[i] * force.C[i] + lambda).Clamp(force.fMin[i], force.fMax[i]);

                        // Disable the force if it has exceeded its fracture threshold
                        //if ( MathF.Abs(force.Lambda[i]) >= force.Fracture[i])
                        //    force.Disable();

                        // Update the penalty parameter and clamp to material stiffness if we are within the force bounds (Eq. 16)
                        if (force.fMin[i] < force.Lambda[i] && force.Lambda[i] < force.fMax[i])
                            force.Penalty[i] = MathF.Min(force.Penalty[i] + Beta * MathF.Abs(force.C[i]),
                                                         MathF.Min(SolverConfig.PENALTY_MAX,
                                                                   force.Stiffness[i]));
                    }
                }
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
        //Debug.Assert(MathF.Abs(a.M12 - a.M21) <= epsilon);
        //Debug.Assert(MathF.Abs(a.M13 - a.M31) <= epsilon);
        //Debug.Assert(MathF.Abs(a.M23 - a.M32) <= epsilon);

        //if (a.M12 != 0f)
        //    Debug.Assert(MathF.Abs((a.M12 - a.M21) / a.M12) <= 0.01f);
        //if (a.M13 != 0f)
        //    Debug.Assert(MathF.Abs((a.M13 - a.M31) / a.M13) <= 0.01f);
        //if (a.M23 != 0f)
        //    Debug.Assert(MathF.Abs((a.M23 - a.M32) / a.M23) <= 0.01f);

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
