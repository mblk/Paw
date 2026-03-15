namespace Paw.Core.Engine;

// Notes for later:
//
// Buffer orphaning:
//    int byteCount = data.Length * sizeof(T);
//    _gl.BufferData(GL.BufferTarget.ARRAY_BUFFER, byteCount, (void*)0, usage); // orphan
//    fixed (T* p = data)
//        _gl.BufferSubData(GL.BufferTarget.ARRAY_BUFFER, 0, byteCount, p);     // fill
//
// check DSA - direct state access:
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
        gl.GenBuffers(1, &id);
        Id = id;

        if (!string.IsNullOrWhiteSpace(label))
        {
            gl.BindBuffer(GL.BufferTarget.ARRAY_BUFFER, Id);
            gl.ObjectLabel(GL.ObjectIdentifier.BUFFER, Id.Id, label);
            gl.BindBuffer(GL.BufferTarget.ARRAY_BUFFER, default);
        }
    }

    public void SetData<T>(ReadOnlySpan<T> data, GL.BufferUsage usage)
        where T : unmanaged
    {
        _gl.BindBuffer(GL.BufferTarget.ARRAY_BUFFER, Id);

        fixed (T* pData = data)
        {
            _gl.BufferData(GL.BufferTarget.ARRAY_BUFFER, data.Length * sizeof(T), pData, usage);
        }

        _gl.BindBuffer(GL.BufferTarget.ARRAY_BUFFER, default);
    }

    public void SetSizeAndUsage(int size, GL.BufferUsage usage)
    {
        _gl.BindBuffer(GL.BufferTarget.ARRAY_BUFFER, Id);
        _gl.BufferData(GL.BufferTarget.ARRAY_BUFFER, size, (void*)0, usage);
        _gl.BindBuffer(GL.BufferTarget.ARRAY_BUFFER, default);
    }

    public void Dispose()
    {
        GL.BufferId id = Id;
        _gl.DeleteBuffers(1, &id);
    }
}
