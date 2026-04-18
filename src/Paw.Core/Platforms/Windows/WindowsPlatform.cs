using Paw.Core.Graphics;
using Paw.Core.Platforms.Windows.Native;
using System.Collections.Frozen;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paw.Core.Platforms.Windows;

internal unsafe class WindowsPlatform : IPlatform
{
    private const string _windowClassName = "GLWndClass";

    private readonly FrozenDictionary<uint, string> _messagesToPrint = new Dictionary<uint, string>
    {
        [User32.WM_DESTROY] = "WM_DESTROY",
        [User32.WM_QUIT] = "WM_QUIT",

        //[User32.WM_ACTIVATEAPP] = "WM_ACTIVATEAPP",
        //[User32.WM_ACTIVATE] = "WM_ACTIVATE",
        //[User32.WM_SETFOCUS] = "WM_SETFOCUS",
        //[User32.WM_KILLFOCUS] = "WM_KILLFOCUS",

        //[User32.WM_SIZE] = "WM_SIZE",

        //[User32.WM_INPUT] = "WM_INPUT",
        //[User32.WM_KEYDOWN] = "WM_KEYDOWN",
        //[User32.WM_KEYUP] = "WM_KEYUP",
        //[User32.WM_CHAR] = "WM_CHAR",
        //[User32.WM_SYSKEYDOWN] = "WM_SYSKEYDOWN",
        //[User32.WM_SYSKEYUP] = "WM_SYSKEYUP",
        //[User32.WM_SYSCHAR] = "WM_SYSCHAR",

    }.ToFrozenDictionary();

    private readonly User32.WndProcDelegate _globalWindowProcedure;

    private readonly Dictionary<IntPtr, Window> _windows = [];

    public WindowsPlatform(PlatformOptions options)
    {
        // create new delegate instance and store it in a field, to prevent it from being GC'ed
        _globalWindowProcedure = GlobalWindowProcedure;

        RegisterWindowClass();
    }

    public void Dispose()
    {
        // ...
    }

    public IWindow CreateWindow(WindowOptions options)
    {
        nint windowHandle = CreateWindow(options.Width, options.Height, options.Title);
        nint deviceContext = CreateDeviceContext(windowHandle);
        SetPixelFormat(deviceContext);
        nint openGlContext = CreateOpenGlContext(deviceContext);
        RegisterRawInput(windowHandle);

        // load OpenGL entry points

        var wgl = new WGL();
        var gl = new GL(wgl.GetProcAddress);

        // initial setup

        SetSwapInterval(wgl, options.SwapInterval);

        // done

        var window = new Window(windowHandle, deviceContext, openGlContext, gl);
        _windows.Add(windowHandle, window);
        return window;
    }

    private void RegisterWindowClass()
    {
        nint hInstance = Kernel32.GetModuleHandleW(IntPtr.Zero);

        // register window class
        var windowClass = new User32.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<User32.WNDCLASSEXW>(),
            style = User32.CS_OWNDC, // own DC for each window
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_globalWindowProcedure),
            lpszClassName = _windowClassName,
            hInstance = hInstance,
            hCursor = User32.LoadCursorW(IntPtr.Zero, (nint)User32.IDC_ARROW),
        };

        if (User32.RegisterClassExW(ref windowClass) == 0)
            throw new Exception("RegisterClassExW failed.");
    }

    private nint GlobalWindowProcedure(nint hWnd, uint msg, nuint wParam, nint lParam)
    {
        if (_messagesToPrint.TryGetValue(msg, out var name))
        {
            Console.WriteLine($"Global WndProc: hWnd={hWnd:X4} msg={msg:X4} ({name}) wParam={wParam:X} lParam={lParam:X}");
        }
        else
        {
            //Console.WriteLine($"Global WndProc: hWnd={hWnd:X4} msg={msg:X4} wParam={wParam:X} lParam={lParam:X}");
        }

        if (_windows.TryGetValue(hWnd, out var window))
        {
            var result = window.WindowProcedure(hWnd, msg, wParam, lParam);
            if (result.HasValue)
                return result.Value;
        }

        return User32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private static nint CreateWindow(int width, int height, string title)
    {
        nint hInstance = Kernel32.GetModuleHandleW(IntPtr.Zero);

        var windowHandle = User32.CreateWindowExW(
            0,
            _windowClassName,
            title,
            User32.WS_OVERLAPPEDWINDOW | User32.WS_VISIBLE,
            User32.CW_USEDEFAULT,
            User32.CW_USEDEFAULT,
            width,
            height,
            IntPtr.Zero,
            IntPtr.Zero,
            hInstance,
            IntPtr.Zero);

        if (windowHandle == IntPtr.Zero)
            throw new Exception("CreateWindowExW failed.");

        User32.ShowWindow(windowHandle, 1);
        User32.UpdateWindow(windowHandle);

        return windowHandle;
    }

    private static nint CreateDeviceContext(nint windowHandle)
    {
        var deviceContext = User32.GetDC(windowHandle);
        if (deviceContext == IntPtr.Zero)
            throw new Exception("GetDC failed.");
        return deviceContext;
    }

    private static void SetPixelFormat(nint deviceContext)
    {
        var pfd = new GDI32.PIXELFORMATDESCRIPTOR
        {
            nSize = (ushort)Marshal.SizeOf<GDI32.PIXELFORMATDESCRIPTOR>(),
            nVersion = 1,
            dwFlags = GDI32.PFD_DRAW_TO_WINDOW | GDI32.PFD_SUPPORT_OPENGL | GDI32.PFD_DOUBLEBUFFER,
            iPixelType = GDI32.PFD_TYPE_RGBA,
            cColorBits = 24,
            cDepthBits = 24,
            cStencilBits = 8,
            iLayerType = GDI32.PFD_MAIN_PLANE,
        };

        var pf = GDI32.ChoosePixelFormat(deviceContext, ref pfd);
        if (pf == 0)
            throw new Exception("ChoosePixelFormat failed.");

        if (!GDI32.SetPixelFormat(deviceContext, pf, ref pfd))
            throw new Exception("SetPixelFormat failed.");
    }

    private static nint CreateOpenGlContext(nint deviceContext)
    {
        // Create dummy 1.x context to load wglCreateContextAttribsARB
        var dummyOpenGlContext = WGL.wglCreateContext(deviceContext);
        if (dummyOpenGlContext == IntPtr.Zero)
            throw new Exception("Failed to create dummy WGL context.");

        if (!WGL.wglMakeCurrent(deviceContext, dummyOpenGlContext))
            throw new Exception("Failed to make current dummy WGL context.");

        // Load wglCreateContextAttribsARB
        var pCreateCtxAttribs = WGL.wglGetProcAddress("wglCreateContextAttribsARB");
        if (pCreateCtxAttribs == IntPtr.Zero)
            throw new Exception("wglCreateContextAttribsARB not available.");

        var wglCreateContextAttribsARB = (delegate* unmanaged[Stdcall]<nint, nint, int*, nint>)pCreateCtxAttribs;

        // Create real core context
        int* attribs = stackalloc int[]
        {
            WGL.CONTEXT_MAJOR_VERSION_ARB, GL.MajorVersion,
            WGL.CONTEXT_MINOR_VERSION_ARB, GL.MinorVersion,
#if DEBUG
            WGL.CONTEXT_FLAGS_ARB, WGL.CONTEXT_FORWARD_COMPATIBLE_BIT_ARB | WGL.CONTEXT_DEBUG_BIT_ARB,
#else
            WGL.CONTEXT_FLAGS_ARB, WGL.CONTEXT_FORWARD_COMPATIBLE_BIT_ARB,
#endif
            WGL.CONTEXT_PROFILE_MASK_ARB, WGL.CONTEXT_CORE_PROFILE_BIT_ARB,
            0 // Terminator
        };
        nint openGlContext = wglCreateContextAttribsARB(deviceContext, IntPtr.Zero, attribs);
        if (openGlContext == IntPtr.Zero)
            throw new Exception("Failed to create OpenGL Core context.");

        // Delete dummy and switch to real context
        WGL.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
        WGL.wglDeleteContext(dummyOpenGlContext);
        if (!WGL.wglMakeCurrent(deviceContext, openGlContext))
            throw new Exception("Failed to activate OpenGL Core context.");

        return openGlContext;
    }

    private static void SetSwapInterval(WGL wgl, int interval)
    {
        wgl.SetSwapInterval(interval);

        var current = wgl.GetSwapInterval();
        if (current != interval)
        {
            Console.WriteLine($"Warning: Could not set swap interval to {interval}, current is {current}");
        }
        else
        {
            Console.WriteLine($"Swap interval set to {current}");
        }
    }

    private static void RegisterRawInput(nint windowHandle)
    {
        User32.RAWINPUTDEVICE[] rid =
        [
            // keyboard
            new User32.RAWINPUTDEVICE
            {
                usUsagePage = User32.HID_USAGE_PAGE_GENERIC,
                usUsage = User32.HID_USAGE_GENERIC_KEYBOARD,
                dwFlags = 0, //User32.RIDEV_NOLEGACY,
                hwndTarget = windowHandle
            },

            // mouse
            new User32.RAWINPUTDEVICE
            {
                usUsagePage = User32.HID_USAGE_PAGE_GENERIC,
                usUsage = User32.HID_USAGE_GENERIC_MOUSE,
                dwFlags = 0, //User32.RIDEV_NOLEGACY,
                hwndTarget = windowHandle
            }
        ];

        if (!User32.RegisterRawInputDevices(rid, rid.Length, (uint)sizeof(User32.RAWINPUTDEVICE)))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "RegisterRawInputDevices() failed");
    }






    private class Input : IInput
    {
        public required IKeyboard Keyboard { get; set; }
        public required IMouse Mouse { get; set; }
    }

    private class Keyboard : IKeyboard
    {
        private readonly bool[] _currStates = new bool[(int)Key.MaxValue]; // TODO: use KeyboardState?
        private readonly bool[] _prevStates = new bool[(int)Key.MaxValue];

        private readonly char[] _charInput = new char[8];
        private int _numChars = 0;

        public Keyboard()
        {
            Activate();
        }

        public bool Get(Key key)
        {
            int idx = GetIndex(key);
            return _currStates[idx];
        }

        public bool WasPressed(Key key)
        {
            int idx = GetIndex(key);
            bool wasPressed = _currStates[idx] && !_prevStates[idx];
            _prevStates[idx] = _currStates[idx];
            return wasPressed;
        }

        public bool WasReleased(Key key)
        {
            int idx = GetIndex(key);
            bool wasReleased = !_currStates[idx] && _prevStates[idx];
            _prevStates[idx] = _currStates[idx];
            return wasReleased;
        }

        public void GetSnapshot(KeyboardState state)
        {
            new Span<bool>(_currStates).CopyTo(new Span<bool>(state.CurrStates));
            new Span<bool>(_prevStates).CopyTo(new Span<bool>(state.PrevStates));

            new Span<char>(_charInput).CopyTo(new Span<char>(state.Chars));
            state.NumChars = _numChars;
        }

        public void HandleRawInput(User32.RAWKEYBOARD* input)
        {
            ushort scancode = input->MakeCode; // ScanCode (Set 1)
            ushort flags = input->Flags;

            bool e0 = (flags & User32.RI_KEY_E0) != 0;
            bool e1 = (flags & User32.RI_KEY_E1) != 0;
            bool isBreak = (flags & User32.RI_KEY_BREAK) != 0;
            bool isPress = !isBreak;

            var key = MapScanCodeToKey(scancode, e0, e1);
            if (key is null) return;

            SetState(key.Value, isPress);
        }

        public void HandleCharacterInput(char c)
        {
            if (_numChars >= _charInput.Length)
            {
                Console.WriteLine($"HandleCharacterInput: char input buffer full");
                return;
            }

            _charInput[_numChars++] = c;
        }

        private void SetState(Key key, bool isPressed)
        {
            //Console.WriteLine($"{key} >> {isPress}");

            _currStates[GetIndex(key)] = isPressed;
        }

        public void NextFrame()
        {
            for (int i = 0; i < _currStates.Length; i++)
            {
                _prevStates[i] = _currStates[i];
            }
            for (int i = 0; i < _charInput.Length; i++)
            {
                _charInput[i] = (char)0;
            }
            _numChars = 0;
        }

        public void Deactivate()
        {
            for (int i = 0; i < _currStates.Length; i++)
            {
                _currStates[i] = false;
                _prevStates[i] = false;
            }
        }

        public void Activate()
        {
            for (int i = 0; i < _currStates.Length; i++)
            {
                _currStates[i] = false; // TODO: maybe query actual keyboard state?
                _prevStates[i] = false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetIndex(Key key)
        {
            return (int)key;
        }

        private static Key? MapScanCodeToKey(ushort sc, bool e0, bool e1)
        {
            if (e1)
                return Key.Pause;

            switch (sc)
            {
                case 0x01: return Key.Escape;
                case 0x02: return Key.D1;
                case 0x03: return Key.D2;
                case 0x04: return Key.D3;
                case 0x05: return Key.D4;
                case 0x06: return Key.D5;
                case 0x07: return Key.D6;
                case 0x08: return Key.D7;
                case 0x09: return Key.D8;
                case 0x0A: return Key.D9;
                case 0x0B: return Key.D0;
                case 0x0C: return Key.Minus;
                case 0x0D: return Key.Equals;
                case 0x0E: return Key.Backspace;
                case 0x0F: return Key.Tab;

                case 0x10: return Key.Q;
                case 0x11: return Key.W;
                case 0x12: return Key.E;
                case 0x13: return Key.R;
                case 0x14: return Key.T;
                case 0x15: return Key.Y;
                case 0x16: return Key.U;
                case 0x17: return Key.I;
                case 0x18: return Key.O;
                case 0x19: return Key.P;

                case 0x1C: return e0 ? Key.NumEnter : Key.Enter;
                case 0x1D: return e0 ? Key.RControl : Key.LControl;

                case 0x1E: return Key.A;
                case 0x1F: return Key.S;
                case 0x20: return Key.D;
                case 0x21: return Key.F;
                case 0x22: return Key.G;
                case 0x23: return Key.H;
                case 0x24: return Key.J;
                case 0x25: return Key.K;
                case 0x26: return Key.L;
                case 0x27: return Key.Semicolon;
                case 0x28: return Key.Apostrophe;

                case 0x29: return Key.Grave;

                case 0x2A: return Key.LShift;
                case 0x2B: return Key.Backslash;
                case 0x2C: return Key.Z;
                case 0x2D: return Key.X;
                case 0x2E: return Key.C;
                case 0x2F: return Key.V;
                case 0x30: return Key.B;
                case 0x31: return Key.N;
                case 0x32: return Key.M;
                case 0x33: return Key.Comma;
                case 0x34: return Key.Period;
                case 0x35: return e0 ? Key.NumDivide : Key.Slash;

                case 0x36: return Key.RShift;
                case 0x37: return e0 ? Key.PrintScreen : Key.NumMultiply;
                case 0x38: return e0 ? Key.RAlt : Key.LAlt;
                case 0x39: return Key.Space;

                case 0x3A: return Key.CapsLock;
                case 0x3B: return Key.F1;
                case 0x3C: return Key.F2;
                case 0x3D: return Key.F3;
                case 0x3E: return Key.F4;
                case 0x3F: return Key.F5;
                case 0x40: return Key.F6;
                case 0x41: return Key.F7;
                case 0x42: return Key.F8;
                case 0x43: return Key.F9;
                case 0x44: return Key.F10;
                case 0x57: return Key.F11;
                case 0x58: return Key.F12;

                case 0x45: return Key.NumLock;
                case 0x46: return Key.ScrollLock;

                case 0x47: return e0 ? Key.Home : Key.Num7;
                case 0x48: return e0 ? Key.Up : Key.Num8;
                case 0x49: return e0 ? Key.PageUp : Key.Num9;
                case 0x4B: return e0 ? Key.Left : Key.Num4;
                case 0x4C: return e0 ? null : Key.Num5;
                case 0x4D: return e0 ? Key.Right : Key.Num6;
                case 0x4F: return e0 ? Key.End : Key.Num1;
                case 0x50: return e0 ? Key.Down : Key.Num2;
                case 0x51: return e0 ? Key.PageDown : Key.Num3;
                case 0x52: return e0 ? Key.Insert : Key.Num0;
                case 0x53: return e0 ? Key.Delete : Key.NumDecimal;

                case 0x5B: if (e0) return Key.LWin; break;
                case 0x5C: if (e0) return Key.RWin; break;
                case 0x5D: if (e0) return Key.Menu; break;
            }

            return null;
        }
    }

    private class Mouse : IMouse
    {
        private readonly bool[] _currStates = new bool[(int)MouseButton.MaxValue];
        private readonly bool[] _prevStates = new bool[(int)MouseButton.MaxValue];

        public int X { get; private set; }
        public int Y { get; private set; }

        public int WheelDelta { get; private set; }

        public Mouse()
        {
            Activate();
        }

        public bool Get(MouseButton button)
        {
            int idx = GetIndex(button);
            return _currStates[idx];
        }

        public bool WasPressed(MouseButton button)
        {
            int idx = GetIndex(button);
            bool wasPressed = _currStates[idx] && !_prevStates[idx];
            _prevStates[idx] = _currStates[idx];
            return wasPressed;
        }

        public bool WasReleased(MouseButton button)
        {
            int idx = GetIndex(button);
            bool wasReleased = !_currStates[idx] && _prevStates[idx];
            _prevStates[idx] = _currStates[idx];
            return wasReleased;
        }

        public void GetSnapshot(MouseState state)
        {
            new Span<bool>(_currStates).CopyTo(new Span<bool>(state.CurrStates));
            new Span<bool>(_prevStates).CopyTo(new Span<bool>(state.PrevStates));

            state.X = X;
            state.Y = Y;
            state.WheelDelta = WheelDelta;
        }

        public void HandleRawInput(User32.RAWMOUSE* mouse)
        {
            // ...
            //Console.WriteLine($"raw mouse: {mouse->lLastX} {mouse->lLastY} {mouse->ulRawButtons}");
        }

        public void HandleWindowMessage(uint msg, nuint wParam, nint lParam)
        {
            switch (msg)
            {
                case User32.WM_MOUSEMOVE:
                {
                    (X, Y) = ExtractMousePosition(lParam);
                    break;
                }

                case User32.WM_LBUTTONDOWN:
                case User32.WM_MBUTTONDOWN:
                case User32.WM_RBUTTONDOWN:
                case User32.WM_LBUTTONUP:
                case User32.WM_MBUTTONUP:
                case User32.WM_RBUTTONUP:
                {
                    (X, Y) = ExtractMousePosition(lParam);
                    SetState(GetMouseButtonFromMessage(msg), GetMouseButtonStateFromMessage(msg));
                    break;
                }

                case User32.WM_MOUSEWHEEL:
                {
                    WheelDelta = ExtractMouseWheelDelta(wParam);
                    break;
                }

                default:
                {
                    throw new NotImplementedException("Unknown message");
                }
            }
        }

        private static MouseButton GetMouseButtonFromMessage(uint msg)
        {
            return msg switch
            {
                User32.WM_LBUTTONDOWN or User32.WM_LBUTTONUP => MouseButton.Left,
                User32.WM_MBUTTONDOWN or User32.WM_MBUTTONUP => MouseButton.Middle,
                User32.WM_RBUTTONDOWN or User32.WM_RBUTTONUP => MouseButton.Right,
                _ => throw new NotImplementedException("Unknown message"),
            };
        }

        private static bool GetMouseButtonStateFromMessage(uint msg)
        {
            return msg switch
            {
                User32.WM_LBUTTONDOWN or User32.WM_MBUTTONDOWN or User32.WM_RBUTTONDOWN => true,
                User32.WM_LBUTTONUP or User32.WM_MBUTTONUP or User32.WM_RBUTTONUP => false,
                _ => throw new NotImplementedException("Unknown message"),
            };
        }

        private static (int, int) ExtractMousePosition(nint lParam)
        {
            UInt32 lParam32 = (UInt32)lParam;
            UInt16 mouseX = (UInt16)(lParam32 & 0xFFFF);
            UInt16 mouseY = (UInt16)((lParam32 & 0xFFFF0000) >> 16);
            return (mouseX, mouseY);
        }

        private static int ExtractMouseWheelDelta(nuint wParam)
        {
            UInt32 wParam32 = (UInt32)wParam;
            int delta = (Int16)((wParam32 & 0xFFFF0000) >> 16);
            delta /= 120;
            return delta;
        }

        public void NextFrame()
        {
            for (int i = 0; i < _currStates.Length; i++)
            {
                _prevStates[i] = _currStates[i];
            }
            WheelDelta = 0;
        }

        public void Deactivate()
        {
            for (int i = 0; i < _currStates.Length; i++)
            {
                _currStates[i] = false;
                _prevStates[i] = false;
            }
            WheelDelta = 0;
        }

        public void Activate()
        {
            for (int i = 0; i < _currStates.Length; i++)
            {
                _currStates[i] = false; // TODO: maybe query actual button state?
                _prevStates[i] = false;
            }
            WheelDelta = 0;
        }

        private void SetState(MouseButton button, bool isPressed)
        {
            //Console.WriteLine($"{button} >> {isPressed}");

            _currStates[GetIndex(button)] = isPressed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetIndex(MouseButton button)
        {
            return (int)button;
        }
    }

    private class Window : IWindow
    {
        private readonly nint _windowHandle;
        private readonly nint _deviceContext;
        private readonly nint _openGlContext;

        private readonly Keyboard _keyboard;
        private readonly Mouse _mouse;
        private readonly Input _input;

        private bool _resizePending;
        private int _clientW, _clientH;


        public GL GL { get; }

        public (int, int) Size { get; private set; }

        public IInput Input => _input;

        public Window(nint windowHandle, nint deviceContext, nint openGlContext, GL gl)
        {
            _windowHandle = windowHandle;
            _deviceContext = deviceContext;
            _openGlContext = openGlContext;
            GL = gl;

            _keyboard = new();
            _mouse = new();
            _input = new()
            {
                Keyboard = _keyboard,
                Mouse = _mouse,
            };

            User32.GetClientSize(windowHandle, out var cw, out var ch);
            // Console.WriteLine($"Setting initial viewport: {cw} {ch}");
            // gl.Viewport(0, 0, cw, ch);
            Size = (cw, ch);
        }

        public void Dispose()
        {
            WGL.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
            WGL.wglDeleteContext(_openGlContext);

            User32.ReleaseDC(_windowHandle, _deviceContext);
            User32.DestroyWindow(_windowHandle);
        }

        public nint? WindowProcedure(nint hWnd, uint msg, nuint wParam, nint lParam)
        {
            switch (msg)
            {
                case User32.WM_DESTROY:
                {
                    User32.PostQuitMessage(0);
                    return IntPtr.Zero;
                }

                case User32.WM_SIZE:
                {
                    HandleWmSize(wParam, lParam);
                    return IntPtr.Zero;
                }

                case User32.WM_INPUT:
                {
                    HandleWmInput(lParam);
                    return IntPtr.Zero;
                }

                case User32.WM_MOUSEMOVE:
                case User32.WM_LBUTTONDOWN:
                case User32.WM_LBUTTONUP:
                case User32.WM_MBUTTONDOWN:
                case User32.WM_MBUTTONUP:
                case User32.WM_RBUTTONDOWN:
                case User32.WM_RBUTTONUP:
                case User32.WM_MOUSEWHEEL:
                {
                    _mouse.HandleWindowMessage(msg, wParam, lParam);
                    return IntPtr.Zero;
                }

                case User32.WM_CHAR:
                {
                    uint cp = wParam.ToUInt32();
                    string s = char.ConvertFromUtf32((int)cp); // TODO can this also return more chan 1 char?

                    if (s.Length == 1)
                    {
                        char c = s[0];
                        //Console.WriteLine($"WM_CHAR: {(int)c} '{c}'");
                        _keyboard.HandleCharacterInput(c);
                    }
                    else
                    {
                        Console.WriteLine($"WM_CHAR ConvertFromUtf32 returned unexpected value");
                    }

                    return IntPtr.Zero;
                }

                case User32.WM_ACTIVATEAPP:
                {
                    bool isActive = wParam != 0;
                    //Console.WriteLine($"isActive: {isActive}");

                    if (isActive)
                    {
                        Console.WriteLine("WM_ACTIVATEAPP: activate");
                        _keyboard.Activate();
                        _mouse.Activate();
                    }
                    else
                    {
                        Console.WriteLine("WM_ACTIVATEAPP: deactivate");
                        _keyboard.Deactivate();
                        _mouse.Deactivate();
                    }

                    return IntPtr.Zero;
                }

                case User32.WM_SETFOCUS:
                {
                    Console.WriteLine("WM_SETFOCUS");
                    return IntPtr.Zero;
                }

                case User32.WM_KILLFOCUS:
                {
                    Console.WriteLine("WM_KILLFOCUS");
                    return IntPtr.Zero;
                }
            }

            return null; // not handled
        }

        private void HandleWmSize(nuint wParam, nint lParam)
        {
            if (wParam == User32.SIZE_MINIMIZED)
            {
                return; // ignore (0,0) size
            }

            var w = lParam.ToInt32() & 0xFFFF;
            var h = lParam.ToInt32() >> 16 & 0xFFFF;

            _clientW = w;
            _clientH = h;
            _resizePending = true;
        }

        private void HandleWmInput(nint lParam)
        {
            const int bufferSize = 128;

            Span<byte> buffer = stackalloc byte[bufferSize];

            fixed (byte* ptrToBuffer = buffer)
            {
                uint bufferSizeCopy = bufferSize;
                uint bytesCopied = User32.GetRawInputData(lParam, User32.RID_INPUT, (nint)ptrToBuffer, ref bufferSizeCopy, (uint)sizeof(User32.RAWINPUTHEADER));

                if (bytesCopied < sizeof(User32.RAWINPUTHEADER))
                {
                    Console.WriteLine($"GetRawInputData() failed or returned too small size: {bytesCopied}");
                    return;
                }

                User32.RAWINPUTHEADER* header = (User32.RAWINPUTHEADER*)ptrToBuffer;

                if (header->dwType == User32.RIM_TYPEKEYBOARD)
                {
                    if (bytesCopied < sizeof(User32.RAWINPUTHEADER) + sizeof(User32.RAWKEYBOARD))
                    {
                        Console.WriteLine($"GetRawInputData() failed or returned too small size: {bytesCopied}");
                        return;
                    }

                    User32.RAWKEYBOARD* kb = (User32.RAWKEYBOARD*)(ptrToBuffer + sizeof(User32.RAWINPUTHEADER));
                    _keyboard.HandleRawInput(kb);
                }
                else if (header->dwType == User32.RIM_TYPEMOUSE)
                {
                    if (bytesCopied < sizeof(User32.RAWINPUTHEADER) + sizeof(User32.RAWMOUSE))
                    {
                        Console.WriteLine($"GetRawInputData() failed or returned too small size: {bytesCopied}");
                        return;
                    }

                    User32.RAWMOUSE* mouse = (User32.RAWMOUSE*)(ptrToBuffer + sizeof(User32.RAWINPUTHEADER));
                    _mouse.HandleRawInput(mouse);
                }
                else if (header->dwType == User32.RIM_TYPEHID)
                {
                    // HID input (joystick, gamepad, ...)
                }
            }
        }

        private bool TryDequeueResize(out int w, out int h) // TODO remove / cleanup
        {
            if (_resizePending)
            {
                _resizePending = false;
                w = _clientW;
                h = _clientH;
                return true;
            }

            w = h = 0;
            return false;
        }

        public bool ProcessEvents()
        {
            _keyboard.NextFrame();
            _mouse.NextFrame();

            var running = true;

            while (User32.PeekMessageW(out var msg, IntPtr.Zero, 0, 0, 1))
            {
                if (msg.message == User32.WM_QUIT)
                {
                    running = false;
                }
                User32.TranslateMessage(ref msg);
                User32.DispatchMessageW(ref msg);
            }

            if (TryDequeueResize(out var w, out var h))
            {
                Console.WriteLine($"ProcessEvents: New size is {w} {h}");
                Size = (w, h);
            }

            return running;
        }

        public void SwapBuffers()
        {
            GDI32.SwapBuffers(_deviceContext);
        }
    }
}
