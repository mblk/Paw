using Paw.Core.Utils;

namespace Paw.Core.Physics;

public class Body
{
    private static int _nextId = 1;

    public int Id;

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

    public readonly List<Force> Forces = [];

    public Body(vec2 size, float density, float friction, vec3 position, vec3 velocity)
    {
        Id = _nextId++;

        Position = position;
        Velocity = velocity;
        PrevVelocity = velocity;
        Size = size;
        Friction = friction;

        Mass = size.X * size.Y * density;
        Moment = Mass * vec2.Dot(size, size) / 12.0f;
        Radius = (size * 0.5f).Length();
    }

    public bool IsConstrainedTo(Body otherBody)
    {
        foreach (var force in Forces)
        {
            if (force.BodyA == this && force.BodyB == otherBody ||
                force.BodyA == otherBody && force.BodyB == this)
            {
                return true;
            }
        }

        return false;
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
