namespace Paw.Core.Physics;

public class Body
{
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

    public readonly List<Force> Forces = [];

    public Body(vec2 size, float density, float friction, vec3 position, vec3 velocity)
    {
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
}
