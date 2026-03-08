using System.Runtime.InteropServices;

namespace Paw.Core.Engine;

public sealed unsafe partial class GL2
{
    private readonly Func<string, nint> _loader;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void DebugProc(DebugSource source, DebugType type, uint id, DebugSeverity severity, int length, sbyte* message, void* userParam);

    public GL2(Func<string, nint> loader)
    {
        _loader = loader;

        LoadFunctions();
        //VerifyLoaded();
        //PrintInfos();
        //EnableDebugOutput();
    }

    private nint Load(string name)
    {
        var p = _loader(name);
        if (p == nint.Zero)
            throw new InvalidOperationException($"GL function not found: {name}");
        return p;
    }
}