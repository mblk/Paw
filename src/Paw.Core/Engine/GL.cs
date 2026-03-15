using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Paw.Core.Engine;

public sealed unsafe partial class GL
{
    public const int MajorVersion = 4;
    public const int MinorVersion = 6;

    public readonly record struct ProgramId(uint Id);
    public readonly record struct ShaderId(uint Id);
    public readonly record struct TextureId(uint Id);
    public readonly record struct BufferId(uint Id);
    public readonly record struct VertexArrayId(uint Id);
    public readonly record struct RenderBufferId(uint Id);
    public readonly record struct FrameBufferId(uint Id);
    public readonly record struct QueryId(uint Id);
    public readonly record struct ProgramPipelineId(uint Id);
    public readonly record struct SamplerId(uint Id);
    public readonly record struct TransformFeedbackId(uint Id);

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

        // Disable notification severity messages
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
            _objectLabel(identifier, name, bytes.Length, (char*)p);
        }
        CheckError();
    }

    private int GetUniformLocation(ProgramId program, scoped ReadOnlySpan<byte> name) // TODO what does "scoped" mean here?
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
            int loc = _getUniformLocation(program, (char*)p);
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
            char** strings = stackalloc char*[1] { (char*)p };
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
            _getShaderInfoLog(shader, len, null, (char*)p);
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
            _getProgramInfoLog(program, len, null, (char*)p);
        }
        CheckError();

        var infoLog = Encoding.UTF8.GetString(buffer).TrimEnd('\0', '\n', '\r');
        return infoLog;
    }
}