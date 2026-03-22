using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Paw.Core.Engine;

public unsafe class VertexArrayObject<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T> 
    : IDisposable
    where T : unmanaged
{
    private readonly GL _gl;
    private readonly BufferObject _vertexBufferObject;

    public GL.VertexArrayId Id { get; }

    public VertexArrayObject(GL gl, BufferObject vertexBufferObject, string? label = null)
    {
        _gl = gl;
        _vertexBufferObject = vertexBufferObject;

        Id = CreateArrayAndConfigureAttributes(gl);

        if (!String.IsNullOrWhiteSpace(label))
        {
            gl.Label(GL.ObjectIdentifier.VERTEX_ARRAY, Id.Id, label);
        }
    }

    private GL.VertexArrayId CreateArrayAndConfigureAttributes(GL gl)
    {
        Console.WriteLine($"VertexArrayObject:");
        Console.WriteLine($"  Type: {typeof(T).FullName}");

        //int stride = Marshal.SizeOf<T>();
        //int stride = Unsafe.SizeOf<T>();
        int stride = sizeof(T);
        Console.WriteLine($"  Stride: {stride}");

        // Create VAO
        GL.VertexArrayId vaoId = default;
        gl.CreateVertexArrays(1, &vaoId);

        // Attach VBO to VAO at binding slot 0
        gl.VertexArrayVertexBuffer(vaoId, 0, _vertexBufferObject.Id, 0, stride);

        // configure attributes
        var fields = typeof(T)
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(fi => new
            {
                Field = fi,
                Offset = (uint)Marshal.OffsetOf<T>(fi.Name),
            })
            .OrderBy(x => x.Offset)
            .Select((x, index) => new
            {
                x.Field,
                x.Offset,
                Index = (uint)index,
            })
            .ToArray();

        foreach (var field in fields)
        {
            gl.EnableVertexArrayAttrib(vaoId, field.Index);

            var attribInfo = GetAttribInfo(field.Field.FieldType);
            Console.WriteLine($"  Attrib {field.Index}: '{field.Field.Name}' offset={field.Offset} {attribInfo}");

            switch (attribInfo)
            {
                case AttribInfo info:
                    gl.VertexArrayAttribFormat(vaoId, field.Index, info.Components, info.Type, info.Normalized, field.Offset);
                    gl.VertexArrayAttribBinding(vaoId, field.Index, 0);
                    break;
                case IAttribInfo info:
                    gl.VertexArrayAttribIFormat(vaoId, field.Index, info.Components, info.Type, field.Offset);
                    gl.VertexArrayAttribBinding(vaoId, field.Index, 0);
                    break;
                case LAttribInfo info:
                    gl.VertexArrayAttribLFormat(vaoId, field.Index, info.Components, info.Type, field.Offset);
                    gl.VertexArrayAttribBinding(vaoId, field.Index, 0);
                    break;

                default: throw new NotImplementedException();
            }
        }

        return vaoId;
    }

    public void Bind()
    {
        _gl.BindVertexArray(Id);
    }

    public void Unbind()
    {
        _gl.BindVertexArray(default);
    }

    public void Draw(GL.PrimitiveType mode, int first, int count)
    {
        _gl.DrawArrays(mode, first, count);
    }

    private abstract record CommonAttribInfo(int Components);
    private record AttribInfo(int Components, GL.VertexAttribType Type, bool Normalized) : CommonAttribInfo(Components);
    private record IAttribInfo(int Components, GL.VertexAttribIType Type) : CommonAttribInfo(Components);
    private record LAttribInfo(int Components, GL.VertexAttribLType Type) : CommonAttribInfo(Components);

    private static CommonAttribInfo GetAttribInfo(Type type)
    {
        // Floats
        if (type == typeof(float)) return new AttribInfo(1, GL.VertexAttribType.FLOAT, false);
        if (type == typeof(Vector2)) return new AttribInfo(2, GL.VertexAttribType.FLOAT, false);
        if (type == typeof(Vector3)) return new AttribInfo(3, GL.VertexAttribType.FLOAT, false);
        if (type == typeof(Vector4)) return new AttribInfo(4, GL.VertexAttribType.FLOAT, false);

        if (type == typeof(sbyte)) return new AttribInfo(1, GL.VertexAttribType.BYTE, true);
        if (type == typeof(byte)) return new AttribInfo(1, GL.VertexAttribType.UNSIGNED_BYTE, true);

        // Integers
        if (type == typeof(int)) return new IAttribInfo(1, GL.VertexAttribIType.INT);
        if (type == typeof(uint)) return new IAttribInfo(1, GL.VertexAttribIType.UNSIGNED_INT);

        // Doubles
        if (type == typeof(double)) return new LAttribInfo(1, GL.VertexAttribLType.DOUBLE);

        throw new Exception($"Vertex attribute type not supported: {type}");
    }

    public void Dispose()
    {
        GL.VertexArrayId id = Id;
        _gl.DeleteVertexArrays(1, &id);
    }
}