using Paw.Core.Graphics;
using Paw.Core.Platforms.LinuxX11.Native;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paw.Core.Platforms.LinuxX11;

public unsafe class LinuxX11Platform : IPlatform
{
    public LinuxX11Platform(PlatformOptions options)
    {
    }

    public IWindow CreateWindow(WindowOptions options)
    {
        //
        // Open X11 display, screen and window.
        //

        nint display = X11.XOpenDisplay(nint.Zero);
        if (display == nint.Zero)
            throw new Exception("XOpenDisplay failed");

        int screen = X11.XDefaultScreen(display);
        nint root = X11.XRootWindow(display, screen);

        //
        // Find GLX FBConfig
        //

        var visualAttribs = new int[]
        {
            GLX.GLX_X_RENDERABLE,  1,
            GLX.GLX_DRAWABLE_TYPE, GLX.GLX_WINDOW_BIT,
            GLX.GLX_RENDER_TYPE,   GLX.GLX_RGBA_BIT,
            GLX.GLX_X_VISUAL_TYPE, GLX.GLX_TRUE_COLOR,
            GLX.GLX_RED_SIZE,      8,
            GLX.GLX_GREEN_SIZE,    8,
            GLX.GLX_BLUE_SIZE,     8,
            GLX.GLX_ALPHA_SIZE,    8,
            GLX.GLX_DEPTH_SIZE,    24,
            GLX.GLX_STENCIL_SIZE,  8,
            GLX.GLX_DOUBLEBUFFER,  1,
            0
        };

        nint fbConfigs = GLX.glXChooseFBConfig(display, screen, visualAttribs, out int nelements);
        Console.WriteLine($"glXChooseFBConfig returned {nelements} matching FBConfigs");
        if (fbConfigs == nint.Zero || nelements == 0)
            throw new Exception("glXChooseFBConfig failed");

        nint fbConfig = Marshal.ReadIntPtr(fbConfigs);
        Console.WriteLine($"Using FBConfig {fbConfig}");

        //
        // Get XVisualInfo for FBConfig
        //

        nint visInfoPtr = GLX.glXGetVisualFromFBConfig(display, fbConfig);
        if (visInfoPtr == nint.Zero)
            throw new Exception("glXGetVisualFromFBConfig failed");

        X11.XVisualInfo vis = Marshal.PtrToStructure<X11.XVisualInfo>(visInfoPtr);

        //
        // Create X11 window
        //

        nint cmap = X11.XCreateColormap(display, root, vis.visual, 0 /*AllocNone*/);

        X11.XSetWindowAttributes swa = new()
        {
            colormap = cmap,
            event_mask = X11.EventMask.Exposure |
                         X11.EventMask.KeyPress |
                         X11.EventMask.KeyRelease |
                         X11.EventMask.StructureNotify
        };

        nint window = X11.XCreateWindow(
            display,
            root,
            0,
            0,
            options.Width,
            options.Height,
            0,
            vis.depth,
            1 /*InputOutput*/,
            vis.visual,
            X11.CreateWindowValueMask.ColorMap | X11.CreateWindowValueMask.EventMask,
            ref swa);
        if (window == nint.Zero)
            throw new Exception("XCreateWindow failed");

        X11.XStoreName(display, window, options.Title);
        X11.XMapWindow(display, window);
        X11.XSync(display, false);

        //
        // Create GLX window
        //

        nint glxWindow = GLX.glXCreateWindow(display, fbConfig, window, nint.Zero);
        if (glxWindow == nint.Zero)
            throw new Exception("glXCreateWindow failed");

        //
        // Create GL/GLX Context
        //

        var pCreateCtx = GLX.glXGetProcAddress("glXCreateContextAttribsARB");
        if (pCreateCtx == nint.Zero)
            throw new Exception("glXCreateContextAttribsARB not available");

        var glXCreateContextAttribsARB = (delegate* unmanaged<nint, nint, nint, int, int*, nint>)pCreateCtx;

        int* ctxAttribs = stackalloc int[]
        {
            GLX.GLX_CONTEXT_MAJOR_VERSION_ARB, GL.MajorVersion,
            GLX.GLX_CONTEXT_MINOR_VERSION_ARB, GL.MinorVersion,
            GLX.GLX_CONTEXT_PROFILE_MASK_ARB,  GLX.GLX_CONTEXT_CORE_PROFILE_BIT_ARB,
            0
        };

        nint glxContext = glXCreateContextAttribsARB(display, fbConfig, nint.Zero, 1, ctxAttribs);
        if (glxContext == IntPtr.Zero)
            throw new Exception($"Failed to create GL {GL.MajorVersion}.{GL.MinorVersion} core context");

        if (!GLX.glXMakeContextCurrent(display, glxWindow, glxWindow, glxContext))
            throw new Exception("glXMakeContextCurrent failed");

        //
        // Load GL functions
        //

        var glx = new GLX();
        var gl = new GL(glx.GetProcAddressWithFallback);

        // 0 = VSync off
        // 1 = VSync on
        glx.SwapIntervalEXT(display, glxWindow, options.SwapInterval);

        return new Window(display, window, glxWindow, glxContext, gl);
    }

    public void Dispose()
    {
    }

    private class Input : IInput
    {
        public required IKeyboard Keyboard { get; init; }
        public required IMouse Mouse { get; init; }
    }

    private class Keyboard : IKeyboard
    {
        private readonly bool[] _currStates = new bool[(int)Key.MaxValue];
        private readonly bool[] _prevStates = new bool[(int)Key.MaxValue];

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

        public void HandleInput(in X11.XKeyEvent keyEvent)
        {
            bool isPress = keyEvent.type == X11.EventType.KeyPress;
            bool isRelease = keyEvent.type == X11.EventType.KeyRelease;

            uint keycode = keyEvent.keycode;
            uint state = keyEvent.state;

            Key? key = MapKeycodeToKey(state, keycode - 8); // offset 8 for "historical reasons"
            if (key is null) return;

            SetState(key.Value, isPress);
        }

        private void SetState(Key key, bool isPress)
        {
            //Console.WriteLine($"{key} >> {isPress}");

            _currStates[GetIndex(key)] = isPress;
        }

        private static Key? MapKeycodeToKey(uint state, uint keycode)
        {
            // if (e1)
            //     return Key.Pause;

            switch (keycode)
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

                case 0x1C: return Key.Enter;
                //case 0x1C: return e0 ? Key.NumEnter : Key.Enter;
                //case 0x1D: return e0 ? Key.RControl : Key.LControl;

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
                //case 0x35: return e0 ? Key.NumDivide : Key.Slash;

                case 0x36: return Key.RShift;
                //case 0x37: return e0 ? Key.PrintScreen : Key.NumMultiply;
                //case 0x38: return e0 ? Key.RAlt : Key.LAlt;
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

                case 0x67: return Key.Up;
                case 0x69: return Key.Left;
                case 0x6A: return Key.Right;
                case 0x6C: return Key.Down;

                // case 0x47: return e0 ? Key.Home : Key.Num7;
                // case 0x48: return e0 ? Key.Up : Key.Num8;
                // case 0x49: return e0 ? Key.PageUp : Key.Num9;
                // case 0x4B: return e0 ? Key.Left : Key.Num4;
                // case 0x4C: return e0 ? null : Key.Num5;
                // case 0x4D: return e0 ? Key.Right : Key.Num6;
                // case 0x4F: return e0 ? Key.End : Key.Num1;
                // case 0x50: return e0 ? Key.Down : Key.Num2;
                // case 0x51: return e0 ? Key.PageDown : Key.Num3;
                // case 0x52: return e0 ? Key.Insert : Key.Num0;
                // case 0x53: return e0 ? Key.Delete : Key.NumDecimal;

                // case 0x5B: if (e0) return Key.LWin; break;
                // case 0x5C: if (e0) return Key.RWin; break;
                // case 0x5D: if (e0) return Key.Menu; break;

                default: Console.WriteLine($"Unknown keycode: 0x{keycode:X}"); break;
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetIndex(Key key)
        {
            return (int)key;
        }
    }

    private class Mouse : IMouse
    {
    }

    private class Window : IWindow
    {
        private readonly nint _display;
        private readonly nint _x11Window;
        private readonly nint _glxWindow;
        private readonly nint _openGlContext;

        private readonly Keyboard _keyboard = new();
        private readonly Mouse _mouse = new();

        public GL GL { get; }

        public (int, int) Size { get; private set; }

        public IInput Input { get; }

        public Window(nint display, nint x11Window, nint glxWindow, nint openGlContext, GL gl)
        {
            _display = display;
            _x11Window = x11Window;
            _glxWindow = glxWindow;
            _openGlContext = openGlContext;

            Input = new Input
            {
                Keyboard = _keyboard,
                Mouse = _mouse
            };

            GL = gl;

            // Move to tests later
            var eventSize1 = Marshal.SizeOf<X11.XEvent>();
            var eventSize2 = Unsafe.SizeOf<X11.XEvent>();
            var keySize1 = Marshal.SizeOf<X11.XKeyEvent>();
            var keySize2 = Unsafe.SizeOf<X11.XKeyEvent>();

            Console.WriteLine($"XEvent sizes {eventSize1} {eventSize2} {keySize1} {keySize2}");

            Debug.Assert(eventSize1 == 192);
            Debug.Assert(eventSize2 == 192);
            Debug.Assert(keySize1 == 96);
            Debug.Assert(keySize2 == 96);
        }

        public bool ProcessEvents()
        {
            const int bufferSize = 256; // 192 plus a little extra
            void* eventBuffer = stackalloc byte[bufferSize];
            var running = true;

            while (X11.XPending(_display) > 0)
            {
                X11.XNextEvent(_display, eventBuffer);
                X11.XEvent* @event = (X11.XEvent*)eventBuffer;
                //Console.WriteLine($"X11 Event: {@event->type}");

                switch (@event->type)
                {
                    case X11.EventType.ClientMessage:
                    {
                        running = false;
                        break;
                    }

                    case X11.EventType.ConfigureNotify:
                    {
                        GLX.glXQueryDrawable(_display, _glxWindow, GLX.GLX_WIDTH, out var gw);
                        GLX.glXQueryDrawable(_display, _glxWindow, GLX.GLX_HEIGHT, out var gh);
                        int w = (int)Math.Max(1, gw);
                        int h = (int)Math.Max(1, gh);
                        Console.WriteLine($"GLX drawable size: {gw}x{gh} (using {w}x{h})");
                        Size = (w, h);
                        break;
                    }

                    case X11.EventType.KeyPress:
                    {
                        _keyboard.HandleInput(@event->key);
                        break;
                    }

                    case X11.EventType.KeyRelease:
                    {
                        _keyboard.HandleInput(@event->key);
                        break;
                    }

                    default:
                    {
                        Console.WriteLine($"Unhandled X11 Event: {@event->type}");
                        break;
                    }
                }
            }

            return running;
        }

        public void SwapBuffers()
        {
            GLX.glXSwapBuffers(_display, _glxWindow);
        }

        public void Dispose()
        {
            GLX.glXMakeCurrent(_display, IntPtr.Zero, IntPtr.Zero);
            GLX.glXDestroyContext(_display, _openGlContext);
            GLX.glXDestroyWindow(_display, _glxWindow);
            X11.XDestroyWindow(_display, _x11Window);
            X11.XCloseDisplay(_display);
        }
    }
}
