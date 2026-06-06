using System.Runtime.InteropServices;

namespace Paw.Core.Platforms.Windows.Native;

//| Win32-Typ                                           | Bedeutung                                                 | C#-Mapping           |
//| --------------------------------------------------- | --------------------------------------------------------- | -------------------- |
//| `HWND`, `HICON`, `HCURSOR`, `HINSTANCE`, `HBRUSH` … | * Handles*, also Zeiger auf interne Strukturen            | `IntPtr` bzw. `nint` |
//| `LRESULT` / `LPARAM`                                | „long“ Zeigergröße(4 Byte auf 32-bit, 8 Byte auf 64-bit)  | `nint`               |
//| `WPARAM`                                            | „unsigned long“ Zeigergröße                               | `nuint`              |
//| `UINT`                                              | Immer 32-Bit unsigned                                     | `uint`               |
//| `DWORD`                                             | Immer 32-Bit unsigned                                     | `uint`               |
//| `POINT`                                             | Zwei `LONG` (immer 32-Bit signed)                         | `int`/`int`          |

internal static class User32
{
    public const int CW_USEDEFAULT = unchecked((int)0x80000000);
    public const int CS_OWNDC = 0x0020;

    // Cursor IDs
    public const int IDC_ARROW = 32512;
    public const int IDC_WAIT = 32514;

    // Window styles
    public const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
    public const uint WS_VISIBLE = 0x10000000;

    // Window messages
    public const uint WM_DESTROY = 0x0002;
    public const uint WM_QUIT = 0x0012;

    public const uint WM_SIZE = 0x0005;

    public const uint WM_ACTIVATEAPP = 0x001C;
    public const uint WM_ACTIVATE = 0x0006;
    public const uint WM_KILLFOCUS = 0x0008;
    public const uint WM_SETFOCUS = 0x0007;

    public const uint WM_INPUT = 0x00FF;
    public const uint WM_CHAR = 0x0102;
    public const uint WM_SYSCHAR = 0x0106;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_SYSKEYDOWN = 0x0104;
    public const uint WM_SYSKEYUP = 0x0105;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_MBUTTONDOWN = 0x0207;
    public const uint WM_MBUTTONUP = 0x0208;
    public const uint WM_MOUSEWHEEL = 0x020A;
    public const uint WM_MOUSEHWHEEL = 0x020E;

    // WM_SIZE wParam values
    public const uint SIZE_RESTORED = 0;
    public const uint SIZE_MINIMIZED = 1;
    public const uint SIZE_MAXIMIZED = 2;
    public const uint SIZE_MAXSHOW = 3;
    public const uint SIZE_MAXHIDE = 4;

    // for RAWINPUTDEVICE struct
    public const ushort HID_USAGE_PAGE_GENERIC = 0x01;
    public const ushort HID_USAGE_PAGE_GAME = 0x05;
    public const ushort HID_USAGE_PAGE_LED = 0x08;
    public const ushort HID_USAGE_PAGE_BUTTON = 0x09;

    public const ushort HID_USAGE_GENERIC_POINTER = 0x01;
    public const ushort HID_USAGE_GENERIC_MOUSE = 0x02;
    public const ushort HID_USAGE_GENERIC_JOYSTICK = 0x04;
    public const ushort HID_USAGE_GENERIC_GAMEPAD = 0x05;
    public const ushort HID_USAGE_GENERIC_KEYBOARD = 0x06;
    public const ushort HID_USAGE_GENERIC_KEYPAD = 0x07;
    public const ushort HID_USAGE_GENERIC_MULTI_AXIS_CONTROLLER = 0x08;

    public const uint RIDEV_REMOVE = 0x00000001;
    public const uint RIDEV_EXCLUDE = 0x00000010;
    public const uint RIDEV_PAGEONLY = 0x00000020;
    public const uint RIDEV_NOLEGACY = 0x00000030;
    public const uint RIDEV_INPUTSINK = 0x00000100;
    public const uint RIDEV_CAPTUREMOUSE = 0x00000200;
    public const uint RIDEV_NOHOTKEYS = 0x00000200;
    public const uint RIDEV_APPKEYS = 0x00000400;
    public const uint RIDEV_EXINPUTSINK = 0x00001000;
    public const uint RIDEV_DEVNOTIFY = 0x00002000;

    // for GetRawInputData
    public const uint RID_HEADER = 0x10000005;
    public const uint RID_INPUT = 0x10000003;

    // for RAWINPUTHEADER
    public const uint RIM_TYPEMOUSE = 0;
    public const uint RIM_TYPEKEYBOARD = 1;
    public const uint RIM_TYPEHID = 2;

    // for RAWKEYBOARD
    public const ushort RI_KEY_MAKE = 0;
    public const ushort RI_KEY_BREAK = 1;
    public const ushort RI_KEY_E0 = 2;
    public const ushort RI_KEY_E1 = 4;





    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public nint hwnd;
        public uint message;
        public nuint wParam;
        public nint lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
        public uint lPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public nint hwndTarget;
    }

    //[StructLayout(LayoutKind.Explicit)]
    //public struct RAWINPUT
    //{
    //    [FieldOffset(0)] public RAWINPUTHEADER header;
    //    [FieldOffset(16)] public RAWKEYBOARD keyboard;
    //    [FieldOffset(16)] public RAWMOUSE mouse;
    //    // [FieldOffset(16)] public RAWHID hid;
    //}

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWKEYBOARD
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWMOUSE
    {
        public ushort usFlags;
        public ushort usButtonFlags;
        public ushort usButtonData;
        public uint ulRawButtons;
        public int lLastX;
        public int lLastY;
        public uint ulExtraInformation;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern ushort RegisterClassExW(ref WNDCLASSEXW wc);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW")]
    public static extern nint CreateWindowExW(uint exStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr LoadCursorW(IntPtr hInstance, IntPtr lpCursorName);

    [DllImport("user32.dll")] public static extern nint DefWindowProcW(nint hWnd, uint msg, nuint wParam, nint lParam);
    [DllImport("user32.dll")] public static extern bool DestroyWindow(nint hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(nint hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool UpdateWindow(nint hWnd);
    [DllImport("user32.dll")] public static extern bool PeekMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);
    [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] public static extern nint DispatchMessageW(ref MSG lpMsg);
    [DllImport("user32.dll")] public static extern void PostQuitMessage(int nExitCode);
    [DllImport("user32.dll")] public static extern nint GetDC(nint hWnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(nint hWnd, nint hdc);
    [DllImport("user32.dll")] public static extern bool GetClientRect(nint hWnd, out RECT lpRect);

    public static void GetClientSize(nint hwnd, out int w, out int h)
    {
        if (!GetClientRect(hwnd, out var r))
            throw new Exception("GetClientRect failed.");

        w = r.right - r.left;
        h = r.bottom - r.top;
    }

    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterRawInputDevices([In] RAWINPUTDEVICE[] pRawInputDevices, int uiNumDevices, uint cbSize);

    [DllImport("user32.dll")] public static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);
}
