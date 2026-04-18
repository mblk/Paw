using Paw.Core.Assets;
using Paw.Core.Graphics;
using Paw.Core.Platforms;
using Paw.Core.Resources;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Paw.Core.UI;

public unsafe class UI : IDisposable
{
    private const float _borderWidth = 1f;

    private const float _titleBarHeight = 20f;

    private const float _textScale = 0.666f;

    private readonly Vector2 _textMargin = new(3, 3);

    private readonly Vector4 _windowBorderColor = new(0.1f, 0.1f, 0.1f, 1.0f);
    private readonly Vector4 _windowTitleBarColor = new(0.2f, 0.2f, 0.2f, 1.0f);
    private readonly Vector4 _windowTitleTextColor = new(1.0f, 1.0f, 1.0f, 1.0f);
    private readonly Vector4 _windowBackgroundColor = new(0.3f, 0.3f, 0.3f, 1.0f);

    private readonly Vector4 _overlayBackgroundColor = new(0.0f);
    private readonly Vector4 _overlayTextColor = new(1.0f, 1.0f, 1.0f, 1.0f);

    private readonly Vector4 _labelBackgroundColor = new(0.5f, 0.5f, 0.5f, 1.0f);
    private readonly Vector4 _labelTextColor = new(1.0f, 1.0f, 1.0f, 1.0f);

    private readonly Vector4 _buttonBorderColor = new(0.1f, 0.1f, 0.1f, 1.0f);
    private readonly Vector4 _buttonBackgroundColor = new(0.5f, 0.2f, 0.2f, 1.0f);
    private readonly Vector4 _buttonTextColor = new(1.0f, 1.0f, 1.0f, 1.0f);

    private readonly Vector4 _inputBorderColor = new(0.1f, 0.1f, 0.1f, 1.0f);
    private readonly Vector4 _inputBackgroundColor = new(0.2f, 0.2f, 0.5f, 1.0f);
    private readonly Vector4 _inputTextColor = new(1.0f, 1.0f, 1.0f, 1.0f);

    private readonly Vector4 _scrollableBorderColor = new(0.1f, 0.1f, 0.1f, 1.0f);
    private readonly Vector4 _scrollableBackgroundColor = new(0.5f, 0.5f, 0.5f, 1.0f);




    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public Vector2 Position;
        public Vector4 Color;
        public Vector2 UV;
    }

    private class DrawCommand
    {
        public required Rect Clip;
        public required int VertexCount;
    }

    private readonly record struct ClipEntry(Rect Rect, Vector2 NextCursor)
    {
    }

    private readonly record struct Rect(Vector2 Min, Vector2 Max)
    {
        public Vector2 TopLeft => Min;
        public Vector2 BottomRight => Max;
        public Vector2 TopRight => new(Max.X, Min.Y);
        public Vector2 BottomLeft => new(Min.X, Max.Y);

        public Vector2 Size => Max - Min;

        public bool Contains(Vector2 p)
        {
            return Min.X <= p.X && p.X <= Max.X &&
                   Min.Y <= p.Y && p.Y <= Max.Y;
        }

        public Rect FromTopLeft(Vector2 size)
        {
            return new Rect(Min, Min + size);
        }

        public Rect FromBottomRight(Vector2 size)
        {
            return new Rect(Max - size, Max);
        }
    }

    public readonly ref struct Scope : IDisposable
    {
        internal enum ScopeType
        {
            Window,
            Scrollable,
        }

        private readonly UI _ui;
        private readonly ScopeType _scopeType;

        public readonly bool IsOpen;

        internal Scope(UI ui, ScopeType scopeType, bool isOpen)
        {
            _ui = ui;
            _scopeType = scopeType;
            IsOpen = isOpen;
        }

        public void Dispose()
        {
            switch (_scopeType)
            {
                case ScopeType.Window: _ui.EndWindow(); break;
                case ScopeType.Scrollable: _ui.EndScrollable(); break;
                default: throw new NotImplementedException();
            }
        }
    }

    private readonly record struct Id(int HC)
    {
#if DEBUG
        public required string Path { get; init; }
#endif

        public static Id Create(string s)
        {
            int hc = s.GetHashCode();

            return new Id(hc)
#if DEBUG
            {
                Path = s,
            }
#endif
            ;
        }

        public Id Combine(string s)
        {
            int hc = s.GetHashCode();
            int hc2 = HashCode.Combine(this.HC, hc);

            return new Id(hc2)
#if DEBUG
            {
                Path = $"{this.Path}/{s}",
            }
#endif
            ;
        }
    }

    private class WindowState
    {
        public Rect Rect;

        // TODO draw commands
        // TODO open closed
    }


    public class Stats
    {
        public int VertexCount { get; set; }
        public int DrawCalls { get; set; }
    }

    private readonly Stats _stats = new();





    //
    // ui state
    //

    private readonly List<DrawCommand> _drawCommands = [];

    private readonly Stack<ClipEntry> _clipStack = [];
    private ClipEntry _rootClipEntry;

    private readonly Stack<Id> _idStack = [];
    private Id _selectedControl = default;

    private readonly Dictionary<Id, WindowState> _windowStates = [];

    private int _openScopeCount;

    private Vector2 _cursor;

    private enum GrabType
    {
        None,
        Move,
        Resize,
    }
    private GrabType _grabType;
    private Vector2 _grabOffset;


    //
    // rendering
    //

    private readonly GL _gl;
    private readonly Font _font;
    private readonly Material _material;

    private readonly BufferObject _vertexBuffer;
    private readonly VertexArrayObject<Vertex> _vertexArray;

    private const int _initialVertexBufferSize = 1024;
    private readonly List<Vertex> _vertices = new(_initialVertexBufferSize);
    // TODO should we also use element buffers?
    //private readonly List<uint> _indices = new(_initialVertexBufferSize);



    //
    // input state snapshot
    //

    private readonly KeyboardState _keyboardState = new();
    private readonly MouseState _mouseState = new();
    private bool _keyboardInputConsumed;
    private bool _mouseInputConsumed;
    private Vector2 _mousePosition;
    private Vector2 _mouseDelta;
    private bool _mouseMoved;



    public UI(AssetManager assetManager)
    {
        _gl = assetManager.GL;
        _font = assetManager.LoadFont("font2");
        _material = assetManager.LoadMaterial("ui");

        _vertexBuffer = new BufferObject(assetManager.GL);
        _vertexBuffer.SetSizeAndUsage(sizeof(Vertex) * _initialVertexBufferSize, GL.BufferUsage.STREAM_DRAW);
        _vertexArray = new VertexArrayObject<Vertex>(assetManager.GL, _vertexBuffer);

        NextFrame(1920, 1080);
    }

    public void Dispose()
    {
        _vertexArray.Dispose();
        _vertexBuffer.Dispose();
    }

    public void Update(UpdateContext context)
    {
        context.Input.Keyboard.GetSnapshot(_keyboardState);
        context.Input.Mouse.GetSnapshot(_mouseState);

        _keyboardInputConsumed = false;
        _mouseInputConsumed = false;

        var prevMousePosition = _mousePosition;
        _mousePosition = new Vector2(_mouseState.X, _mouseState.Y);
        _mouseDelta = _mousePosition - prevMousePosition;
        _mouseMoved = _mouseDelta != default;
    }

    public void Render(RenderContext context)
    {
        var dt = context.DeltaTime;
        var (width, height) = context.WindowSize;

        var mOrthoProj = Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, -1, 1);
        var mModel = Matrix4x4.Identity;
        var mView = Matrix4x4.Identity;
        var mvp = mModel * mView * mOrthoProj;

        int totalDrawCalls = 0;
        int totalVertexCount = _vertices.Count;


        _vertexBuffer.SetData(_vertices, GL.BufferUsage.STREAM_DRAW);

        _vertexArray.Bind();

        _material.SetUniform("uMVP", mvp);
        _material.Bind();

        _gl.Enable(GL.EnableCap.BLEND);
        _gl.BlendFunc(GL.BlendingFactor.SRC_ALPHA, GL.BlendingFactor.ONE_MINUS_SRC_ALPHA);

        _gl.Enable(GL.EnableCap.SCISSOR_TEST);

        int vertexOffset = 0;

        for (int i = 0; i < _drawCommands.Count; i++)
        {
            var drawCommand = _drawCommands[i];

            int x = (int)drawCommand.Clip.Min.X;
            int y = (int)drawCommand.Clip.Min.Y;
            int w = Math.Max(0, (int)(drawCommand.Clip.Max.X - drawCommand.Clip.Min.X));
            int h = Math.Max(0, (int)(drawCommand.Clip.Max.Y - drawCommand.Clip.Min.Y));

            // flip y because scissor(0, 0) is bottom left
            y = height - y - h;

            _gl.Scissor(x, y, w, h);

            _vertexArray.Draw(GL.PrimitiveType.TRIANGLES, vertexOffset, drawCommand.VertexCount);

            vertexOffset += drawCommand.VertexCount;
            totalDrawCalls++;
        }

        _gl.Disable(GL.EnableCap.SCISSOR_TEST);

        _gl.Disable(GL.EnableCap.BLEND);

        _material.Unbind();

        _vertexArray.Unbind();


        NextFrame(width, height);
        _stats.VertexCount = totalVertexCount;
        _stats.DrawCalls = totalDrawCalls;
    }

    private void NextFrame(int windowWidth, int windowHeight)
    {
        if (_openScopeCount != 0)
            throw new InvalidOperationException($"Unbalanced UI scopes: {_openScopeCount} scopes were not disposed");
        if (_clipStack.Count > 1)
            throw new InvalidOperationException($"Clip stack was not cleaned up on end of frame. Items left: {_clipStack.Count}");
        if (_idStack.Count > 1)
            throw new InvalidOperationException($"ID stack was not cleaned up on end of frame. Items left: {_idStack.Count}");

        _vertices.Clear();
        _drawCommands.Clear();

        _rootClipEntry = new ClipEntry()
        {
            Rect = new Rect(new Vector2(0, 0), new Vector2(windowWidth, windowHeight)),
            NextCursor = new Vector2(0, 0),
        };
        _clipStack.Clear();
        _clipStack.Push(_rootClipEntry);

        _idStack.Clear();
        _idStack.Push(Id.Create("root"));

        _cursor = new Vector2(0, 0);
    }

    #region Geometry emission

    private int EmitQuad(Rect rect, Vector4 color)
    {
        Vector2 tl = rect.TopLeft;
        Vector2 tr = rect.TopRight;
        Vector2 bl = rect.BottomLeft;
        Vector2 br = rect.BottomRight;

        Vector2 uv = new(2f, 2f); // magic uv coord: always white

        _vertices.Add(new Vertex() { Position = tl, Color = color, UV = uv });
        _vertices.Add(new Vertex() { Position = tr, Color = color, UV = uv });
        _vertices.Add(new Vertex() { Position = bl, Color = color, UV = uv });

        _vertices.Add(new Vertex() { Position = bl, Color = color, UV = uv });
        _vertices.Add(new Vertex() { Position = tr, Color = color, UV = uv });
        _vertices.Add(new Vertex() { Position = br, Color = color, UV = uv });

        return 6;
    }

    private int EmitTriangleBottomRight(Rect rect, Vector4 color)
    {
        Vector2 tr = rect.TopRight;
        Vector2 bl = rect.BottomLeft;
        Vector2 br = rect.BottomRight;

        Vector2 uv = new(2f, 2f); // magic uv coord: always white

        _vertices.Add(new Vertex() { Position = bl, Color = color, UV = uv });
        _vertices.Add(new Vertex() { Position = tr, Color = color, UV = uv });
        _vertices.Add(new Vertex() { Position = br, Color = color, UV = uv });

        return 3;
    }

    private int EmitBoxWithBorder(Rect rect, Vector4 borderColor, Vector4 fillColor)
    {
        int vertexCount = 0;
        vertexCount += EmitQuad(rect, borderColor);
        vertexCount += EmitQuad(new Rect(rect.Min + new Vector2(_borderWidth), rect.Max - new Vector2(_borderWidth)), fillColor);
        return vertexCount;
    }

    private int EmitTextVerts(Vector2 position, Vector4 color, string text)
    {
        int vertexCount = 0;

        Vector2 currentPosition = position + _textMargin;

        foreach (char c in text)
        {
            if (!_font.MetaData.Characters.TryGetValue(c, out var charData))
            {
                Console.WriteLine($"char data {(uint)c} '{c}' not found");
                continue;
            }

            if (c == ' ')
            {
                currentPosition.X += charData.XAdvance * _textScale;
                continue;
            }

            Vector2 uvMin = charData.UvMin;
            Vector2 uvMax = charData.UvMax;
            Vector2 size = charData.Size * _textScale;
            Vector2 offset = charData.Offset * _textScale;

            float xl = currentPosition.X + offset.X;
            float xr = xl + size.X;

            float yt = currentPosition.Y + offset.Y;
            float yb = yt + size.Y;

            Vector2 tl = new(xl, yt);
            Vector2 tr = new(xr, yt);
            Vector2 br = new(xr, yb);
            Vector2 bl = new(xl, yb);

            _vertices.Add(new Vertex { Position = bl, Color = color, UV = new(uvMin.X, uvMax.Y) });
            _vertices.Add(new Vertex { Position = br, Color = color, UV = new(uvMax.X, uvMax.Y) });
            _vertices.Add(new Vertex { Position = tr, Color = color, UV = new(uvMax.X, uvMin.Y) });
            _vertices.Add(new Vertex { Position = bl, Color = color, UV = new(uvMin.X, uvMax.Y) });
            _vertices.Add(new Vertex { Position = tr, Color = color, UV = new(uvMax.X, uvMin.Y) });
            _vertices.Add(new Vertex { Position = tl, Color = color, UV = new(uvMin.X, uvMin.Y) });
            vertexCount += 6;

            currentPosition.X += charData.XAdvance * _textScale;
        }

        return vertexCount;
    }

    private Vector2 MeasureTextLine(string text)
    {
        Vector2 currentPosition = _textMargin * 2;
        float maxHeight = 0;

        foreach (char c in text)
        {
            if (!_font.MetaData.Characters.TryGetValue(c, out var charData))
            {
                Console.WriteLine($"char data {(uint)c} '{c}' not found");
                continue;
            }

            if (c == ' ')
            {
                currentPosition.X += charData.XAdvance * _textScale;
                continue;
            }

            float yb = currentPosition.Y + _font.MetaData.LineBase * _textScale;

            maxHeight = Math.Max(maxHeight, yb);

            currentPosition.X += charData.XAdvance * _textScale;
        }

        return new Vector2(currentPosition.X, maxHeight);
    }

    #endregion

    private void AddDrawCommand(int vertexCount)
    {
        var clipEntry = _clipStack.Peek();

        // try to merge
        if (_drawCommands.Count > 0)
        {
            DrawCommand mostRecent = _drawCommands[^1];

            if (mostRecent.Clip == clipEntry.Rect)
            {
                mostRecent.VertexCount += vertexCount;
                return;
            }
        }

        // new command
        _drawCommands.Add(new DrawCommand()
        {
            Clip = clipEntry.Rect,
            VertexCount = vertexCount,
        });
    }

    private void PushClipEntry(Vector2 size)
    {
        var outerClipEntry = _clipStack.Peek();
        var innerClipEntry = new ClipEntry()
        {
            Rect = new Rect(
                Vector2.Max(outerClipEntry.Rect.Min, _cursor + new Vector2(1)),
                Vector2.Min(outerClipEntry.Rect.Max, _cursor + size - new Vector2(2))
                ),
            NextCursor = _cursor + new Vector2(0, size.Y + 10),
        };

        _clipStack.Push(innerClipEntry);
    }

    private void PopClipEntry()
    {
        if (_clipStack.Count < 2)
            throw new InvalidOperationException("Unbalanced Begin/End calls to clip stack");

        var clipEntry = _clipStack.Pop();

        _cursor = clipEntry.NextCursor;
    }

    private bool IsMouseWithin(Rect rect)
    {
        var topClipEntry = _clipStack.Peek();

        if (!topClipEntry.Rect.Contains(_mousePosition))
            return false;

        if (!rect.Contains(_mousePosition))
            return false;

        return true;
    }

    private Rect EnsureRectIsOnScreenByMoving(Rect rect)
    {
        var pos = rect.Min;
        var size = rect.Size;

        if (pos.X < 0)
            pos.X = 0;

        if (pos.Y < 0)
            pos.Y = 0;

        Rect root = _rootClipEntry.Rect;

        if (pos.X + size.X > root.Max.X)
            pos.X = root.Max.X - size.X;

        if (pos.Y + size.Y > root.Max.Y)
            pos.Y = root.Max.Y - size.Y;

        return new Rect(pos, pos + size);
    }

    private Rect EnsureRectIsOnScreenByResizing(Rect rect)
    {
        var pos = rect.Min;
        var size = rect.Size;

        if (pos.X < 0)
            pos.X = 0;

        if (pos.Y < 0)
            pos.Y = 0;

        Rect root = _rootClipEntry.Rect;

        if (pos.X + size.X > root.Max.X)
            size.X = root.Max.X - pos.X;

        if (pos.Y + size.Y > root.Max.Y)
            size.Y = root.Max.Y - pos.Y;

        return new Rect(pos, pos + size);
    }

    private Id PushId(string s)
    {
        var id = Id.Create(s);
        _idStack.Push(id);
        return id;
    }

    private void PopId()
    {
        if (_idStack.Count < 1)
            throw new InvalidOperationException("Unbalanced Begin/End calls to ID stack");

        _idStack.Pop();
    }

    #region Controls implementation/API

    public Scope BeginWindow(Vector2 initialSize, string title)
    {
        Id id = PushId(title);

        if (!_windowStates.TryGetValue(id, out WindowState? windowState))
        {
            _windowStates.Add(id, windowState = new WindowState()
            {
                Rect = new Rect(_cursor, _cursor + initialSize),
            });
        }

        var windowRect = windowState.Rect;
        var titleRect = windowRect.FromTopLeft(new Vector2(windowRect.Size.X, _titleBarHeight));
        var resizeRect = windowRect.FromBottomRight(new Vector2(15));

        //xxx
        if (IsMouseWithin(titleRect) && _mouseState.WasPressed(MouseButton.Left))
        {
            _selectedControl = id;

            _grabType = GrabType.Move;
            _grabOffset = _mousePosition - windowRect.Min;

            Console.WriteLine($"start moving window (offset {_grabOffset})");
        }
        else if (IsMouseWithin(resizeRect) && _mouseState.WasPressed(MouseButton.Left))
        {
            _selectedControl = id;

            _grabType = GrabType.Resize;
            _grabOffset = windowRect.Max - _mousePosition;

            Console.WriteLine($"start resize window (offset {_grabOffset})");
        }

        if (_selectedControl == id && _mouseMoved)
        {
            if (_grabType == GrabType.Move)
            {
                var currentSize = windowState.Rect.Size;
                var newPos = _mousePosition - _grabOffset;

                windowState.Rect = new Rect(newPos, newPos + currentSize);
            }
            else if (_grabType == GrabType.Resize)
            {
                var min = windowState.Rect.Min;
                var newMax = _mousePosition + _grabOffset;

                var newSize = newMax - min;

                if (newSize.X < 100) newSize.X = 100;
                if (newSize.Y < 100) newSize.Y = 100;

                newMax = min + newSize;

                windowState.Rect = EnsureRectIsOnScreenByResizing(new Rect(min, newMax));
            }
        }

        if (_selectedControl == id && !_mouseState.Get(MouseButton.Left))
        {
            Console.WriteLine($"stop moving/resize window");

            _selectedControl = default;

            _grabType = default;
            _grabOffset = default;
        }

        // make sure window is always inside root clip bounds (also after game window resize)
        Rect fixedRect = EnsureRectIsOnScreenByMoving(windowState.Rect);

        if (fixedRect != windowState.Rect)
        {
            windowState.Rect = fixedRect;
        }

        // update rects
        windowRect = windowState.Rect;
        titleRect = new Rect(windowRect.Min, windowRect.Min + new Vector2(windowRect.Size.X, _titleBarHeight));
        resizeRect = windowRect.FromBottomRight(new Vector2(15));

        // color highlights
        var titleBarColor = _windowTitleBarColor;
        if (IsMouseWithin(titleRect))
        {
            titleBarColor += new Vector4(0.05f, 0.05f, 0.05f, 0);
        }
        var resizeRectColor = _windowBorderColor;
        if (IsMouseWithin(resizeRect))
        {
            resizeRectColor = new Vector4(1, 1, 1, 0) - resizeRectColor;
            resizeRectColor.W = 1;
        }
        //xxx

        _cursor = windowState.Rect.Min;
        int vertexCount = 0;
        vertexCount += EmitBoxWithBorder(windowRect, _windowBorderColor, _windowBackgroundColor);
        vertexCount += EmitBoxWithBorder(titleRect, _windowBorderColor, titleBarColor);
        vertexCount += EmitTextVerts(_cursor, _windowTitleTextColor, title);
        vertexCount += EmitTriangleBottomRight(resizeRect, resizeRectColor); // TODO draw in EndWindow so it's always on top

        AddDrawCommand(vertexCount); // before pushing new clip entry!

        PushClipEntry(windowRect.Size);

        _cursor += new Vector2(10f, _titleBarHeight + 10f); // change cursor last!

        _openScopeCount++;
        return new Scope(this, Scope.ScopeType.Window, true);
    }

    private void EndWindow()
    {
        _openScopeCount--;
        PopClipEntry();
        PopId();
    }

    public void Overlay(string text)
    {
        var size = MeasureTextLine(text);
        var rect = new Rect(_cursor, _cursor + size);

        int vertexCount = 0;
        vertexCount += EmitQuad(rect, _overlayBackgroundColor);
        vertexCount += EmitTextVerts(_cursor, _overlayTextColor, text);

        AddDrawCommand(vertexCount);

        _cursor.Y += size.Y;
    }

    public void Label(string text)
    {
        var size = MeasureTextLine(text);
        var rect = new Rect(_cursor, _cursor + size);

        int vertexCount = 0;
        vertexCount += EmitQuad(rect, _labelBackgroundColor);
        vertexCount += EmitTextVerts(_cursor, _labelTextColor, text);

        AddDrawCommand(vertexCount);

        _cursor.Y += size.Y + 5;
    }

    public bool Button(string text)
    {
        var size = new Vector2(100, 20);
        var rect = new Rect(_cursor, _cursor + size);

        //xxx
        var wasPressed = false;
        Vector4 backgroundColor = _buttonBackgroundColor;

        if (IsMouseWithin(rect))
        {
            backgroundColor += new Vector4(0.1f, 0.1f, 0.1f, 0.0f);
            wasPressed = _mouseState.WasPressed(MouseButton.Left);
        }
        //xxx

        int vertexCount = 0;
        vertexCount += EmitBoxWithBorder(rect, _buttonBorderColor, backgroundColor);
        vertexCount += EmitTextVerts(_cursor, _buttonTextColor, text);

        AddDrawCommand(vertexCount);

        _cursor.Y += size.Y + 5;

        return wasPressed;
    }

    public bool Input(string label, ref string value)
    {
        Id id = _idStack.Peek().Combine(label);

        var size = new Vector2(100, 20);
        var rect = new Rect(_cursor, _cursor + size);

        //xxx
        var wasPressed = false;
        Vector4 backgroundColor = _inputBackgroundColor;

        if (IsMouseWithin(rect))
        {
            backgroundColor += new Vector4(0.1f, 0.1f, 0.1f, 0.0f);
            wasPressed = _mouseState.WasPressed(MouseButton.Left);

            if (_selectedControl != id && wasPressed)
            {
                _selectedControl = id;
            }
        }

        string valueToShow = value;

        if (_selectedControl == id)
        {
            if (_keyboardState.NumChars > 0)
            {
                char c = _keyboardState.Chars[0];

                if (c == 8) // backspace
                {
                    if (value.Length > 0)
                    {
                        value = value[0..^1];
                    }
                }
                else if (c == 9) // tab
                {
                    _selectedControl = default;
                }
                else if (c == 10 || c == 13) // enter
                {
                    _selectedControl = default;
                }
                else if (!char.IsControl(c))
                {
                    value += c;
                }
            }

            backgroundColor += new Vector4(0.1f, 0.1f, 0.1f, 0.0f);
            valueToShow = value + "_";
        }
        //xxx

        int vertexCount = 0;
        vertexCount += EmitBoxWithBorder(rect, _inputBorderColor, backgroundColor);
        vertexCount += EmitTextVerts(_cursor, _inputTextColor, valueToShow);
        vertexCount += EmitTextVerts(_cursor + new Vector2(size.X + 5, 0), _labelTextColor, label);

        AddDrawCommand(vertexCount);

        _cursor.Y += size.Y + 5;

        return false;
    }

    public Scope BeginScrollable(Vector2 size)
    {
        var rect = new Rect(_cursor, _cursor + size);

        int vertexCount = 0;
        vertexCount += EmitBoxWithBorder(rect, _scrollableBorderColor, _scrollableBackgroundColor);

        AddDrawCommand(vertexCount); // before pushing new clip entry!

        PushClipEntry(size);

        _cursor += new Vector2(10, 10); // change cursor last!

        _openScopeCount++;
        return new Scope(this, Scope.ScopeType.Scrollable, true);
    }

    private void EndScrollable()
    {
        _openScopeCount--;
        PopClipEntry();
    }

    public void SetCursor(Vector2 position)
    {
        _cursor = position;
    }

    public void Horizontal()
    {
        // ...
    }

    public void Vertical()
    {
        // ...
    }

    #endregion

    #region Debugging

    public void ShowDebuggingOverlay()
    {
        Overlay($"UI vertices: {_stats.VertexCount}");
        Overlay($"UI draw calls: {_stats.DrawCalls}");

        Overlay($"Mouse: {_mouseState.X} {_mouseState.Y}");

        Overlay($"Selected: {_selectedControl}");
    }

    #endregion
}
