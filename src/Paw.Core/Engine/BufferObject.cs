namespace Paw.Core.Engine;

// Notes for later:
//
// Buffer orphaning:
//    int byteCount = data.Length * sizeof(T);
//    _gl.BufferData(GL.BufferTarget.ARRAY_BUFFER, byteCount, (void*)0, usage); // orphan
//    fixed (T* p = data)
//        _gl.BufferSubData(GL.BufferTarget.ARRAY_BUFFER, 0, byteCount, p);     // fill
//
// check glBufferStorage:
//     allows permanent mapping:
//     void* ptr = glMapBufferRange(GL_ARRAY_BUFFER, 0, size, GL_MAP_WRITE_BIT | GL_MAP_PERSISTENT_BIT | GL_MAP_COHERENT_BIT);
//     better performance for streaming

public unsafe class BufferObject : IDisposable
{
    private readonly GL _gl;

    public GL.BufferId Id { get; }

    public BufferObject(GL gl, string? label = null)
    {
        _gl = gl;

        GL.BufferId id = default;
        gl.CreateBuffers(1, &id);
        Id = id;

        if (!String.IsNullOrWhiteSpace(label))
        {
            gl.Label(GL.ObjectIdentifier.BUFFER, Id.Id, label);
        }
    }

    public void SetData<T>(ReadOnlySpan<T> data, GL.BufferUsage usage)
        where T : unmanaged
    {
        fixed (T* pData = data)
        {
            _gl.NamedBufferData(Id, data.Length * sizeof(T), pData, usage);
        }
    }

    public void SetSizeAndUsage(int size, GL.BufferUsage usage)
    {
        _gl.NamedBufferData(Id, size, null, usage);
    }

    public void Dispose()
    {
        GL.BufferId id = Id;
        _gl.DeleteBuffers(1, &id);
    }
}
