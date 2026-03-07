using System.Runtime.InteropServices;

namespace Paw.Core.Platforms.Windows.Native;

internal unsafe class WGL
{
    [DllImport("opengl32.dll", ExactSpelling = true)] public static extern nint wglCreateContext(nint hdc);
    [DllImport("opengl32.dll", ExactSpelling = true)] public static extern bool wglDeleteContext(nint hglrc);
    [DllImport("opengl32.dll", ExactSpelling = true)] public static extern bool wglMakeCurrent(nint hdc, nint hglrc);
    [DllImport("opengl32.dll", ExactSpelling = true, CharSet = CharSet.Ansi)] public static extern nint wglGetProcAddress(string name);

    // wglCreateContextAttribsARB attributes
    public const int CONTEXT_MAJOR_VERSION_ARB = 0x2091;
    public const int CONTEXT_MINOR_VERSION_ARB = 0x2092;
    public const int CONTEXT_FLAGS_ARB = 0x2094;
    public const int CONTEXT_PROFILE_MASK_ARB = 0x9126;

    // WGL_CONTEXT_FLAGS_ARB bits
    public const int CONTEXT_DEBUG_BIT_ARB = 0x0001;
    public const int CONTEXT_FORWARD_COMPATIBLE_BIT_ARB = 0x0002;

    // WGL_CONTEXT_PROFILE_MASK_ARB bits
    public const int CONTEXT_CORE_PROFILE_BIT_ARB = 0x00000001;

    private nint _libGL = nint.Zero;

    private delegate* unmanaged[Stdcall]<int, bool> _wglSwapIntervalEXT;
    private delegate* unmanaged[Stdcall]<int> _wglGetSwapIntervalEXT;

    public WGL()
    {
        LoadExtensions();
    }

    private void LoadExtensions()
    {
        _wglSwapIntervalEXT = (delegate* unmanaged[Stdcall]<int, bool>)GetProcAddress("wglSwapIntervalEXT");
        _wglGetSwapIntervalEXT = (delegate* unmanaged[Stdcall]<int>)GetProcAddress("wglGetSwapIntervalEXT");
    }

    public void SetSwapInterval(int interval) // 0=vsync off, 1=vsync on
    {
        if (_wglSwapIntervalEXT is null)
            return;

        _wglSwapIntervalEXT(interval);
    }

    public int GetSwapInterval()
    {
        if (_wglGetSwapIntervalEXT is null)
            return 0;

        return _wglGetSwapIntervalEXT();
    }

    public nint GetProcAddress(string name)
    {
        nint p;

        // newer functions must be queried via wglGetProcAddress
        p = GetProcAddressFromWgl(name);
        if (p != nint.Zero)
            return p;

        // older/core symbols may be in opengl32.dll
        p = GetProcAddressFromNativeLib(name);
        if (p != nint.Zero)
            return p;

        Console.WriteLine($"GetProcAddress: {name} not found.");
        return nint.Zero;
    }

    private nint GetProcAddressFromWgl(string name)
    {
        var p = wglGetProcAddress(name);

        // Filter out bogus values sometimes returned
        if (p == nint.Zero || p == new nint(1) || p == new nint(2) || p == new nint(3) || p == new nint(-1))
            return nint.Zero;

        Console.WriteLine($"GetProcAddress (WGL): {name} = 0x{p:X}");
        return p;
    }

    private IntPtr GetProcAddressFromNativeLib(string name)
    {
        if (_libGL == nint.Zero)
        {
            // opengl32.dll for fallback (core functions <= 1.1)
            if (!NativeLibrary.TryLoad("opengl32.dll", out _libGL))
                throw new Exception("Failed to load opengl32.dll");
        }

        if (NativeLibrary.TryGetExport(_libGL, name, out var p))
        {
            Console.WriteLine($"GetProcAddress (fallback): {name} = 0x{p:X}");
            return p;
        }

        return nint.Zero;
    }
}
