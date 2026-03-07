using Paw.Core.Engine;

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

public interface IKeyboard
{
    bool Get(Key key);
    bool WasPressed(Key key); // edge
    bool WasReleased(Key key); // edge
}

public interface IMouse
{
    // ...
}
