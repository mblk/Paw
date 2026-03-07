using System.Runtime.InteropServices;

namespace Paw.Core.Platforms.LinuxX11.Native;

internal unsafe class GLX
{
    // --- Constants ---
    public const int GLX_X_RENDERABLE = 0x8012;
    public const int GLX_DRAWABLE_TYPE = 0x8010;
    public const int GLX_RENDER_TYPE = 0x8011;
    public const int GLX_X_VISUAL_TYPE = 0x22;
    public const int GLX_TRUE_COLOR = 0x8002;
    public const int GLX_WINDOW_BIT = 0x00000001;
    public const int GLX_RGBA_BIT = 0x00000001;
    public const int GLX_RED_SIZE = 8;
    public const int GLX_GREEN_SIZE = 9;
    public const int GLX_BLUE_SIZE = 10;
    public const int GLX_ALPHA_SIZE = 11;
    public const int GLX_DEPTH_SIZE = 12;
    public const int GLX_STENCIL_SIZE = 13;
    public const int GLX_DOUBLEBUFFER = 5;

    public const int GLX_CONTEXT_MAJOR_VERSION_ARB = 0x2091;
    public const int GLX_CONTEXT_MINOR_VERSION_ARB = 0x2092;
    public const int GLX_CONTEXT_PROFILE_MASK_ARB = 0x9126;
    public const int GLX_CONTEXT_CORE_PROFILE_BIT_ARB = 0x00000001;

    public const int GLX_WIDTH = 0x801D;
    public const int GLX_HEIGHT = 0x801E;

    [DllImport("libGL.so.1")] public static extern nint glXChooseFBConfig(nint display, int screen, int[] attrib_list, out int nelements);
    [DllImport("libGL.so.1")] public static extern nint glXGetVisualFromFBConfig(nint display, nint fbconfig);
    [DllImport("libGL.so.1")] public static extern bool glXMakeCurrent(nint display, nint drawable, nint ctx);
    [DllImport("libGL.so.1")] public static extern void glXSwapBuffers(nint display, nint drawable);
    [DllImport("libGL.so.1")] public static extern void glXDestroyContext(nint display, nint ctx);
    [DllImport("libGL.so.1")] public static extern nint glXCreateWindow(nint display, nint fbconfig, nint win, nint attrib_list);
    [DllImport("libGL.so.1")] public static extern void glXDestroyWindow(nint display, nint glxwindow);

    [DllImport("libGL.so.1")] public static extern void glXQueryDrawable(nint display, nint drawable, int attribute, out uint value);

    [DllImport("libGL.so.1")] public static extern bool glXMakeContextCurrent(nint display, nint draw, nint read, nint ctx);

    [DllImport("libGL.so.1")] public static extern void glXWaitGL();
    [DllImport("libGL.so.1")] public static extern void glXWaitX();

    [DllImport("libGL.so.1")] public static extern nint glXGetProcAddress([MarshalAs(UnmanagedType.LPUTF8Str)] string name);



    private delegate* unmanaged<nint, nint, int, void> _glXSwapIntervalEXT;

    private readonly IntPtr _libGL;

    public GLX()
    {
        if (!NativeLibrary.TryLoad("libGL.so.1", out _libGL))
            throw new Exception("Failed to load libGL.so.1");

        LoadExtensions();
    }

    private void LoadExtensions()
    {
        _glXSwapIntervalEXT = (delegate* unmanaged<nint, nint, int, void>)GetProcAddressWithFallback("glXSwapIntervalEXT");
        if (_glXSwapIntervalEXT == null)
        {
            Console.WriteLine("glXSwapIntervalEXT not found.");
        }
        else
        {
            Console.WriteLine("glXSwapIntervalEXT loaded.");
        }
    }

    public nint GetProcAddressWithFallback(string name)
    {
        var p = glXGetProcAddress(name);
        if (p != IntPtr.Zero)
            return p;

        if (NativeLibrary.TryGetExport(_libGL, name, out p))
            return p;

        Console.WriteLine($"GLX: Failed to get address of {name}");
        return IntPtr.Zero;
    }
    
    public void SwapIntervalEXT(nint display, nint drawable, int interval)
    {
        if (_glXSwapIntervalEXT != null)
            _glXSwapIntervalEXT(display, drawable, interval);
    }
}
