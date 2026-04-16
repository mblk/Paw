using System.Runtime.InteropServices;

namespace Paw.Core.Platforms.LinuxX11.Native;

internal unsafe static class X11
{
    public enum CreateWindowValueMask : ulong
    {
        EventMask = 1L << 11,
        ColorMap = 1L << 13,
    }

    public enum EventMask : ulong
    {
        KeyPress = 1L << 0,
        KeyRelease = 1L << 1,
        ButtonPress = 1L << 2,
        ButtonRelease = 1L << 3,
        PointerMotion = 1L << 6,
        Exposure = 1L << 15,
        StructureNotify = 1L << 17,
    }

    public enum EventType : uint
    {
        KeyPress = 2,
        KeyRelease = 3,
        ButtonPress = 4,
        ButtonRelease = 5,
        MotionNotify = 6,
        ConfigureNotify = 22,
        ClientMessage = 33,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XVisualInfo
    {
        public nint visual;
        public nint visualid;
        public int screen;
        public int depth;
        public int c_class;
        public long red_mask;
        public long green_mask;
        public long blue_mask;
        public int colormap_size;
        public int bits_per_rgb;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XSetWindowAttributes
    {
        public nint background_pixmap;
        public long background_pixel;
        public long border_pixmap;
        public long border_pixel;
        public int bit_gravity;
        public int win_gravity;
        public int backing_store;
        public long backing_planes;
        public long backing_pixel;
        public bool save_under;
        public EventMask event_mask;
        public long do_not_propagate_mask;
        public bool override_redirect;
        public nint colormap;
        public nint cursor;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XWindowAttributes
    {
        public nint x, y;
        public int width, height;
        public int border_width;
        public int depth;
        public nint visual;
        public nint root;
        public int c_class;
        public int bit_gravity, win_gravity;
        public int backing_store;
        public long backing_planes;
        public long backing_pixel;
        public bool save_under;
        public nint colormap;
        public bool map_installed;
        public int map_state;
        public long all_event_masks;
        public long your_event_mask;
        public long do_not_propagate_mask;
        public bool override_redirect;
        public nint screen;
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    public struct XEvent
    {
        // All fields with offset 0 means this works like a union in c.
        [FieldOffset(0)] public EventType type;
        [FieldOffset(0)] public XEventAny any;
        [FieldOffset(0)] public XKeyEvent key;
        [FieldOffset(0)] public XButtonEvent button;
        [FieldOffset(0)] public XMotionEvent motion;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct XEventAny
    {
        [FieldOffset(0)] public EventType type;
        [FieldOffset(8)] public uint serial;
        [FieldOffset(16)] public int send_event; // bool
        [FieldOffset(24)] public nint display; // pointer
        [FieldOffset(32)] public int window; // id
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XKeyEvent
    {
        public EventType type;
        public ulong serial;
        public int send_event;
        public nint display;
        public ulong window;
        public ulong root;
        public ulong subwindow;
        public ulong time;
        public int x, y;
        public int x_root, y_root;
        public uint state;
        public uint keycode;
        public int same_screen;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XButtonEvent
    {
        public EventType type;
        public ulong serial;
        public int send_event;
        public nint display;
        public ulong window;
        public ulong root;
        public ulong subwindow;
        public ulong time;
        public int x, y;
        public int x_root, y_root;
        public uint state;
        public uint button;
        public int same_screen;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XMotionEvent
    {
        public EventType type;
        public ulong serial;
        public int send_event;
        public nint display;
        public ulong window;
        public ulong root;
        public ulong subwindow;
        public ulong time;
        public int x, y;
        public int x_root, y_root;
        public uint state;
        public byte is_hint;
        public int same_screen;
    }

    [DllImport("libX11.so.6")] public static extern nint XOpenDisplay(nint display);
    [DllImport("libX11.so.6")] public static extern int XDefaultScreen(nint display);
    [DllImport("libX11.so.6")] public static extern nint XRootWindow(nint display, int screen);
    [DllImport("libX11.so.6")] public static extern nint XCreateColormap(nint display, nint window, nint visual, int alloc);

    [DllImport("libX11.so.6")]
    public static extern nint XCreateWindow(
        nint display, nint parent,
        int x, int y,
        int width, int height,
        int border_width, int depth,
        int c_class, nint visual,
        CreateWindowValueMask valuemask,
        ref XSetWindowAttributes attributes);

    [DllImport("libX11.so.6")] public static extern void XStoreName(nint display, nint window, string window_name);
    [DllImport("libX11.so.6")] public static extern void XMapWindow(nint display, nint window);
    [DllImport("libX11.so.6")] public static extern int XSync(nint display, bool discard);
    [DllImport("libX11.so.6")] public static extern void XDestroyWindow(nint display, nint window);
    [DllImport("libX11.so.6")] public static extern void XCloseDisplay(nint display);

    [DllImport("libX11.so.6")] public static extern int XPending(nint display);
    [DllImport("libX11.so.6")] public static extern void XNextEvent(nint display, void* data);
}
