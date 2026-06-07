using Paw.Core.Utils;

namespace Paw.Core.Physics;

public struct Body
{
    public uint Gen;
    public bool Used;

    public vec3 Position;       // x,y,angle
    public vec3 Initial;
    public vec3 Inertial;       // Inertial Position
    public vec3 Velocity;
    public vec3 PrevVelocity;
    public vec2 Size;
    public float Mass;
    public float Moment;
    public float Friction;
    public float Radius;

    public vec3 ExternalForce;  // N,N,Nm

    //public List<Force> Forces = [];

    public Body()
    {
    }

    public void Setup(vec2 size, float density, float friction, vec3 position, vec3 velocity)
    {
        Position = position;
        Velocity = velocity;
        PrevVelocity = velocity;
        Size = size;
        Friction = friction;

        Mass = size.X * size.Y * density;
        Moment = Mass * vec2.Dot(size, size) / 12.0f;
        Radius = (size * 0.5f).Length();

        //Forces = [];
    }

    //public bool IsConstrainedTo(Body otherBody)
    //{
    //    foreach (var force in Forces)
    //    {
    //        if (force.BodyA.HasValue && force.BodyA.Value.Id == this.Id && force.BodyB.HasValue && force.BodyB.Value.Id == otherBody.Id ||
    //            force.BodyA.HasValue && force.BodyA.Value.Id == otherBody.Id && force.BodyB.HasValue && force.BodyB.Value.Id == this.Id)
    //        {
    //            return true;
    //        }
    //    }

    //    return false;
    //}

    public vec2 LocalToWorld(vec2 local)
    {
        return Transform2D.LocalToWorld(Position, local);
    }

    public vec2 WorldToLocal(vec2 world)
    {
        return Transform2D.WorldToLocal(Position, world);
    }

    public void AddForceWorld(vec2 worldForce)
    {
        ExternalForce += new vec3(worldForce, 0f);
    }

    public void AddForceLocal(vec2 localForce)
    {
        vec2 worldForce = Transform2D.Rotate(Position.Z, localForce);
        AddForceWorld(worldForce);
    }

    public void AddForceAtWorldPoint(vec2 force, vec2 worldPoint)
    {
        vec2 r = worldPoint - Position.XY;
        float torque = vec2.Cross(r, force);
        ExternalForce += new vec3(force, torque);
    }

    public void AddForceAtLocalPoint(vec2 localForce, vec2 localPoint)
    {
        vec2 worldForce = Transform2D.Rotate(Position.Z, localForce);
        vec2 worldPoint = Transform2D.LocalToWorld(Position, localPoint);
        AddForceAtWorldPoint(worldForce, worldPoint);
    }
}
