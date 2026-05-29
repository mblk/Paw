using Paw.Core.Graphics;
using Paw.Core.Platforms;
using Paw.Core.Resources;
using Paw.Core.Utils;

namespace Paw.Core.Physics;

public class PhysicsTestScene : Scene
{
    private readonly Solver _solver = new();

    private DynamicGeometryRenderer2D _renderer = null!;



    private int _cameraZoom;
    private vec2 _cameraPos;

    private bool _movingCamera;
    private vec2 _cameraGrabPos;

    private bool _showAxis = true;
    private bool _showForces = true;

    private vec2 _mouseScreen;


    public enum NewBodyType
    {
        SquareSmall,
        SquareBig,
        RectVert,
        RectHor,
    }
    private NewBodyType _newBodyType;
    private readonly NewBodyType[] _allNewBodyTypes = Enum.GetValues<NewBodyType>();


    private Body? _selectedBody;


    private bool _simulate = true;
    private bool _singleStep = false;
    private int _tickCount = 0;



    public PhysicsTestScene(SceneContext context)
        : base(context)
    {
    }

    public override void Load()
    {
        _renderer = new DynamicGeometryRenderer2D(AssetManager, ["default", "font1"], ["font1"]);
    }

    public override void Unload()
    {
    }

    public override void Update(UpdateContext context)
    {
        var dt = context.DeltaTime;
        var kb = context.Input.Keyboard;

        if (kb.WasPressed(Key.Escape))
        {
            context.SceneController.RequestSceneChange("menu");
        }

        if (_simulate || _singleStep)
        {
            _singleStep = false;
            _solver.Step();
            _tickCount++;
        }

        _mouseScreen = new vec2(context.Input.Mouse.X, context.Input.Mouse.Y);
    }

    private readonly float[] _startStopTableCols = [0.33f, 0.33f, 0.33f];

    public override void Render(RenderContext context)
    {
        float dt = context.DeltaTime;
        (int windowWidth, int windowHeight) = context.WindowSize;

        //
        // UI
        //

        UI.SetNextWindowPositionMode(Core.UI.UI.WindowPositionMode.Left);
        using (UI.BeginWindow(new Vector2(300, 800), "Physics test"))
        {
            //ReadOnlySpan<float> cols = stackalloc float[] { 0.33f, 0.33f, 0.33f }; // TODO allocs, but why?

            using (UI.BeginTable(_startStopTableCols))
            {
                if (UI.Button("Start"))
                {
                    _simulate = true;
                }
                UI.NextColumn();
                if (UI.Button("Stop"))
                {
                    _simulate = false;
                }
                UI.NextColumn();
                if (UI.Button("Step"))
                {
                    _singleStep = true;
                }
            }

            UI.Label(Format($"Ticks: {_tickCount}"));

            //
            UI.Label("Camera:");

            UI.Label(Format($"Zoom: {_cameraZoom}"));
            UI.Label(Format($"Pos: {_cameraPos.X:F2} {_cameraPos.Y:F2}"));

            if (UI.Button("Reset camera"))
            {
                _cameraPos = default;
                _cameraZoom = 0;
            }

            //
            UI.Label("Render:");
            UI.Checkbox("Show axis", ref _showAxis);
            UI.Checkbox("Show forces", ref _showForces);

            UI.Label(Format($"Bodies: {_solver.Bodies.Count}"));
            UI.Label(Format($"Forces: {_solver.Forces.Count}"));

            //
            UI.Label("Params:");
            UI.Input("Iterations", ref _solver.Iterations);
            UI.Input("Alpha", ref _solver.Alpha);
            UI.Input("Beta", ref _solver.Beta);
            UI.Input("Gamma", ref _solver.Gamma);
            UI.Checkbox("Post stabilize", ref _solver.PostStabilize);
            //UI.Checkbox("Gravity", ref _solver.PostStabilize);

            if (UI.Button("Reset params"))
            {
                _solver.SetDefaultConfig();
            }

            UI.Radiobuttons<NewBodyType>("New body:", ref _newBodyType, _allNewBodyTypes);
        }

        if (_selectedBody is not null)
        {
            UI.SetNextWindowPositionMode(Core.UI.UI.WindowPositionMode.Right);
            using (UI.BeginWindow(new Vector2(300, 800), "Selected Body"))
            {
                UI.Label(Format($"Position: {_selectedBody.Position.X} {_selectedBody.Position.Y} {_selectedBody.Position.Z}"));
                UI.Label(Format($"Velocity: {_selectedBody.Velocity.X} {_selectedBody.Velocity.Y} {_selectedBody.Velocity.Z}"));
                UI.Label(Format($"Size: {_selectedBody.Size.X} {_selectedBody.Size.Y}"));
                UI.Label(Format($"Mass: {_selectedBody.Mass}"));
                UI.Label(Format($"Moment: {_selectedBody.Moment}"));
                UI.Label(Format($"Friction: {_selectedBody.Friction}"));
                UI.Label(Format($"Radius: {_selectedBody.Radius}"));

                UI.Label(Format($"Forces: {_selectedBody.Forces.Count}"));

                foreach (var force in _selectedBody.Forces)
                {
                    UI.Label(Format($"Force {force.GetType().Name}"));

                    for (int i = 0; i < force.Rows; i++)
                    {
                        UI.Label(Format($"C[{i}]: {force.C[i]}"));
                        UI.Label(Format($"Penalty[{i}]: {force.Penalty[i]}"));
                        UI.Label(Format($"Lambda[{i}]: {force.Lambda[i]}"));
                    }
                }
            }
        }

        bool mouseOverUI = UI.IsMouseOverAnyWindow();

        //
        // camera input (must be done before creating mView)
        //

        _cameraZoom -= context.Input.Mouse.WheelDelta;

        f32 ratio = (f32)windowWidth / (f32)windowHeight;
        f32 visibleWidth = 50f * MathF.Pow(1.25f, _cameraZoom);
        f32 visibleHeight = visibleWidth / ratio;

        mat4 mProj = mat4.CreateOrthographic(visibleWidth, visibleHeight, -1f, 1f);

        if (!mat4.Invert(mProj, out mat4 mInvProj))
            throw new InvalidOperationException("can't invert projection matrix");

        vec2 mouseNDC = _mouseScreen / new vec2(windowWidth, -windowHeight) * 2f + new vec2(-1f, 1f);
        vec2 mouseView = vec4.Transform(new vec4(mouseNDC, 0f, 1f), mInvProj).XY;

        if (context.Input.Mouse.WasPressed(MouseButton.Right) && !mouseOverUI)
        {
            _movingCamera = true;
            _cameraGrabPos = _cameraPos + mouseView;
        }

        if (!context.Input.Mouse.Get(MouseButton.Right) || mouseOverUI)
        {
            _movingCamera = false;
            _cameraGrabPos = default;
        }

        if (_movingCamera)
        {
            _cameraPos = _cameraGrabPos - mouseView;
        }

        //
        // setup projection
        //

        mat4 mModel = mat4.Identity;
        mat4 mView = mat4.CreateTranslation(-_cameraPos.X, -_cameraPos.Y, 0f);
        mat4 mvp = mModel * mView * mProj;

        mat4 viewProj = mView * mProj;

        if (!mat4.Invert(viewProj, out mat4 mInvViewProj))
            throw new InvalidOperationException("can't invert view+projection matrix");

        vec2 mouseWorld = vec4.Transform(new vec4(mouseNDC, 0f, 1f), mInvViewProj).XY;

        //
        // input
        //

        if (context.Input.Keyboard.Get(Key.A)) _cameraPos.X -= 10.0f * dt;
        if (context.Input.Keyboard.Get(Key.D)) _cameraPos.X += 10.0f * dt;
        if (context.Input.Keyboard.Get(Key.W)) _cameraPos.Y += 10.0f * dt;
        if (context.Input.Keyboard.Get(Key.S)) _cameraPos.Y -= 10.0f * dt;

        if (_solver.Pick(mouseWorld, out Body? pickedBody, out vec2 pickedLocalPos))
        {
            //
        }

        if (pickedBody is not null)
        {
            if (context.Input.Mouse.WasPressed(MouseButton.Left) && !mouseOverUI)
            {
                _selectedBody = pickedBody;
            }
        }
        else
        {
            if (context.Input.Mouse.WasPressed(MouseButton.Left) && !mouseOverUI)
            {
                var newSize = GetNewBodySize();

                var newBody = new Body(
                    size: newSize,
                    density: 1.0f,
                    friction: 0.5f,
                    position: new Vector3(mouseWorld.X, mouseWorld.Y, 0),
                    velocity: vec3.Zero
                    );

                _solver.Bodies.Add(newBody);

                _selectedBody = newBody;
            }
        }

        if (context.Input.Mouse.WasPressed(MouseButton.Right) && !mouseOverUI)
        {
            _selectedBody = null;
        }

        if (_selectedBody is not null)
        {
            if (context.Input.Mouse.Get(MouseButton.Middle) && !mouseOverUI)
            {
                _selectedBody.Position = new vec3(mouseWorld, _selectedBody.Position.Z);
                _selectedBody.Velocity = vec3.Zero;
            }
        }

        //
        // content
        //

        var font = _renderer.GetFont("font1");
        var defaultWriter = _renderer.GetWriter("default");
        var fontWriter = _renderer.GetWriter("font1");

        const f32 fontScale = 0.01f;

        void AddText(vec2 pos, ReadOnlySpan<char> text)
        {
            fontWriter.AddText(font, pos, vec4.One, fontScale, text, true);
        }

        if (_showAxis)
        {
            var axisColor = new Vector4(1, 1, 1, 1);

            defaultWriter.AddLine(new Vector2(-10, 0), new Vector2(10, 0), axisColor);
            defaultWriter.AddLine(new Vector2(0, -10), new Vector2(0, 10), axisColor);

            AddText(new Vector2(-10, 0), "-10");
            AddText(new Vector2(+10, 0), "+10");
            AddText(new Vector2(0, -10), "-10");
            AddText(new Vector2(0, +10), "+10");
        }

        var bodyColor = new Vector4(0.6f, 0.3f, 0.3f, 1f);
        var selectedBodyColor = new Vector4(0.7f, 0.4f, 0.4f, 1f);
        var borderColor = new vec4(1, 1, 1, 1);
        var previewColor = new vec4(1, 0, 1, 1);

        foreach (var body in _solver.Bodies)
        {
            vec2 pos = body.Position.XY;
            float angle = body.Position.Z;

            mat2 rot = mat2.Rotation(angle);

            vec2 halfSize = body.Size * 0.5f;

            vec2 p1 = rot * new vec2(-halfSize.X, -halfSize.Y) + pos; // bottom left
            vec2 p2 = rot * new vec2(+halfSize.X, -halfSize.Y) + pos; // bottom right
            vec2 p3 = rot * new vec2(+halfSize.X, +halfSize.Y) + pos; // top right
            vec2 p4 = rot * new vec2(-halfSize.X, +halfSize.Y) + pos; // top left

            var fillColor = (pickedBody == body || _selectedBody == body) ? selectedBodyColor : bodyColor;

            defaultWriter.AddVertex(new DynamicGeometryRenderer2D.Vertex() { Position = p1, Color = fillColor, UV = default, });
            defaultWriter.AddVertex(new DynamicGeometryRenderer2D.Vertex() { Position = p2, Color = fillColor, UV = default, });
            defaultWriter.AddVertex(new DynamicGeometryRenderer2D.Vertex() { Position = p3, Color = fillColor, UV = default, });

            defaultWriter.AddVertex(new DynamicGeometryRenderer2D.Vertex() { Position = p1, Color = fillColor, UV = default, });
            defaultWriter.AddVertex(new DynamicGeometryRenderer2D.Vertex() { Position = p3, Color = fillColor, UV = default, });
            defaultWriter.AddVertex(new DynamicGeometryRenderer2D.Vertex() { Position = p4, Color = fillColor, UV = default, });

            defaultWriter.AddLine(p1, p2, borderColor);
            defaultWriter.AddLine(p2, p3, borderColor);
            defaultWriter.AddLine(p3, p4, borderColor);
            defaultWriter.AddLine(p4, p1, borderColor);
        }

        if (_showForces)
        {
            foreach (var force in _solver.Forces)
            {
                switch (force)
                {
                    case Manifold manifold:
                    {
                        defaultWriter.AddLine(manifold.BodyA!.Position.XY, manifold.BodyB!.Position.XY, new vec4(1, 1, 1, 1));
                        break;
                    }
                }
            }
        }

        // preview
        if (pickedBody is null)
        {
            var size = GetNewBodySize();

            mat2 rot = mat2.Rotation(0f);

            vec2 halfSize = size * 0.5f;

            vec2 p1 = rot * new vec2(-halfSize.X, -halfSize.Y) + mouseWorld; // bottom left
            vec2 p2 = rot * new vec2(+halfSize.X, -halfSize.Y) + mouseWorld; // bottom right
            vec2 p3 = rot * new vec2(+halfSize.X, +halfSize.Y) + mouseWorld; // top right
            vec2 p4 = rot * new vec2(-halfSize.X, +halfSize.Y) + mouseWorld; // top left

            defaultWriter.AddLine(p1, p2, previewColor);
            defaultWriter.AddLine(p2, p3, previewColor);
            defaultWriter.AddLine(p3, p4, previewColor);
            defaultWriter.AddLine(p4, p1, previewColor);
        }

        // mouse
        defaultWriter.AddRectangle(mouseWorld, new vec2(0.1f, 0.1f), new vec4(1f));

        _renderer.Render(mvp);
    }

    private vec2 GetNewBodySize()
    {
        switch (_newBodyType)
        {
            case NewBodyType.SquareSmall: return new vec2(1f, 1f);
            case NewBodyType.SquareBig: return new vec2(2f, 2f);
            case NewBodyType.RectVert: return new vec2(1f, 3f);
            case NewBodyType.RectHor: return new vec2(3f, 1f);
            default: throw new NotImplementedException();
        }
    }
}
