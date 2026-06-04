using Paw.Core.Graphics;
using System.Runtime.CompilerServices;

namespace Paw.Core.Platforms;

public static class PlatformFactory
{
    public static IPlatform CreatePlatform(PlatformOptions options)
    {
        if (OperatingSystem.IsWindows())
        {
            return new Windows.WindowsPlatform(options);
        }

        if (OperatingSystem.IsLinux())
        {
            var xdgSessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE_");
            if (string.Equals(xdgSessionType, "wayland", StringComparison.OrdinalIgnoreCase))
            {
                return new LinuxWayland.LinuxWaylandPlatform(options);
            }
            else
            {
                return new LinuxX11.LinuxX11Platform(options);
            }
        }

        throw new PlatformNotSupportedException("Unsupported OS platform.");
    }
}

public record PlatformOptions();
public record WindowOptions(int Width, int Height, string Title, int SwapInterval = 1); // 0=vsync off, 1=vsync on

public interface IPlatform : IDisposable
{
    IWindow CreateWindow(WindowOptions options);
}

public interface IWindow : IDisposable
{
    GL GL { get; }
    (int, int) Size { get; }

    bool ProcessEvents();
    void SwapBuffers();

    IInput Input { get; }
}

public interface IInput
{
    IKeyboard Keyboard { get; }
    IMouse Mouse { get; }
}

public enum Key : uint
{
    // Control keys
    Escape, Enter, Space, Tab, Backspace,
    Left, Right, Up, Down,
    Insert, Delete, Home, End, PageUp, PageDown,

    LControl, RControl, LShift, RShift, LAlt, RAlt,
    LWin, RWin, Menu,

    // ...
    Minus,
    Equals,
    Semicolon,
    Apostrophe,
    Grave,
    Backslash,
    Comma,
    Period,
    Slash,

    // Letters
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    // Top row digits
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,

    // Numpad
    NumLock, NumDivide, NumMultiply, NumSubtract, NumAdd, NumEnter, NumDecimal,
    Num0, Num1, Num2, Num3, Num4, Num5, Num6, Num7, Num8, Num9,

    // Function keys
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

    // Other keys
    CapsLock, ScrollLock, PrintScreen, Pause,


    MaxValue
}

public enum MouseButton : uint
{
    Left,
    Middle,
    Right,
    // what about all the special keys (back, forward, etc?)

    MaxValue,
}

public interface IKeyboard
{
    bool Get(Key key);
    bool WasPressed(Key key); // edge
    bool WasReleased(Key key); // edge

    Key? GetFirstPressedKey();

    void GetSnapshot(KeyboardState state);
}

public interface IMouse
{
    int X { get; }
    int Y { get; }
    int WheelDelta { get; }

    bool Get(MouseButton key);
    bool WasPressed(MouseButton key); // edge
    bool WasReleased(MouseButton key); // edge

    void GetSnapshot(MouseState state);
}

public class KeyboardState
{
    public readonly bool[] CurrStates = new bool[(int)Key.MaxValue];
    public readonly bool[] PrevStates = new bool[(int)Key.MaxValue];

    public readonly char[] Chars = new char[8];
    public int NumChars = 0;

    public bool Get(Key key)
    {
        int idx = GetIndex(key);
        return CurrStates[idx];
    }

    public bool WasPressed(Key key)
    {
        int idx = GetIndex(key);
        bool wasPressed = CurrStates[idx] && !PrevStates[idx];
        //PrevStates[idx] = CurrStates[idx];
        return wasPressed;
    }

    public bool WasReleased(Key key)
    {
        int idx = GetIndex(key);
        bool wasReleased = !CurrStates[idx] && PrevStates[idx];
        //PrevStates[idx] = CurrStates[idx];
        return wasReleased;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetIndex(Key key)
    {
        return (int)key;
    }
}

public class MouseState
{
    public readonly bool[] CurrStates = new bool[(int)MouseButton.MaxValue];
    public readonly bool[] PrevStates = new bool[(int)MouseButton.MaxValue];

    public int X;
    public int Y;
    public int WheelDelta;

    public bool Get(MouseButton button)
    {
        int idx = GetIndex(button);
        return CurrStates[idx];
    }

    public bool WasPressed(MouseButton button)
    {
        int idx = GetIndex(button);
        bool wasPressed = CurrStates[idx] && !PrevStates[idx];
        //PrevStates[idx] = CurrStates[idx];
        return wasPressed;
    }

    public bool WasReleased(MouseButton button)
    {
        int idx = GetIndex(button);
        bool wasReleased = !CurrStates[idx] && PrevStates[idx];
        //PrevStates[idx] = CurrStates[idx];
        return wasReleased;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetIndex(MouseButton button)
    {
        return (int)button;
    }
}