using Paw.Core.Utils;
using System.Collections;
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

public readonly record struct BodyRef(uint Index, uint Gen);


public class Solver
{
    public struct BodyEnumerator : IEnumerator<BodyRef>
    {
        private readonly Solver _solver;
        private uint _index;

        public BodyEnumerator(Solver solver)
        {
            _solver = solver;
            Reset();
        }

        public BodyRef Current { get; private set; }

        object IEnumerator.Current => Current;

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            if (_index == uint.MaxValue)
            {
                _index = 0;
            }

            while (_index < _solver._bodies.Length)
            {
                uint currentIndex = _index++;

                ref Body body = ref _solver._bodies[currentIndex];

                if (body.Used)
                {
                    Current = new BodyRef(currentIndex, body.Gen);
                    return true;
                }
            }

            return false;
        }

        public void Reset()
        {
            _index = uint.MaxValue;
        }
    }

    public readonly struct BodyEnumerable : IEnumerable<BodyRef>
    {
        private readonly Solver _solver;

        public BodyEnumerable(Solver solver)
        {
            _solver = solver;
        }

        public IEnumerator<BodyRef> GetEnumerator()
        {
            return new BodyEnumerator(_solver);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }






    // config
    public float Dt;
    public vec3 Gravity;
    public int Iterations;

    public float Alpha;
    public float Beta;
    public float Gamma;

    public bool PostStabilize;

    // state
    private Body[] _bodies = new Body[100];

    public readonly List<Force> Forces = [];

    private readonly Dictionary<BodyRef, List<Force>> _bodyForces = []; // temporary

    private readonly List<Force> _forcesToDelete = [];



    public IEnumerable<BodyRef> AliveBodies => new BodyEnumerable(this);


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
        for (int i = 0; i < _bodies.Length; i++)
        {
            ref Body b = ref _bodies[i];

            b.Gen = 1; // Start at 1 - default(Body) must be invalid
            b.Used = false;
        }

        //Bodies.Clear();
        Forces.Clear();

        AddBody(new Vector3(-5, 4, 15f.DegToRad()), new vec2(2, 1));

        // Ground
        {
            AddBody(new Vector3(0, -5f, 0), new vec2(30, 1), density: 0f);
            AddBody(new Vector3(-16.75f, -3.7f, -30f.DegToRad()), new vec2(5, 1), density: 0f);
            AddBody(new Vector3(-20f, -0.5f, -60f.DegToRad()), new vec2(5, 1), density: 0f);
            AddBody(new Vector3(16.75f, -3.7f, 30f.DegToRad()), new vec2(5, 1), density: 0f);
            AddBody(new Vector3(20f, -0.5f, 60f.DegToRad()), new vec2(5, 1), density: 0f);
        }

        // Triangle
        {
            var bLeft = AddBody(new vec3(5, 5, 0f), new vec2(1, 1));
            var bRight = AddBody(new vec3(10, 5, 0f), new vec2(1, 1));
            var bTop = AddBody(new vec3(7.5f, 10f, 0f), new vec2(1, 1));

            AddStiffAutoJoint(bLeft, bRight);
            AddStiffAutoJoint(bRight, bTop);
            AddStiffAutoJoint(bTop, bLeft);
        }

        // Spring test
        {
            var bAnchor = AddBody(new vec3(15f, 15f, 0f), new vec2(1, 1), density: 0f);
            var bFree = AddBody(new vec3(20f, 15f, 0f), new vec2(1, 1));

            AddAutoSpring(bAnchor, bFree);
        }

        // Motor test
        {
            var bAnchor = AddBody(new vec3(-15f, 15f, 0f), new vec2(1, 1), density: 0f);
            var bFree = AddBody(new vec3(-10f, 15f, 0f), new vec2(3, 1));

            AddJoint(bAnchor, bFree, new vec2(5f, 0f), new vec2(0, 0), new vec3(1000f, 1000f, 0f));
            AddMotor(bAnchor, bFree, 90f.DegToRad(), 100f);
        }
    }

    public BodyRef AddBody(Vector3 position, Vector2 size, Vector3 velocity = default, float density = 1.0f, float friction = 0.5f)
    {
        // find free slot
        uint index = GetFreeBodyIndex();

        ref Body body = ref _bodies[index];

        body.Used = true;
        body.Gen++;

        body.Setup(size: size,
                               density: density,
                               friction: friction,
                               position: position,
                               velocity: velocity);

        var bodyRef = new BodyRef(index, body.Gen);

        return bodyRef;
    }

    private uint GetFreeBodyIndex()
    {
        for (uint i = 0; i < _bodies.Length; i++)
        {
            if (!_bodies[i].Used)
            {
                return i;
            }
        }

        throw new Exception("no more free bodies");
    }

    public void RemoveBody(BodyRef bodyRef)
    {
        //if (!Bodies.Contains(body))
        //    throw new ArgumentException("Unknown body");

        //Bodies.Remove(body);

        //_forcesToDelete.Clear();
        //_forcesToDelete.AddRange(body.Forces);

        //foreach (var force in _forcesToDelete)
        //{
        //    force.RemoveFromBodies();
        //    Forces.Remove(force);
        //}

        //_forcesToDelete.Clear();

        throw new NotImplementedException();
    }

    public Body GetCopyOfBody(BodyRef bodyRef)
    {
        if (!Exists(bodyRef))
            throw new Exception("body does not exist");

        return _bodies[bodyRef.Index];
    }

    public float GetMass(BodyRef bodyRef)
    {
        if (!Exists(bodyRef))
            throw new Exception("body does not exist");

        return _bodies[bodyRef.Index].Mass;
    }

    public void AddForceLocal(BodyRef bodyRef, vec2 localForce)
    {
        if (!Exists(bodyRef))
            throw new Exception("body does not exist");

        ref Body body = ref _bodies[bodyRef.Index];

        body.AddForceLocal(localForce);
    }

    public Joint AddJoint(BodyRef bodyRefA, BodyRef bodyRefB, vec2 rA, vec2 rB, vec3 stiffness)
    {
        var newJoint = new Joint(bodyRefA, bodyRefB, rA, rB, stiffness);

        bool hasBodyA = bodyRefA != default;
        bool hasBodyB = bodyRefB != default;
        ref Body bodyA = ref _bodies[bodyRefA.Index];
        ref Body bodyB = ref _bodies[bodyRefB.Index];
        newJoint.OneTimeInit(hasBodyA, hasBodyB, bodyA, bodyB);

        AddForceToBody(bodyRefA, newJoint);
        AddForceToBody(bodyRefB, newJoint);
        Forces.Add(newJoint);

        return newJoint;
    }

    public Joint AddStiffAutoJoint(BodyRef bodyRefA, BodyRef bodyRefB)
    {
        if (!Exists(bodyRefA) || !Exists(bodyRefB))
            throw new Exception("Body does not exist");

        ref Body bodyA = ref _bodies[bodyRefA.Index];
        ref Body bodyB = ref _bodies[bodyRefB.Index];

        vec2 pA = bodyA.Position.XY;
        vec2 pB = bodyB.Position.XY;
        vec2 vAB = pB - pA; // world space

        vec2 rA = Transform2D.WorldToLocal(bodyA.Position, pA + vAB * 0.5f); // local
        vec2 rB = Transform2D.WorldToLocal(bodyB.Position, pB - vAB * 0.5f); // local

        vec3 stiffness = new vec3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

        return AddJoint(bodyRefA, bodyRefB, rA, rB, stiffness);
    }

    public Joint AddWeakAutoJoint(BodyRef bodyRefA, BodyRef bodyRefB)
    {
        if (!Exists(bodyRefA) || !Exists(bodyRefB))
            throw new Exception("Body does not exist");

        ref Body bodyA = ref _bodies[bodyRefA.Index];
        ref Body bodyB = ref _bodies[bodyRefB.Index];

        vec2 pA = bodyA.Position.XY;
        vec2 pB = bodyB.Position.XY;
        vec2 vAB = pB - pA; // world space

        vec2 rA = Transform2D.WorldToLocal(bodyA.Position, pA + vAB * 0.5f); // local
        vec2 rB = Transform2D.WorldToLocal(bodyB.Position, pB - vAB * 0.5f); // local

        vec3 stiffness = new vec3(1000f, 1000f, 0f);

        return AddJoint(bodyRefA, bodyRefB, rA, rB, stiffness);
    }

    public Spring AddSpring(BodyRef bodyRefA, BodyRef bodyRefB, vec2 rA, vec2 rB, float stiffness)
    {
        var newSpring = new Spring(bodyRefA, bodyRefB, rA, rB, stiffness);

        bool hasBodyA = bodyRefA != default;
        bool hasBodyB = bodyRefB != default;
        ref Body bodyA = ref _bodies[bodyRefA.Index];
        ref Body bodyB = ref _bodies[bodyRefB.Index];
        newSpring.OneTimeInit(hasBodyA, hasBodyB, bodyA, bodyB);

        AddForceToBody(bodyRefA, newSpring);
        AddForceToBody(bodyRefB, newSpring);
        Forces.Add(newSpring);

        return newSpring;
    }

    public Spring AddAutoSpring(BodyRef bodyRefA, BodyRef bodyRefB)
    {
        vec2 rA = default; // attach at center
        vec2 rB = default;

        float stiffness = 10f;

        return AddSpring(bodyRefA, bodyRefB, rA, rB, stiffness);
    }

    public Motor AddMotor(BodyRef bodyRefA, BodyRef bodyRefB, float targetSpeed, float maxTorque)
    {
        var newMotor = new Motor(bodyRefA, bodyRefB, targetSpeed, maxTorque);

        bool hasBodyA = bodyRefA != default;
        bool hasBodyB = bodyRefB != default;
        ref Body bodyA = ref _bodies[bodyRefA.Index];
        ref Body bodyB = ref _bodies[bodyRefB.Index];
        newMotor.OneTimeInit(hasBodyA, hasBodyB, bodyA, bodyB);

        AddForceToBody(bodyRefA, newMotor);
        AddForceToBody(bodyRefB, newMotor);
        Forces.Add(newMotor);

        return newMotor;
    }

    public void RemoveForce(Force force)
    {
        if (!Forces.Contains(force))
            throw new ArgumentException("Unknown force");

        //force.RemoveFromBodies();

        if (force.BodyA != default)
            RemoveForceFromBody(force.BodyA, force);
        if (force.BodyB != default)
            RemoveForceFromBody(force.BodyB, force);

        Forces.Remove(force);
    }

    public bool Pick(vec2 worldPosition, [NotNullWhen(true)] out BodyRef pickedBodyRef, out vec2 pickedBodyLocalPosition)
    {
        //foreach (var body in Bodies)
        for (uint i = 0; i < _bodies.Length; i++)
        {
            ref Body body = ref _bodies[i];
            if (!body.Used) continue;

            mat2 invRot = mat2.Rotation(-body.Position.Z);
            vec2 localPosition = invRot * (worldPosition - body.Position.XY);
            vec2 halfSize = body.Size * 0.5f;

            if (-halfSize.X <= localPosition.X && localPosition.X <= halfSize.X &&
                -halfSize.Y <= localPosition.Y && localPosition.Y <= halfSize.Y)
            {
                pickedBodyRef = new BodyRef(i, body.Gen);
                pickedBodyLocalPosition = localPosition;
                return true;
            }
        }

        pickedBodyRef = default;
        pickedBodyLocalPosition = default;
        return false;
    }



    public ref Body TryGetBody(BodyRef bodyRef)
    {
        ref Body body = ref _bodies[bodyRef.Index];

        // ?


        return ref body;
    }

    public bool Exists(BodyRef bodyRef)
    {
        if (bodyRef == default)
            return false;

        if (bodyRef.Index >= _bodies.Length)
            return false;

        ref Body body = ref _bodies[bodyRef.Index];

        if (!body.Used)
            return false;

        if (body.Gen != bodyRef.Gen)
            return false;

        return true;
    }

    public bool IsConstrainedTo(BodyRef bodyA, BodyRef bodyB)
    {
        if (!Exists(bodyA) || !Exists(bodyB))
            return false;

        if (!_bodyForces.TryGetValue(bodyA, out List<Force>? forcesA))
            return false;

        foreach (Force force in forcesA)
        {
            if (force.BodyA == bodyA && force.BodyB == bodyB ||
                force.BodyA == bodyB && force.BodyB == bodyA)
            {
                return true;
            }
        }

        return false;
    }

    public void AddForceToBody(BodyRef bodyRef, Force force)
    {
        if (bodyRef == default)
            return;

        if (!Exists(bodyRef))
            return;

        if (!_bodyForces.TryGetValue(bodyRef, out List<Force>? forces))
        {
            _bodyForces.Add(bodyRef, forces = new List<Force>());
        }

        forces.Add(force);
    }

    public void RemoveForceFromBody(BodyRef bodyRef, Force force)
    {
        if (!_bodyForces.TryGetValue(bodyRef, out List<Force>? forces))
            return;

        forces.Remove(force);
    }


    public void Step()
    {
        // Perform broadphase collision detection
        // This is a naive O(n^2) approach, but it is sufficient for small numbers of bodies in this sample.
        for (uint indexA = 0; indexA < _bodies.Length; indexA++)
        {
            Body bodyA = _bodies[indexA];
            if (!bodyA.Used) continue;

            var bodyRefA = new BodyRef(indexA, bodyA.Gen);

            for (uint indexB = indexA + 1; indexB < _bodies.Length; indexB++)
            {
                Body bodyB = _bodies[indexB];
                if (!bodyB.Used) continue;

                var bodyRefB = new BodyRef(indexB, bodyB.Gen);

                vec2 dp = bodyA.Position.XY - bodyB.Position.XY;
                float r = bodyA.Radius + bodyB.Radius;

                if (vec2.Dot(dp, dp) < r * r && !IsConstrainedTo(bodyRefA, bodyRefB))
                {
                    var manifold = new Manifold(bodyRefA, bodyRefB);

                    // manifold.OneTimeInit ...

                    //manifold.AddToBodies(bodyA, bodyB);
                    AddForceToBody(bodyRefA, manifold);
                    AddForceToBody(bodyRefB, manifold);

                    Forces.Add(manifold);
                }
            }
        }

        // Initialize and warmstart forces
        _forcesToDelete.Clear();
        foreach (var force in Forces)
        {
            bool hasBodyA = Exists(force.BodyA);
            bool hasBodyB = Exists(force.BodyB);

            ref Body bodyA = ref _bodies[force.BodyA.Index];
            ref Body bodyB = ref _bodies[force.BodyB.Index];

            // Initialization can including caching anything that is constant over the step
            if (!force.PerTickInit(hasBodyA, hasBodyB, bodyA, bodyB))
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

                    force.Penalty[i].VerifyFinite();
                    force.Lambda[i].VerifyFinite();
                }
            }
        }

        foreach (var force in _forcesToDelete)
        {
            bool r = Forces.Remove(force);
            Debug.Assert(r);

            //force.RemoveFromBodies();

            if (force.BodyA != default)
            {
                RemoveForceFromBody(force.BodyA, force);
            }
            if (force.BodyB != default)
            {
                RemoveForceFromBody(force.BodyB, force);
            }
        }
        _forcesToDelete.Clear();

        // Initialize and warmstart bodies (ie primal variables)
        //foreach (var body in Bodies)
        for (uint bodyIndex = 0; bodyIndex < _bodies.Length; bodyIndex++)
        {
            ref Body body = ref _bodies[bodyIndex];
            if (!body.Used) continue;

            // Don't let bodies rotate too fast
            body.Velocity.Z = body.Velocity.Z.Clamp(-50f, +50f);

            // Apply external force
            vec3 externalAccel = body.Mass > 0f
                ? Gravity + body.ExternalForce / new vec3(body.Mass, body.Mass, body.Moment)
                : default;
            body.ExternalForce = default;

            // Compute inertial position (Eq 2)
            body.Inertial = body.Position + body.Velocity * Dt;
            if (body.Mass > 0f)
            {
                body.Inertial += externalAccel * Dt * Dt;
                //body.Inertial += Gravity * Dt * Dt;
            }

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
            //foreach (var body in Bodies)
            for (uint bodyIndex = 0; bodyIndex < _bodies.Length; bodyIndex++)
            {
                ref Body body = ref _bodies[bodyIndex];
                if (!body.Used) continue;

                var bodyRef = new BodyRef(bodyIndex, body.Gen);

                // Skip static / kinematic bodies
                if (body.Mass <= 0f)
                    continue;

                // Initialize left and right hand sides of the linear system (Eqs. 5, 6)
                mat3 M = mat3.Diagonal(body.Mass, body.Mass, body.Moment);
                mat3 lhs = M / (Dt * Dt);
                vec3 rhs = M / (Dt * Dt) * (body.Position - body.Inertial);

                // Iterate over all forces acting on the body
                //foreach (var force in body.Forces)

                if (!_bodyForces.TryGetValue(new BodyRef(bodyIndex, body.Gen), out List<Force>? forces))
                {
                    forces = [];
                }

                foreach (var force in forces)
                {
                    bool hasBodyA = Exists(force.BodyA);
                    bool hasBodyB = Exists(force.BodyB);

                    ref Body bodyA = ref _bodies[force.BodyA.Index];
                    ref Body bodyB = ref _bodies[force.BodyB.Index];

                    bool isBodyA = bodyRef == force.BodyA;

                    // Compute constraint and its derivatives
                    force.ComputeConstraint(hasBodyA, hasBodyB, bodyA, bodyB, currentAlpha);
                    force.ComputeDerivatives(isBodyA, bodyA, bodyB);

                    for (int i = 0; i < force.Rows; i++)
                    {
                        // Use lambda as 0 if it's not a hard constraint
                        float lambda = float.IsInfinity(force.Stiffness[i]) ? force.Lambda[i] : 0.0f;

                        // Compute the clamped force magnitude (Sec 3.2)
                        float f = (force.Penalty[i] * force.C[i] + lambda).Clamp(force.fMin[i], force.fMax[i]);
                        f.VerifyFinite();

                        // Compute the diagonally lumped geometric stiffness term (Sec 3.5)
                        mat3 G = mat3.Diagonal(force.H[i].Column1.Length(),
                                               force.H[i].Column2.Length(),
                                               force.H[i].Column3.Length()) * MathF.Abs(f);

                        // Accumulate force (Eq. 13) and hessian (Eq. 17)
                        rhs += force.J[i] * f;
                        lhs += mat3.Outer(force.J[i], force.J[i] * force.Penalty[i]) + G;
                    }
                }

                // Solve the SPD linear system using LDL and apply the update (Eq. 4)
                body.Position -= Solve(lhs, rhs);
                body.Position.VerifyFinite();
            }

            // Dual update, only for non stabilized iterations in the case of post stabilization
            // If doing more than one post stabilization iteration, we can still do a dual update,
            // but make sure not to persist the penalty or lambda updates done during the stabilization iterations for the next frame.
            if (it < Iterations)
            {
                foreach (var force in Forces)
                {
                    bool hasBodyA = Exists(force.BodyA);
                    bool hasBodyB = Exists(force.BodyB);

                    ref Body bodyA = ref _bodies[force.BodyA.Index];
                    ref Body bodyB = ref _bodies[force.BodyB.Index];

                    // Compute constraint
                    force.ComputeConstraint(hasBodyA, hasBodyB, bodyA, bodyB, currentAlpha);

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
                //foreach (var body in Bodies)
                for (uint bodyIndex = 0; bodyIndex < _bodies.Length; bodyIndex++)
                {
                    ref Body body = ref _bodies[bodyIndex];
                    if (!body.Used) continue;

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

        // Inputs must be finite
        a.Column1.VerifyFinite();
        a.Column2.VerifyFinite();
        a.Column3.VerifyFinite();
        b.VerifyFinite();

        // A must be symmetric
        //Debug.Assert(NearlyEqualSymmetric(a.M12, a.M21));
        //Debug.Assert(NearlyEqualSymmetric(a.M13, a.M31));
        //Debug.Assert(NearlyEqualSymmetric(a.M23, a.M32));

        // Symmetrize noise
        float M21 = 0.5f * (a.M12 + a.M21);
        float M31 = 0.5f * (a.M13 + a.M31);
        float M32 = 0.5f * (a.M23 + a.M32);

        // Basic sanity
        float M11 = a.M11;
        float M22 = a.M22;
        float M33 = a.M33;
        Debug.Assert(M11 > 0f);
        Debug.Assert(M22 > 0f);
        Debug.Assert(M33 > 0f);

        // Compute LDL^T decomposition
        float D1 = M11;
        float L21 = M21 / M11;
        float L31 = M31 / M11;
        float D2 = M22 - L21 * L21 * D1;
        float L32 = (M32 - L21 * L31 * D1) / D2;
        float D3 = M33 - (L31 * L31 * D1 + L32 * L32 * D2);

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

        vec3 x = new vec3(x1, x2, x3);
        x.VerifyFinite();
        return x;
    }

    private static bool NearlyEqualSymmetric(float x, float y)
    {
        const float relTol = 0.25f; // Allow 25% as the error can get quite high

        float diff = MathF.Abs(x - y);
        float scale = MathF.Max(MathF.Abs(x), MathF.Abs(y));

        return diff <= relTol * scale;
    }
}
