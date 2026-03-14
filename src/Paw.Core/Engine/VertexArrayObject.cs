using System.Diagnostics;
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

    public uint Id { get; }

    public VertexArrayObject(GL gl, BufferObject vertexBufferObject, string? label = null)
    {
        _gl = gl;
        _vertexBufferObject = vertexBufferObject;

        Id = CreateArrayAndConfigureAttributes(gl);

        if (!string.IsNullOrWhiteSpace(label))
        {
            gl.BindVertexArray(Id);
            gl.ObjectLabel(GL.ObjectIdentifier.VERTEX_ARRAY, Id, label);
            gl.BindVertexArray(0);
        }
    }

    private uint CreateArrayAndConfigureAttributes(GL gl)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"VertexArrayObject:");
        Console.WriteLine($"  Type: {typeof(T).FullName}");

        //int stride = Marshal.SizeOf<T>();
        //int stride = Unsafe.SizeOf<T>();
        int stride = sizeof(T);
        Console.WriteLine($"  Stride: {stride}");

        uint id = 0;
        gl.GenVertexArrays(1, &id);
        gl.BindVertexArray(id);

        gl.BindBuffer(GL.BufferTarget.ARRAY_BUFFER, _vertexBufferObject.Id);

        var fields = typeof(T)
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(fi => new
            {
                Field = fi,
                Offset = (int)Marshal.OffsetOf<T>(fi.Name),
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
            gl.EnableVertexAttribArray(field.Index);

            var attribInfo = GetAttribInfo(field.Field.FieldType);
            Console.WriteLine($"  Attrib {field.Index}: '{field.Field.Name}' offset={field.Offset} {attribInfo}");

            switch (attribInfo)
            {
                case AttribInfo info: gl.VertexAttribPointer(field.Index, info.Components, info.Type, info.Normalized ? (byte)1 : (byte)0, stride, (void*)field.Offset); break;
                case IAttribInfo info: gl.VertexAttribIPointer(field.Index, info.Components, info.Type, stride, (void*)field.Offset); break;
                case LAttribInfo info: gl.VertexAttribLPointer(field.Index, info.Components, info.Type, stride, (void*)field.Offset); break;
                default: throw new NotImplementedException();
            }
        }

        // Unbind
        gl.BindVertexArray(0);
        gl.BindBuffer(GL.BufferTarget.ARRAY_BUFFER, 0);

        Console.WriteLine($"-> {sw.ElapsedMilliseconds} ms"); // TODO: maybe add cache later
        return id;
    }

    public void Bind()
    {
        _gl.BindVertexArray(Id);
    }

    public void Unbind()
    {
        _gl.BindVertexArray(0);
    }

    public void Draw(GL.PrimitiveType mode, int first, int count)
    {
        _gl.DrawArrays(mode, first, count);
    }

    private abstract record CommonAttribInfo(int Components);
    private record AttribInfo(int Components, GL.VertexAttribPointerType Type, bool Normalized) : CommonAttribInfo(Components);
    private record IAttribInfo(int Components, GL.VertexAttribIType Type) : CommonAttribInfo(Components);
    private record LAttribInfo(int Components, GL.VertexAttribLType Type) : CommonAttribInfo(Components);

    private static CommonAttribInfo GetAttribInfo(Type type)
    {
        // Floats
        if (type == typeof(float)) return new AttribInfo(1, GL.VertexAttribPointerType.FLOAT, false);
        if (type == typeof(Vector2)) return new AttribInfo(2, GL.VertexAttribPointerType.FLOAT, false);
        if (type == typeof(Vector3)) return new AttribInfo(3, GL.VertexAttribPointerType.FLOAT, false);
        if (type == typeof(Vector4)) return new AttribInfo(4, GL.VertexAttribPointerType.FLOAT, false);

        if (type == typeof(sbyte)) return new AttribInfo(1, GL.VertexAttribPointerType.BYTE, true);
        if (type == typeof(byte)) return new AttribInfo(1, GL.VertexAttribPointerType.UNSIGNED_BYTE, true);

        // Integers
        if (type == typeof(int)) return new IAttribInfo(1, GL.VertexAttribIType.INT);
        if (type == typeof(uint)) return new IAttribInfo(1, GL.VertexAttribIType.UNSIGNED_INT);

        // Doubles
        if (type == typeof(double)) return new LAttribInfo(1, GL.VertexAttribLType.DOUBLE);

        throw new Exception($"Vertex attribute type not supported: {type}");
    }

    public void Dispose()
    {
        uint id = Id;
        _gl.DeleteVertexArrays(1, &id);
    }
}