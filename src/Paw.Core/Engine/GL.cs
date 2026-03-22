using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Paw.Core.Engine;

// Ideas for additional features:
//
// 1. Span<T> / ReadOnlySpan<T> overloads — For pointer-taking functions like GenBuffers(int n, BufferId* buffers),
//    generate safe overloads like GenBuffers(Span<BufferId> buffers) that do the fixed pinning internally.
//    This is the biggest ergonomic win.
//
// 2. Single - value convenience overloads — For Gen*/Create*/Delete* functions that take(int n, T* ptr),
//    generate BufferId GenBuffer() / void DeleteBuffer(BufferId id) single - item helpers.
//
// 3. String parameter overloads — Generate string - accepting overloads for functions like GetUniformLocation(ProgramId, scoped ReadOnlySpan<byte>),
//    BindAttribLocation(ProgramId, uint, byte*), etc. (the UTF-8 encoding + null-termination + fixed boilerplate currently written by hand in GL.cs).
//
// 4. Enum validation in DEBUG — Emit #if DEBUG assertions that enum values passed to GL functions are actually defined members, catching miscast integers early.
//
// 5. XML doc comments — Pull the <command> descriptions from gl.xml and emit /// <summary> on the generated wrappers so IntelliSense shows GL documentation.
//    >> https://github.com/KhronosGroup/OpenGL-Refpages/tree/main/gl4
//    >> https://github.com/BSVino/docs.gl/tree/mainline/gl4
//    >> https://docs.gl/
//
// 6. Extension support — Add an option to include selected<extensions>(e.g., GL_ARB_bindless_texture) beyond the core profile, using the same require/remove machinery.

public sealed unsafe partial class GL
{
    private readonly Func<string, nint> _loader;

    private DebugProc? _debugProc;

    public GL(Func<string, nint> loader)
    {
        _loader = loader;

        LoadFunctions();
        VerifyLoaded();
        EnableDebugOutput();
        PrintInfos();
    }

    private nint Load(string name)
    {
        var p = _loader(name);
        if (p == nint.Zero)
            throw new InvalidOperationException($"GL function not found: {name}");
        return p;
    }

    [Conditional("DEBUG")]
    private void CheckError([CallerMemberName] string? caller = "")
    {
        var hadError = false;
        ErrorCode error;
        while ((error = GetError()) != ErrorCode.NO_ERROR)
        {
            hadError = true;
            Console.WriteLine($"GL error: {caller} -> {error} (0x{(uint)error:X})");
        }

        if (hadError)
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }

            throw new Exception("GL error(s) detected");
        }
    }

    [Conditional("DEBUG")]
    private void VerifyLoaded()
    {
        var type = typeof(GL);
        foreach (var fieldInfo in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            object? value = fieldInfo.GetValue(this);

            if (value is nint addr && addr == 0)
            {
                throw new Exception($"Field {fieldInfo.Name} not initialized");
            }
        }
    }

    [Conditional("DEBUG")]
    private void EnableDebugOutput()
    {
        Enable(EnableCap.DEBUG_OUTPUT);
        Enable(EnableCap.DEBUG_OUTPUT_SYNCHRONOUS);

        // Keep Reference to prevent GC
        _debugProc = DebugCallback;

        DebugMessageCallback(_debugProc, (void*)0);

        // Enable all messages
        DebugMessageControl(DebugSource.DONT_CARE, DebugType.DONT_CARE, DebugSeverity.DONT_CARE, 0, null, true);

        // Disable notification
        DebugMessageControl(DebugSource.DONT_CARE, DebugType.DONT_CARE, DebugSeverity.DEBUG_SEVERITY_NOTIFICATION, 0, null, false);
    }

    private void DebugCallback(DebugSource source, DebugType type, uint id, DebugSeverity severity, int length, sbyte* message, void* userParam)
    {
        string msg = Marshal.PtrToStringAnsi((nint)message, length) ?? string.Empty;

        Console.WriteLine($"GL: {severity}|{source}|{type}: {msg}");
    }

    private void PrintInfos()
    {
        Console.WriteLine("OpenGL initialized:");
        Console.WriteLine($"  Version:    {GetString(StringName.VERSION)}");
        Console.WriteLine($"  Vendor:     {GetString(StringName.VENDOR)}");
        Console.WriteLine($"  Renderer:   {GetString(StringName.RENDERER)}");
        Console.WriteLine($"  SL Version: {GetString(StringName.SHADING_LANGUAGE_VERSION)}");
    }

    // ------------------------------------------------------------------------

    //
    // Handwritten types
    //

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void DebugProc(DebugSource source, DebugType type, uint id, DebugSeverity severity, int length, sbyte* message, void* userParam);

    //
    // Handwritten mappings for strings, etc
    //

    public void ObjectLabel(ObjectIdentifier identifier, uint name, string label)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(label);
        fixed (byte* p = bytes)
        {
            _objectLabel(identifier, name, bytes.Length, p);
        }
        CheckError();
    }

    private int GetUniformLocation(ProgramId program, scoped ReadOnlySpan<byte> name) // scoped == allow caller to pass stack allocated buffer
    {
#if DEBUG
        if (name.Length < 1) throw new ArgumentException("Name must not be empty", nameof(name));
#endif

        ReadOnlySpan<byte> terminatedName = name;

        if (name[^1] != 0)
        {
            Span<byte> buffer = name.Length < 256
                ? stackalloc byte[name.Length + 1]
                : new byte[name.Length + 1];

            name.CopyTo(buffer);
            buffer[^1] = 0;

            terminatedName = buffer;
        }

        fixed (byte* p = terminatedName)
        {
            int loc = _getUniformLocation(program, p);
            CheckError();
#if DEBUG
            if (loc == -1)
            {
                Console.WriteLine($"Warning: uniform '{Encoding.UTF8.GetString(name).TrimEnd('\0')}' not found in program {program}");
            }
#endif
            return loc;
        }
    }

    public int GetUniformLocation(ProgramId program, string name)
    {
#if DEBUG
        if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name must not be empty", nameof(name));
#endif

        int len = Encoding.UTF8.GetByteCount(name);

        Span<byte> buffer = len < 256
            ? stackalloc byte[len + 1]
            : new byte[len + 1];

        Encoding.UTF8.GetBytes(name, buffer[..len]);
        buffer[^1] = 0;

        return GetUniformLocation(program, buffer);
    }

    public void ShaderSource(ShaderId shader, string source)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(source);

        fixed (byte* p = bytes)
        {
            byte** strings = stackalloc byte*[1] { p };
            int length = bytes.Length;

            _shaderSource(shader, 1, strings, &length);
        }

        CheckError();
    }

    public int GetShaderI(ShaderId shader, ShaderParameterName pname)
    {
        int value = 0;
        GetShaderiv(shader, pname, &value);
        return value;
    }

    public int GetProgramI(ProgramId program, ProgramProperty prop)
    {
        int value = 0;
        GetProgramiv(program, prop, &value);
        return value;
    }

    public string GetShaderInfoLog(ShaderId shader)
    {
        int len = GetShaderI(shader, ShaderParameterName.INFO_LOG_LENGTH);
        if (len < 1)
        {
            return String.Empty;
        }

        Span<byte> buffer = len < 256
            ? stackalloc byte[len]
            : new byte[len];

        fixed (byte* p = buffer)
        {
            _getShaderInfoLog(shader, len, null, p);
        }
        CheckError();

        var infoLog = Encoding.UTF8.GetString(buffer).TrimEnd('\0', '\n', '\r');
        return infoLog;
    }

    public string GetProgramInfoLog(ProgramId program)
    {
        int len = GetProgramI(program, ProgramProperty.INFO_LOG_LENGTH);
        if (len < 1)
        {
            return String.Empty;
        }

        Span<byte> buffer = len < 256
            ? stackalloc byte[len]
            : new byte[len];

        fixed (byte* p = buffer)
        {
            _getProgramInfoLog(program, len, null, p);
        }
        CheckError();

        var infoLog = Encoding.UTF8.GetString(buffer).TrimEnd('\0', '\n', '\r');
        return infoLog;
    }

    //
    // ideas for automatically generated convenience functions:
    //

#if false

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BufferId GenBuffer()
    {
        BufferId id = default;
        _genBuffers(1, &id);
        CheckError();
        return id;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GenBuffers(Span<BufferId> buffers)
    {
        fixed (BufferId* ptr = buffers)
        {
            _genBuffers(buffers.Length, ptr);
        }
        CheckError();
    }

#endif
}