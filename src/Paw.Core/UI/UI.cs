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

    private const float _textScale = 0.8f;

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

    private class ClipEntry
    {
        public required Rect Rect;
        public required Vector2 NextCursor;
    }

    private readonly record struct Rect(Vector2 Min, Vector2 Max)
    {
        public bool Contains(Vector2 p)
        {
            return Min.X <= p.X && p.X <= Max.X &&
                   Min.Y <= p.Y && p.Y <= Max.Y;
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

    public class Stats
    {
        public int VertexCount { get; set; }
        public int DrawCalls { get; set; }
    }

    public Stats Statistics { get; private set; } = new Stats();




    private const int _initialVertexBufferSize = 1024;

    private readonly List<Vertex> _vertices = new(_initialVertexBufferSize);

    // TODO should we also use element buffers?
    //private readonly List<uint> _indices = new(_initialVertexBufferSize);

    private readonly List<DrawCommand> _drawCommands = [];

    private readonly Stack<ClipEntry> _clipStack = [];

    private int _openScopeCount;

    private Vector2 _cursor;




    private readonly GL _gl;
    private readonly Font _font;
    private readonly Material _material;

    private readonly BufferObject _vertexBuffer;
    private readonly VertexArrayObject<Vertex> _vertexArray;


    // input state snapshot
    private readonly KeyboardState _keyboardState = new();
    private readonly MouseState _mouseState = new();



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
        Statistics.VertexCount = totalVertexCount;
        Statistics.DrawCalls = totalDrawCalls;
    }

    private void NextFrame(int windowWidth, int windowHeight)
    {
        if (_openScopeCount != 0)
            throw new InvalidOperationException($"Unbalanced UI scopes: {_openScopeCount} scopes were not disposed");
        if (_clipStack.Count > 1)
            throw new InvalidOperationException($"Clip stack was not cleaned up on end of frame. Items left: {_clipStack.Count}");

        _vertices.Clear();
        _drawCommands.Clear();

        _clipStack.Clear();
        _clipStack.Push(new ClipEntry()
        {
            Rect = new Rect(new Vector2(0, 0), new Vector2(windowWidth, windowHeight)),
            NextCursor = new Vector2(0, 0),
        });

        _cursor = new Vector2(0, 0);
    }

    private int EmitQuad(Rect rect, Vector4 color)
    {
        Vector2 tl = rect.Min;
        Vector2 tr = new(rect.Max.X, rect.Min.Y);
        Vector2 bl = new(rect.Min.X, rect.Max.Y);
        Vector2 br = rect.Max;

        Vector2 uv = new(2f, 2f); // magic uv coord

        _vertices.Add(new Vertex() { Position = tl, Color = color, UV = uv });
        _vertices.Add(new Vertex() { Position = tr, Color = color, UV = uv });
        _vertices.Add(new Vertex() { Position = bl, Color = color, UV = uv });

        _vertices.Add(new Vertex() { Position = bl, Color = color, UV = uv });
        _vertices.Add(new Vertex() { Position = tr, Color = color, UV = uv });
        _vertices.Add(new Vertex() { Position = br, Color = color, UV = uv });

        return 6;
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
            float yb = currentPosition.Y + _font.MetaData.LineBase * _textScale;

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
        {
            throw new InvalidOperationException("Unbalanced Begin/End calls to clip stack");
        }

        var clipEntry = _clipStack.Pop();

        _cursor = clipEntry.NextCursor;
    }

    private bool IsMouseWithin(Rect rect)
    {
        var mp = new Vector2(_mouseState.X, _mouseState.Y);

        var topClipEntry = _clipStack.Peek();

        if (!topClipEntry.Rect.Contains(mp))
            return false;

        if (!rect.Contains(mp))
            return false;

        return true;
    }


    public Scope BeginWindow(Vector2 size, string title)
    {
        var windowRect = new Rect(_cursor, _cursor + size);
        var titleRect = new Rect(_cursor, _cursor + new Vector2(size.X, _titleBarHeight));

        int vertexCount = 0;
        vertexCount += EmitBoxWithBorder(windowRect, _windowBorderColor, _windowBackgroundColor);
        vertexCount += EmitBoxWithBorder(titleRect, _windowBorderColor, _windowTitleBarColor);
        vertexCount += EmitTextVerts(_cursor, _windowTitleTextColor, title);

        AddDrawCommand(vertexCount); // before pushing new clip entry!

        PushClipEntry(size);

        _cursor += new Vector2(10f, _titleBarHeight + 10f); // change cursor last!

        _openScopeCount++;
        return new Scope(this, Scope.ScopeType.Window, true);
    }

    private void EndWindow()
    {
        _openScopeCount--;
        PopClipEntry();
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
}

public class UiTestScene : Scene
{
    private int _mouseX;
    private int _mouseY;

    private int _numFrames;
    private double _totalTime;
    private double _avgFramerate;


    private UI UI { get; set; } = null!;


    public UiTestScene(SceneContext context)
        : base(context)
    {
    }

    public override void Load()
    {
        UI = new UI(AssetManager);
    }

    public override void Unload()
    {
        UI.Dispose();
    }

    public override void Update(UpdateContext context)
    {
        if (context.Input.Keyboard.WasPressed(Platforms.Key.Escape))
        {
            context.SceneController.RequestExit();
        }

        // TODO add Input to RenderContext?
        _mouseX = context.Input.Mouse.X;
        _mouseY = context.Input.Mouse.Y;

        UI.Update(context);
    }

    public override void Render(RenderContext context)
    {
        _totalTime += context.DeltaTime;
        _numFrames++;
        if (_totalTime > 0.25)
        {
            _avgFramerate = 1.0 / (_totalTime / _numFrames);
            _totalTime = 0;
            _numFrames = 0;
        }

        //
        // overlays
        //
        UI.SetCursor(new Vector2(0, 0));

        UI.Overlay($"Hello");
        UI.Overlay($"World");
        UI.Overlay($"FPS: {_avgFramerate:F1}");

        UI.Overlay($"UI vertices: {UI.Statistics.VertexCount}");
        UI.Overlay($"UI draw calls: {UI.Statistics.DrawCalls}");

        UI.Overlay($"Mouse: {_mouseX} {_mouseY}");

        //
        // window 1
        //

        UI.SetCursor(new Vector2(400, 200));

        using (var window = UI.BeginWindow(new Vector2(500, 500), "window 1"))
        {
            UI.Label("label 1");
            UI.Label("label 2");

            if (UI.Button("button 1"))
            {
                Console.WriteLine($"button 1");
            }
            if (UI.Button("button 2"))
            {
                Console.WriteLine($"button 2");
            }

            using (UI.BeginScrollable(new Vector2(200, 100)))
            {
                UI.Label("label 1.1");
                UI.Label("label 1.2");
                if (UI.Button("button 1.3"))
                {
                    Console.WriteLine($"button 1.3");
                }
                if (UI.Button("button 1.4"))
                {
                    Console.WriteLine($"button 1.4");
                }
                UI.Label("label 1.5");
                UI.Label("label 1.6");
                if (UI.Button("button 1.7"))
                {
                    Console.WriteLine($"button 1.7");
                }
            }

            UI.Label("label 5");

            using (UI.BeginScrollable(new Vector2(1000, 100)))
            {
                UI.Label("label 6");
                UI.Label("label 7");
                UI.Label("label 8");
                UI.Label("label 9");
            }

            UI.Label("label 10");
        }

        //
        // window 2
        //

        UI.SetCursor(new Vector2(1000, 200));

        using (var window = UI.BeginWindow(new Vector2(300, 200), "window 2"))
        {
            if (UI.Button("button 10"))
            {
                Console.WriteLine($"button 10");
            }
            if (UI.Button("button 11"))
            {
                Console.WriteLine($"button 11");
            }
        }

        //
        // Window 3
        //

        //UI.SetCursor(new Vector2(_mouseX, _mouseY));

        //using (var window = UI.BeginWindow(new Vector2(100, 100), "window 3"))
        //{
        //    //
        //}

        //
        //
        //
        UI.Render(context);
    }
}
