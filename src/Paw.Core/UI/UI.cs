using Paw.Core.Assets;
using Paw.Core.Graphics;
using Paw.Core.Resources;
using System.Diagnostics;
using System.Numerics;

namespace Paw.Core.UI;

public class UI : IDisposable
{
    private const float _titleBarHeight = 20f;
    private const float _buttonBorderWidth = 1f;
    private const float _scrollableBorderWidth = 2f;

    private class ClipRect
    {
        public Vector2 Position;
        public Vector2 Size;

        public Vector2 Cursor;

        public readonly List<DrawCommand> DrawCommands = [];
        public readonly List<ClipRect> Children = [];
    }

    private readonly record struct DrawCommand(
        Vector2 Position,
        Vector2 Size,
        Vector3 Color,
        string? Text
    );

    private ClipRect _root = null!;
    private ClipRect _current = null!;
    private readonly Stack<ClipRect> _clipStack = [];

    private readonly DynamicGeometryRenderer2D _renderer;

    public UI(AssetManager assetManager)
    {
        _renderer = new DynamicGeometryRenderer2D(assetManager, ["ui_shapes", "ui_text"], ["font1"]);

        NextFrame();
    }

    public void Dispose()
    {
        _renderer.Dispose();
    }

    private void NextFrame()
    {
        // Root clip rect is always the entire window.
        // This means it can be used for overlays, etc.
        _root = new()
        {
            Position = new Vector2(0, 0),
            Size = new Vector2(1920, 1080),
        };

        _current = _root;
        _clipStack.Clear();
        _clipStack.Push(_root);
    }

    public void Update()
    {
    }

    public void Render(RenderContext context)
    {
        var dt = context.DeltaTime;
        var (width, height) = context.WindowSize;

        var mOrthoProj = Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, -1, 1);
        var mModel = Matrix4x4.Identity;
        var mView = Matrix4x4.Identity;
        var mvp = mModel * mView * mOrthoProj;

        var shapesWriter = _renderer.GetWriter("ui_shapes");
        var textWriter = _renderer.GetWriter("ui_text");
        var font = _renderer.GetFont("font1");

        ProcessClipRect(_root, Vector2.Zero, new Vector2(context.WindowSize.Item1, context.WindowSize.Item2));

        void ProcessClipRect(ClipRect clipRect, Vector2 outerPosition, Vector2 outerSize)
        {
            const float clipMargin = 5f;

            Vector2 clipRectPosition = outerPosition + clipRect.Position;

            Vector2 clippedPosition = clipRectPosition;
            clippedPosition += new Vector2(clipMargin, clipMargin);

            Vector2 clippedSize = clipRect.Size - new Vector2(2 * clipMargin, 2 * clipMargin);
            if (clipRectPosition.X + clippedSize.X > outerPosition.X + outerSize.X)
                clippedSize.X = outerPosition.X + outerSize.X - clipRectPosition.X;
            if (clipRectPosition.Y + clippedSize.Y > outerPosition.Y + outerSize.Y)
                clippedSize.Y = outerPosition.Y + outerSize.Y - clipRectPosition.Y;

            foreach (var drawCommand in clipRect.DrawCommands)
            {
                Vector2 p = clipRectPosition + drawCommand.Position;
                Vector2 s = drawCommand.Size;
                Vector3 c = drawCommand.Color;
                string? t = drawCommand.Text;

                shapesWriter.AddRectangleWithPositionSize(p, s, c);

                if (!string.IsNullOrWhiteSpace(t))
                    textWriter.AddText(font, p, 0.5f, t);
            }

            void SetupUniforms(Material material)
            {
                // gl_FragCoord: Pixel Coordinates with origin at bottom-left
                float clipMinX = clippedPosition.X;
                float clipMaxX = clippedPosition.X + clippedSize.X;

                // must flip y
                float clipMinY = height - (clippedPosition.Y + clippedSize.Y);
                float clipMaxY = height - clippedPosition.Y;

                material.SetUniform("uClipMin", new Vector2(clipMinX, clipMinY));
                material.SetUniform("uClipMax", new Vector2(clipMaxX, clipMaxY));
            }

            _renderer.Render(mvp, SetupUniforms);

            foreach (var childClipRect in clipRect.Children)
            {
                ProcessClipRect(childClipRect, clipRectPosition, clippedSize);
            }
        }

        NextFrame();
    }

    public bool BeginWindow(Vector2 size, string title)
    {
        var parentClipRect = _current;

        var newClipRect = new ClipRect()
        {
            Position = parentClipRect.Cursor,
            Size = size,
            Cursor = new Vector2(10f, 5f + _titleBarHeight + 5f + 10f),
        };

        // draw window on parent clip rect - not in new clip rect
        // border
        parentClipRect.DrawCommands.Add(new DrawCommand(parentClipRect.Cursor, size, new Vector3(0.2f), null));

        // title bar
        parentClipRect.DrawCommands.Add(new DrawCommand(
            parentClipRect.Cursor + new Vector2(5, 5),
            new Vector2(size.X - 10f, _titleBarHeight),
            new Vector3(0.2f),
            title));

        // window background
        parentClipRect.DrawCommands.Add(new DrawCommand(
            parentClipRect.Cursor + new Vector2(5, 5f + _titleBarHeight + 5f),
            new Vector2(size.X - 10f, size.Y - _titleBarHeight - 5f - 5f - 5f),
            new Vector3(0.4f),
            null));

        parentClipRect.Cursor.X += size.X + 10f;

        // Push new clip rect
        parentClipRect.Children.Add(newClipRect);
        _clipStack.Push(newClipRect);
        _current = newClipRect;

        return true;
    }

    public void EndWindow()
    {
        Debug.Assert(_clipStack.Count >= 2);

        var clipItem = _clipStack.Pop();
        _current = _clipStack.Peek();

        // ?
    }

    public void Overlay(string text)
    {
        //
    }

    public void Label(string text)
    {
        var size = new Vector2(200, 30);

        _current.DrawCommands.Add(new DrawCommand(_current.Cursor, size, new Vector3(0.5f), text));
        _current.Cursor += new Vector2(0, size.Y + 10f);
    }

    public bool Button(string text)
    {
        var size = new Vector2(200, 50);

        // border
        _current.DrawCommands.Add(new DrawCommand(_current.Cursor, size, new Vector3(0.2f), null));

        // fill
        _current.DrawCommands.Add(new DrawCommand(
            _current.Cursor + new Vector2(_buttonBorderWidth, _buttonBorderWidth),
            size - new Vector2(2 * _buttonBorderWidth, 2 * _buttonBorderWidth),
            new Vector3(0.5f, 0.2f, 0.2f),
            text));

        _current.Cursor += new Vector2(0, size.Y + 10f);

        return false;
    }

    public void BeginScrollable(Vector2 size) // TODO very similar to BeginWindow
    {
        var parentClipRect = _current;

        // Push new clip rect
        var newClipItem = new ClipRect()
        {
            Position = parentClipRect.Cursor,
            Size = size,
            Cursor = new Vector2(10, 10),
        };

        // draw scrollable on parent clip rect - not on new clip rect
        // border
        parentClipRect.DrawCommands.Add(new DrawCommand(parentClipRect.Cursor, size, new Vector3(0.2f), null));

        // content
        parentClipRect.DrawCommands.Add(new DrawCommand(
            parentClipRect.Cursor + new Vector2(_scrollableBorderWidth, _scrollableBorderWidth),
            size - new Vector2(2 * _scrollableBorderWidth, 2 * _scrollableBorderWidth),
            new Vector3(0.4f),
            null));

        parentClipRect.Cursor.Y += size.Y + 10f;

        // Push new clip rect
        parentClipRect.Children.Add(newClipItem);
        _clipStack.Push(newClipItem);
        _current = newClipItem;
    }

    public void EndScrollable()
    {
        Debug.Assert(_clipStack.Count >= 2);

        var clipItem = _clipStack.Pop();
        _current = _clipStack.Peek();
    }

    public void SetCursor(Vector2 position)
    {
        _current.Cursor = position;
    }
}

public class UiTestScene : Scene
{
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
    }

    private int _numFrames;
    private double _totalTime;
    private double _avgFramerate;

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

        UI.Label($"Hello");
        UI.Label($"World");
        UI.Label($"FPS: {_avgFramerate:F1}");

        //
        // window 1
        //

        UI.SetCursor(new Vector2(400, 200));

        if (UI.BeginWindow(new Vector2(500, 500), "window 1"))
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

            UI.BeginScrollable(new Vector2(200, 100));
            {
                UI.Label("label 3");
                UI.Label("label 4");
                if (UI.Button("button 3"))
                {
                    Console.WriteLine($"button 3");
                }
                if (UI.Button("button 4"))
                {
                    Console.WriteLine($"button 4");
                }
            }
            UI.EndScrollable();

            UI.Label("label 5");

            UI.BeginScrollable(new Vector2(1000, 100));
            {
                UI.Label("label 6");
                UI.Label("label 7");
                UI.Label("label 8");
                UI.Label("label 9");
            }
            UI.EndScrollable();

            UI.Label("label 10");

            UI.EndWindow();
        }

        //
        // window 2
        //

        if (UI.BeginWindow(new Vector2(300, 200), "window 2"))
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
        //
        //
        UI.Render(context);
    }
}
