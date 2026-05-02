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

    private const float _simpleLayoutSpacing = 5f; // horizontal/vertical layouts
    private const float _nestedControlSpacing = 5f; // window/scrollable
    private const float _tableSpacing = 5f;

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
    private readonly Vector4 _scrollableBackgroundColor = new(0.4f, 0.4f, 0.4f, 1.0f);




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
        public required int VertexOffset;
        public required int VertexCount;
    }

    private readonly record struct ClipEntry(Rect Rect)
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
            Null,
            Window,
            Scrollable,
            Vertical,
            Horizontal,
            Table,
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
                case ScopeType.Null: _ui.EndNullScope(); break;
                case ScopeType.Window: _ui.EndWindow(); break;
                case ScopeType.Scrollable: _ui.EndScrollable(); break;
                case ScopeType.Vertical: _ui.EndVertical(); break;
                case ScopeType.Horizontal: _ui.EndHorizontal(); break;
                case ScopeType.Table: _ui.EndTable(); break;
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
        public required Id Id;

        public Rect Rect;

        public readonly List<DrawCommand> DrawCommands = [];

        public required LinkedListNode<Id> ZOrderNode;

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

    // clipping
    private readonly Stack<ClipEntry> _clipStack = [];
    private ClipEntry _rootClipEntry;

    // id
    private readonly Stack<Id> _idStack = [];
    private Id _selectedControl = default;

    // windows
    private readonly Dictionary<Id, WindowState> _windowStates = [];
    private readonly LinkedList<Id> _windowZOrder = [];

    private readonly List<DrawCommand> _globalDrawCommands = [];
    private WindowState? _activeWindow;
    private bool _mouseBlockedByOtherWindow;

    private int _openScopeCount;

    // next window position
    public enum WindowPositionMode
    {
        Cascading,
        Center,
        Left,
        Right,
        Top,
        Bottom,
        Explicit,
    }
    private static readonly Vector2 _initialCascadingWindowPosition = new Vector2(250, 250);
    private static readonly Vector2 _maxCascadingWindowPosition = new Vector2(500, 500);
    private static readonly Vector2 _cascadingWindowOffset = new Vector2(40, 40);
    private WindowPositionMode _nextWindowPositionMode;
    private Vector2? _nextWindowExplicitOpeningPosition;
    private Vector2 _nextCascadingWindowPosition = _initialCascadingWindowPosition;
    private Vector2? _nextWindowExplicitPosition;

    // grabbing
    private enum GrabType
    {
        None,
        Move,
        Resize,
    }
    private GrabType _grabType;
    private Vector2 _grabOffset;

    // layout
    private enum LayoutMode
    {
        Vertical,
        Horizontal,
        Table,
    }
    private class LayoutItem
    {
        public readonly LayoutMode Mode;
        public Rect TotalRect;
        public Vector2 Cursor;
        public Vector2 MaxCursor;

        // table
        public float[]? ColumnWidths;
        public int Row;
        public int Column;
        public float MaxRowHeight;

        public LayoutItem(LayoutMode mode, Rect totalRect)
        {
            Mode = mode;
            TotalRect = totalRect;
            Cursor = totalRect.TopLeft;
            MaxCursor = totalRect.TopLeft;
        }

        public Rect GetRemainingSpace()
        {
            return new Rect(Cursor, TotalRect.BottomRight);
        }

        public Vector2 GetAvailableSize()
        {
            return TotalRect.BottomRight - Cursor;
        }

        public Vector2 AdjustSize(Vector2 requestedSize)
        {
            switch (Mode)
            {
                case LayoutMode.Vertical:
                {
                    var remainingSpace = TotalRect.BottomRight - Cursor;
                    return new Vector2(TotalRect.Size.X, Math.Min(remainingSpace.Y, requestedSize.Y));
                }

                case LayoutMode.Horizontal:
                {
                    var remainingSpace = TotalRect.BottomRight - Cursor;
                    return new Vector2(Math.Min(remainingSpace.X, requestedSize.X), TotalRect.Size.Y);
                }

                case LayoutMode.Table:
                {
                    var colWidth = GetCurrentColumnWidth();
                    return new Vector2(colWidth, requestedSize.Y);
                }

                default: throw new NotImplementedException();
            }
        }

        public Rect Layout(Vector2 size)
        {
            var pos = Cursor;
            var rect = new Rect(pos, pos + size);

            switch (Mode)
            {
                case LayoutMode.Vertical:
                    Cursor.Y += size.Y + _simpleLayoutSpacing;
                    break;

                case LayoutMode.Horizontal:
                    Cursor.X += size.X + _simpleLayoutSpacing;
                    break;

                case LayoutMode.Table:
                {
                    MaxRowHeight = Math.Max(MaxRowHeight, size.Y);
                    break;
                }

                default: throw new NotImplementedException();
            }

            MaxCursor = Vector2.Max(MaxCursor, rect.BottomRight);

            return rect;
        }

        public float GetCurrentColumnWidth()
        {
            if (ColumnWidths is null)
                throw new InvalidOperationException("Not a table");
            if (Column < 0 || Column >= ColumnWidths.Length)
                throw new InvalidOperationException("Invalid Column index");

            return ColumnWidths[Column];
        }
    }

    private readonly Stack<LayoutItem> _layoutItems = [];

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

        foreach (var id in _windowZOrder)
        {
            var windowState = _windowStates[id];

            for (int i = 0; i < windowState.DrawCommands.Count; i++)
            {
                ProcessDrawCommand(windowState.DrawCommands[i]);
            }
        }

        for (int i = 0; i < _globalDrawCommands.Count; i++)
        {
            ProcessDrawCommand(_globalDrawCommands[i]);
        }

        void ProcessDrawCommand(DrawCommand drawCommand)
        {
            int x = (int)drawCommand.Clip.Min.X;
            int y = (int)drawCommand.Clip.Min.Y;
            int w = Math.Max(0, (int)(drawCommand.Clip.Max.X - drawCommand.Clip.Min.X));
            int h = Math.Max(0, (int)(drawCommand.Clip.Max.Y - drawCommand.Clip.Min.Y));

            // flip y because scissor(0, 0) is bottom left
            y = height - y - h;

            _gl.Scissor(x, y, w, h);
            _vertexArray.Draw(GL.PrimitiveType.TRIANGLES, drawCommand.VertexOffset, drawCommand.VertexCount);
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
        if (_activeWindow is not null)
            throw new InvalidOperationException($"EndWindow was not called");
        if (_layoutItems.Count > 1)
            throw new InvalidOperationException($"Layout stack was not cleaned up on end of frame. Items left: {_layoutItems.Count}");

        _vertices.Clear();
        _globalDrawCommands.Clear();

        foreach (var (_, windowState) in _windowStates)
        {
            windowState.DrawCommands.Clear();
        }

        _rootClipEntry = new ClipEntry()
        {
            Rect = new Rect(new Vector2(0, 0), new Vector2(windowWidth, windowHeight)),
        };
        _clipStack.Clear();
        _clipStack.Push(_rootClipEntry);

        _idStack.Clear();
        _idStack.Push(Id.Create("root"));

        _activeWindow = null;
        _mouseBlockedByOtherWindow = false;

        _nextWindowPositionMode = default;
        _nextWindowExplicitOpeningPosition = null;
        _nextWindowExplicitPosition = null;

        _layoutItems.Clear();
        PushLayoutItem(LayoutMode.Vertical, new Rect(new Vector2(0, 0), new Vector2(windowWidth, windowHeight)));
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

        List<DrawCommand> drawCommands = _activeWindow is not null
            ? _activeWindow.DrawCommands
            : _globalDrawCommands;

        // try to merge
        if (drawCommands.Count > 0)
        {
            DrawCommand mostRecent = drawCommands[^1];

            if (mostRecent.Clip == clipEntry.Rect)
            {
                mostRecent.VertexCount += vertexCount;
                return;
            }
        }

        // new command
        drawCommands.Add(new DrawCommand()
        {
            Clip = clipEntry.Rect,
            VertexOffset = _vertices.Count - vertexCount, // xxx not sure
            VertexCount = vertexCount,
        });
    }

    private void PushClipEntry(Rect clipRect)
    {
        var outerClipEntry = _clipStack.Peek();
        var innerClipEntry = new ClipEntry()
        {
            Rect = new Rect(
                Vector2.Max(outerClipEntry.Rect.Min, clipRect.TopLeft),
                Vector2.Min(outerClipEntry.Rect.Max, clipRect.BottomRight)
                ),
        };

        _clipStack.Push(innerClipEntry);
    }

    private void PopClipEntry()
    {
        if (_clipStack.Count < 2)
            throw new InvalidOperationException("Unbalanced Begin/End calls to clip stack");

        _ = _clipStack.Pop();
    }

    private LayoutItem PushLayoutItem(LayoutMode mode, Rect totalRect)
    {
        var item = new LayoutItem(mode, totalRect);

        _layoutItems.Push(item);

        return item;
    }

    private LayoutItem PopLayoutItem()
    {
        if (_layoutItems.Count < 2)
            throw new InvalidOperationException("Unbalanced Push/Pop calls to layout stack");

        return _layoutItems.Pop();
    }

    private Rect Layout(Vector2 size)
    {
        return _layoutItems.Peek().Layout(size);
    }

    private Vector2 AdjustSize(Vector2 size)
    {
        return _layoutItems.Peek().AdjustSize(size);
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

    private void MoveWindowToFront(Id id)
    {
        if (!_windowStates.TryGetValue(id, out WindowState? windowState))
            throw new InvalidOperationException($"Window '{id}' not found in window states dict");

        var node = windowState.ZOrderNode;
        _windowZOrder.Remove(node);
        _windowZOrder.AddLast(node);
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

    public void SetNextWindowPositionMode(WindowPositionMode mode, Vector2? explicitPosition = null)
    {
        if ((mode == WindowPositionMode.Explicit) != (explicitPosition is not null))
            throw new ArgumentException($"Incorrect usage of explicit position");

        _nextWindowPositionMode = mode;
        _nextWindowExplicitOpeningPosition = explicitPosition;
    }

    public void SetNextWindowPosition(Vector2 position)
    {
        if (_nextWindowExplicitPosition is not null)
            throw new InvalidOperationException("Next window position already set");

        _nextWindowExplicitPosition = position;
    }

    private Vector2 GetNewWindowPosition(Vector2 initialSize)
    {
        switch (_nextWindowPositionMode)
        {
            case WindowPositionMode.Cascading:
            {
                Vector2 p = _nextCascadingWindowPosition;
                _nextCascadingWindowPosition += _cascadingWindowOffset;

                if (_nextCascadingWindowPosition.X > _maxCascadingWindowPosition.X || _nextCascadingWindowPosition.Y > _maxCascadingWindowPosition.Y)
                    _nextCascadingWindowPosition = _initialCascadingWindowPosition;

                return p;
            }

            case WindowPositionMode.Center:
            {
                Vector2 screenSize = _rootClipEntry.Rect.Size;
                Vector2 p = new Vector2(screenSize.X * 0.5f - initialSize.X * 0.5f, screenSize.Y * 0.5f - initialSize.Y * 0.5f);
                return p;
            }

            case WindowPositionMode.Left:
            {
                Vector2 screenSize = _rootClipEntry.Rect.Size;
                Vector2 p = new Vector2(0f, screenSize.Y * 0.5f - initialSize.Y * 0.5f);
                return p;
            }

            case WindowPositionMode.Right:
            {
                Vector2 screenSize = _rootClipEntry.Rect.Size;
                Vector2 p = new Vector2(screenSize.X - initialSize.X, screenSize.Y * 0.5f - initialSize.Y * 0.5f);
                return p;
            }

            case WindowPositionMode.Top:
            {
                Vector2 screenSize = _rootClipEntry.Rect.Size;
                Vector2 p = new Vector2(screenSize.X * 0.5f - initialSize.X * 0.5f, 0f);
                return p;
            }

            case WindowPositionMode.Bottom:
            {
                Vector2 screenSize = _rootClipEntry.Rect.Size;
                Vector2 p = new Vector2(screenSize.X * 0.5f - initialSize.X * 0.5f, screenSize.Y - initialSize.Y);
                return p;
            }

            case WindowPositionMode.Explicit:
            {
                if (_nextWindowExplicitOpeningPosition is null)
                    throw new InvalidOperationException($"{nameof(_nextWindowExplicitOpeningPosition)} not set");

                Vector2 p = _nextWindowExplicitOpeningPosition.Value;
                _nextWindowExplicitOpeningPosition = null;
                return p;
            }

            default: throw new NotImplementedException();
        }
    }

    public Scope BeginWindow(Vector2 initialSize, string title)
    {
        if (_activeWindow is not null)
            throw new InvalidOperationException("BeginWindow called while a window is active");

        Id id = PushId(title);

        // Get / create window state
        if (!_windowStates.TryGetValue(id, out WindowState? windowState))
        {
            Vector2 openingPosition = GetNewWindowPosition(initialSize);

            _windowStates.Add(id, windowState = new WindowState()
            {
                Id = id,
                Rect = new Rect(openingPosition, openingPosition + initialSize),
                ZOrderNode = _windowZOrder.AddLast(id),
            });
        }
        _activeWindow = windowState;

        // Check if mouse is blocked by other window
        _mouseBlockedByOtherWindow = false;
        {
            LinkedListNode<Id>? currentNode = windowState.ZOrderNode.Next;
            while (currentNode is not null)
            {
                if (IsMouseWithin(_windowStates[currentNode.Value].Rect))
                {
                    _mouseBlockedByOtherWindow = true;
                    break;
                }
                currentNode = currentNode.Next;
            }
        }

        if (IsMouseWithin(_activeWindow.Rect) && _mouseState.WasPressed(MouseButton.Left) && !_mouseBlockedByOtherWindow)
        {
            MoveWindowToFront(_activeWindow.Id);
        }

        // Handle window movement/resize
        var windowRect = windowState.Rect;
        var titleRect = windowRect.FromTopLeft(new Vector2(windowRect.Size.X, _titleBarHeight));
        var resizeRect = windowRect.FromBottomRight(new Vector2(15));

        if (IsMouseWithin(titleRect) && _mouseState.WasPressed(MouseButton.Left) && !_mouseBlockedByOtherWindow)
        {
            _selectedControl = id;

            _grabType = GrabType.Move;
            _grabOffset = _mousePosition - windowRect.Min;
        }
        else if (IsMouseWithin(resizeRect) && _mouseState.WasPressed(MouseButton.Left) && !_mouseBlockedByOtherWindow)
        {
            _selectedControl = id;

            _grabType = GrabType.Resize;
            _grabOffset = windowRect.Max - _mousePosition;
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

        if (_nextWindowExplicitPosition is not null)
        {
            Vector2 p = _nextWindowExplicitPosition.Value;
            windowState.Rect = EnsureRectIsOnScreenByMoving(new Rect(p, p + windowState.Rect.Size));
            _nextWindowExplicitPosition = null;
        }

        if (_selectedControl == id && !_mouseState.Get(MouseButton.Left))
        {
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

        // update rects after resize/move
        windowRect = windowState.Rect;
        titleRect = new Rect(windowRect.Min, windowRect.Min + new Vector2(windowRect.Size.X, _titleBarHeight));
        resizeRect = windowRect.FromBottomRight(new Vector2(15));

        // color highlights
        var titleBarColor = _windowTitleBarColor;
        if (IsMouseWithin(titleRect) && !_mouseBlockedByOtherWindow)
        {
            titleBarColor += new Vector4(0.05f, 0.05f, 0.05f, 0);
        }
        var resizeRectColor = _windowBorderColor;
        if (IsMouseWithin(resizeRect) && !_mouseBlockedByOtherWindow)
        {
            resizeRectColor = new Vector4(1, 1, 1, 0) - resizeRectColor;
            resizeRectColor.W = 1;
        }

        // Emit geometry
        int vertexCount = 0;
        vertexCount += EmitBoxWithBorder(windowRect, _windowBorderColor, _windowBackgroundColor);
        vertexCount += EmitBoxWithBorder(titleRect, _windowBorderColor, titleBarColor);
        vertexCount += EmitTextVerts(titleRect.TopLeft, _windowTitleTextColor, title);
        vertexCount += EmitTriangleBottomRight(resizeRect, resizeRectColor); // TODO draw in EndWindow so it's always on top

        AddDrawCommand(vertexCount);

        PushClipEntry(new Rect(
            windowRect.TopLeft + new Vector2(_nestedControlSpacing),
            windowRect.BottomRight - new Vector2(_nestedControlSpacing)));

        Rect contentRect = new Rect(windowState.Rect.TopLeft + new Vector2(_nestedControlSpacing, _titleBarHeight + _nestedControlSpacing),
                                    windowState.Rect.BottomRight - new Vector2(_nestedControlSpacing));

        PushLayoutItem(LayoutMode.Vertical, contentRect);

        _openScopeCount++;
        return new Scope(this, Scope.ScopeType.Window, true);
    }

    private void EndWindow()
    {
        if (_activeWindow is null)
            throw new InvalidOperationException("EndWindow called while no window is active");

        _activeWindow = null;

        _openScopeCount--;

        PopLayoutItem();
        PopClipEntry();
        PopId();
    }

    public Scope BeginScrollable(Vector2 size) // TODO enable/disable scroll-axis x/y
    {
        size = AdjustSize(size);

        if (size.X <= 0 || size.Y <= 0)
        {
            _openScopeCount++;
            return new Scope(this, Scope.ScopeType.Null, false);
        }

        var rect = Layout(size);

        int vertexCount = 0;
        vertexCount += EmitBoxWithBorder(rect, _scrollableBorderColor, _scrollableBackgroundColor);

        AddDrawCommand(vertexCount);

        PushClipEntry(new Rect(
            rect.TopLeft + new Vector2(_nestedControlSpacing),
            rect.BottomRight - new Vector2(_nestedControlSpacing)));

        var contentRect = new Rect(rect.TopLeft + new Vector2(_nestedControlSpacing), new Vector2(10_000, 10_000)); // TODO max value?

        PushLayoutItem(LayoutMode.Vertical, contentRect);

        _openScopeCount++;
        return new Scope(this, Scope.ScopeType.Scrollable, true);
    }

    private void EndScrollable()
    {
        PopLayoutItem();

        _openScopeCount--;
        PopClipEntry();
    }

    public Scope BeginVertical(float? maxWidth = null)
    {
        Rect rect = _layoutItems.Peek().GetRemainingSpace();

        if (maxWidth is not null && rect.Size.X > maxWidth.Value)
        {
            rect = new Rect(rect.TopLeft, rect.BottomLeft + new Vector2(maxWidth.Value, 0));
        }

        _ = PushLayoutItem(LayoutMode.Vertical, rect);

        _openScopeCount++;
        return new Scope(this, Scope.ScopeType.Vertical, true);
    }

    private void EndVertical()
    {
        if (_layoutItems.Peek().Mode != LayoutMode.Vertical)
            throw new InvalidOperationException("Unbalanced BeginVertical/EndVertical");

        // consume space in parent layout
        var vertical = PopLayoutItem();
        var consumedSize = vertical.MaxCursor - vertical.TotalRect.TopLeft;
        _ = Layout(consumedSize);

        _openScopeCount--;
    }

    public Scope BeginHorizontal(float? maxHeight = null)
    {
        Rect rect = _layoutItems.Peek().GetRemainingSpace();

        if (maxHeight is not null && rect.Size.Y > maxHeight.Value)
        {
            rect = new Rect(rect.TopLeft, rect.TopRight + new Vector2(0, maxHeight.Value));
        }

        _ = PushLayoutItem(LayoutMode.Horizontal, rect);

        _openScopeCount++;
        return new Scope(this, Scope.ScopeType.Horizontal, true);
    }

    private void EndHorizontal()
    {
        if (_layoutItems.Peek().Mode != LayoutMode.Horizontal)
            throw new InvalidOperationException("Unbalanced BeginHorizontal/EndHorizontal");

        // consume space in parent layout
        var horizontal = PopLayoutItem();
        var consumedSize = horizontal.MaxCursor - horizontal.TotalRect.TopLeft;
        _ = Layout(consumedSize);

        _openScopeCount--;
    }

    /// <summary>
    /// Define column widths:<br />
    /// width &gt; 1: size in pixels<br />
    /// 0 &lt; width &lt;= 1: fraction of remaining size<br />
    /// </summary>
    public Scope BeginTable(params float[] columnWidths)
    {
        if (columnWidths.Length < 1)
            throw new ArgumentException("Column widths not specified", nameof(columnWidths));

        // error checking without allocations
        {
            var sumOfFractions = 0f;

            for (var i = 0; i < columnWidths.Length; i++)
            {
                var w = columnWidths[i];
                if (w <= 0f)
                    throw new ArgumentOutOfRangeException(nameof(columnWidths), "Column widths must be greater than zero");

                if (!float.IsFinite(w))
                    throw new ArgumentOutOfRangeException(nameof(columnWidths), "Column widths must be finite (not NaN, Inf, etc)");

                if (IsFractionalColumnWidth(w))
                    sumOfFractions += w;
            }

            if (sumOfFractions > 1f)
                throw new ArgumentOutOfRangeException(nameof(columnWidths), "Sum of column fractions must not be greater than one");
        }

        Rect rect = _layoutItems.Peek().GetRemainingSpace();

        // calculate column widths in pixels
        var actualColumnWidths = new float[columnWidths.Length];
        var alreadyConsumedWidth = _tableSpacing * (columnWidths.Length - 1);
        for (var i = 0; i < columnWidths.Length; i++)
        {
            if (columnWidths[i] > 1f)
                alreadyConsumedWidth += columnWidths[i];
        }
        var remainingWidth = MathF.Max(0f, rect.Size.X - alreadyConsumedWidth);

        for (var i = 0; i < columnWidths.Length; i++)
        {
            actualColumnWidths[i] = IsFractionalColumnWidth(columnWidths[i])
                ? columnWidths[i] * remainingWidth
                : columnWidths[i];
        }

        // create table layout item
        var item = PushLayoutItem(LayoutMode.Table, rect);

        item.ColumnWidths = actualColumnWidths;
        item.Row = 0;
        item.Column = 0;
        item.MaxRowHeight = 0;

        _openScopeCount++;
        return new Scope(this, Scope.ScopeType.Table, true);

        static bool IsFractionalColumnWidth(float columnWidth) => columnWidth <= 1f;
    }

    private void EndTable()
    {
        if (_layoutItems.Peek().Mode != LayoutMode.Table)
            throw new InvalidOperationException("Unbalanced BeginTable/EndTable");

        var table = PopLayoutItem();
        var consumedSize = table.MaxCursor - table.TotalRect.TopLeft;
        _ = Layout(consumedSize);

        _openScopeCount--;
    }

    public void NextColumn()
    {
        if (_layoutItems.Peek().Mode != LayoutMode.Table)
            throw new InvalidOperationException("NextColumn called without active table");

        var table = _layoutItems.Peek();

        if (table.Column >= table.ColumnWidths!.Length - 1)
            throw new InvalidOperationException("No more columns in table");

        var colWidth = table.GetCurrentColumnWidth();

        table.Column++;
        table.Cursor += new Vector2(colWidth + _tableSpacing, 0);
    }

    public void NextRow()
    {
        if (_layoutItems.Peek().Mode != LayoutMode.Table)
            throw new InvalidOperationException("NextRow called without active table");

        var table = _layoutItems.Peek();

        table.Row++;
        table.Column = 0;
        table.Cursor = new Vector2(table.TotalRect.TopLeft.X, table.Cursor.Y + table.MaxRowHeight + _tableSpacing);
        table.MaxRowHeight = 0;
    }

    public void Overlay(string text)
    {
        var size = MeasureTextLine(text);
        var rect = Layout(size);

        int vertexCount = 0;
        vertexCount += EmitQuad(rect, _overlayBackgroundColor);
        vertexCount += EmitTextVerts(rect.TopLeft, _overlayTextColor, text);

        AddDrawCommand(vertexCount);
    }

    public void Label(string text)
    {
        var size = MeasureTextLine(text);
        size = AdjustSize(size);

        if (size.X <= 0 || size.Y <= 0)
            return;

        var rect = Layout(size);

        // geometry
        int vertexCount = 0;
        vertexCount += EmitQuad(rect, _labelBackgroundColor);
        vertexCount += EmitTextVerts(rect.TopLeft, _labelTextColor, text);

        AddDrawCommand(vertexCount);
    }

    public bool Button(string text)
    {
        var size = MeasureTextLine(text) + new Vector2(5, 5);
        size = AdjustSize(size);

        if (size.X <= 0 || size.Y <= 0)
            return false;

        var rect = Layout(size);

        // input
        var wasPressed = false;
        Vector4 backgroundColor = _buttonBackgroundColor;

        if (IsMouseWithin(rect) && !_mouseBlockedByOtherWindow)
        {
            backgroundColor += new Vector4(0.1f, 0.1f, 0.1f, 0.0f);
            wasPressed = _mouseState.WasPressed(MouseButton.Left);
        }

        // geometry
        int vertexCount = 0;
        vertexCount += EmitBoxWithBorder(rect, _buttonBorderColor, backgroundColor);
        vertexCount += EmitTextVerts(rect.TopLeft + new Vector2(2, 0), _buttonTextColor, text);

        AddDrawCommand(vertexCount);

        return wasPressed;
    }

    public bool Input(ref string value)
    {
        var size = new Vector2(200, 22);
        size = AdjustSize(size);

        if (size.X <= 0 || size.Y <= 0)
            return false;

        var rect = Layout(size);

        // input
        Id id = _idStack.Peek();

        var wasPressed = false;
        Vector4 backgroundColor = _inputBackgroundColor;

        if (IsMouseWithin(rect) && !_mouseBlockedByOtherWindow)
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

        // geometry
        int vertexCount = 0;
        vertexCount += EmitBoxWithBorder(rect, _inputBorderColor, backgroundColor);
        vertexCount += EmitTextVerts(rect.TopLeft + new Vector2(2, 0), _inputTextColor, valueToShow);

        AddDrawCommand(vertexCount);

        return false;
    }

    private void EndNullScope()
    {
        _openScopeCount--;
    }

    #endregion

    #region Composite controls

    public bool Input(string label, ref string value)
    {
        bool r;

        using (BeginTable(0.5f, 0.5f))
        {
            Label(label);
            NextColumn();

            PushId(label);
            r = Input(ref value);
            PopId();
        }

        return r;
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
