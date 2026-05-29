using Paw.Core.Graphics;
using Paw.Core.Platforms;
using Paw.Core.Resources;
using Paw.Core.Utils;

namespace Paw.Core.Physics;

public class PhysicsTestScene : Scene
{
    private readonly Solver _solver = new();

    private DynamicGeometryRenderer2D _renderer = null!;


    private vec2 _mouseScreen;

    private int _cameraZoom;
    private vec2 _cameraPos;

    private bool _movingCamera;
    private vec2 _cameraGrabPos;

    private bool _showAxis = true;
    private bool _showManifolds = true;
    private bool _showJoints = true;


    public enum Mode
    {
        GrabAndSelect,
        CreateBody,
        CreateJoint,
    }
    private Mode _mode;
    private readonly Mode[] _allModes = Enum.GetValues<Mode>();


    public enum BodyType
    {
        SquareSmall,
        SquareBig,
        RectVert,
        RectHor,
    }
    private BodyType _newBodyType;
    private readonly BodyType[] _allBodyTypes = Enum.GetValues<BodyType>();


    private Body? _selectedBody;
    private Joint? _grabJoint;


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

    private readonly float[] _startStopTableCols = [0.333f, 0.333f, 0.333f];

    public override void Render(RenderContext context)
    {
        float dt = context.DeltaTime;
        (int windowWidth, int windowHeight) = context.WindowSize;

        bool modeChanged;
        Body? pickedBody = null;

        //
        // UI
        //

        UI.Overlay(Format($"Bodies: {_solver.Bodies.Count}"));
        UI.Overlay(Format($"Forces: {_solver.Forces.Count}"));
        UI.Overlay(Format($"Ticks: {_tickCount}"));

        UI.SetNextWindowPositionMode(Core.UI.UI.WindowPositionMode.Left);
        using (UI.BeginWindow(new Vector2(300, 800), "Physics test"))
        {
            //ReadOnlySpan<float> cols = stackalloc float[] { 0.333f, 0.333f, 0.333f }; // TODO allocs in debug build, but why?

            //Span<float> cols = stackalloc float[3]; // does not alloc in debug build
            //cols[0] = 0.333f;
            //cols[1] = 0.333f;
            //cols[2] = 0.333f;

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

            // Solver Parameters
            UI.Input("Iterations", ref _solver.Iterations);
            UI.Input("Alpha", ref _solver.Alpha);
            UI.Input("Beta", ref _solver.Beta);
            UI.Input("Gamma", ref _solver.Gamma);
            UI.Checkbox("Post stabilize", ref _solver.PostStabilize);

            bool hasGravity = _solver.Gravity.Length() > 0.1f;
            if (UI.Checkbox("Gravity", ref hasGravity))
            {
                Console.WriteLine($"Change gravity");
                _solver.Gravity = hasGravity ? new vec3(0f, -9.81f, 0f) : vec3.Zero;
            }

            if (UI.Button("Reset params"))
            {
                _solver.SetDefaultConfig();
            }

            // Render settings
            if (UI.Button("Reset camera"))
            {
                _cameraPos = default;
                _cameraZoom = 0;
            }

            UI.Label("Render:");
            UI.Checkbox("Show axis", ref _showAxis);
            UI.Checkbox("Show manifolds", ref _showManifolds);
            UI.Checkbox("Show joints", ref _showJoints);

            //
            modeChanged = UI.Radiobuttons("Mouse mode:", ref _mode, _allModes);

            //
            if (_mode == Mode.CreateBody)
            {
                UI.Radiobuttons("New body:", ref _newBodyType, _allBodyTypes);
            }
        }

        if (_selectedBody is not null)
        {
            UI.SetNextWindowPositionMode(Core.UI.UI.WindowPositionMode.Right);
            using (UI.BeginWindow(new Vector2(300, 800), "Selected Body"))
            {
                UI.Label(Format($"Position: {_selectedBody.Position:F3}"));
                UI.Label(Format($"Velocity: {_selectedBody.Velocity:F3}"));
                UI.Label(Format($"Size: {_selectedBody.Size:0.###}"));
                UI.Label(Format($"Mass: {_selectedBody.Mass:0.###}"));
                UI.Label(Format($"Moment: {_selectedBody.Moment:F3}"));
                UI.Label(Format($"Friction: {_selectedBody.Friction:F3}"));
                UI.Label(Format($"Radius: {_selectedBody.Radius:F3}"));

                UI.Label(Format($"Forces: {_selectedBody.Forces.Count}"));

                foreach (var force in _selectedBody.Forces)
                {
                    UI.Label(Format($"Force {force.GetType().Name}"));

                    for (int i = 0; i < force.Rows; i++)
                    {
                        UI.Label(Format($"C[{i}]: {force.C[i]:F3}"));
                        UI.Label(Format($"Penalty[{i}]: {force.Penalty[i]:F3}"));
                        UI.Label(Format($"Lambda[{i}]: {force.Lambda[i]:F3}"));
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

        // cleanup between modes
        if (modeChanged)
        {
            _selectedBody = null;

            if (_grabJoint is not null)
            {
                Console.WriteLine($"Destroy grab joint");
                _solver.Forces.Remove(_grabJoint);
                _grabJoint.RemoveFromBodies();
                _grabJoint = null;
            }
        }

        switch (_mode)
        {
            case Mode.GrabAndSelect:
            {
                if (_solver.Pick(mouseWorld, out pickedBody, out vec2 pickedLocalPos))
                {
                    if (context.Input.Mouse.WasPressed(MouseButton.Left) && !mouseOverUI)
                    {
                        _selectedBody = pickedBody;

                        if (_grabJoint is null)
                        {
                            Console.WriteLine($"Create grab joint");
                            _grabJoint = new Joint(null, pickedBody, mouseWorld, pickedLocalPos, new vec3(1000, 1000, 0));
                            _solver.Forces.Add(_grabJoint);
                        }
                    }
                }

                if (context.Input.Mouse.WasPressed(MouseButton.Right) && !mouseOverUI)
                {
                    _selectedBody = null;
                }

                if (_grabJoint is not null)
                {
                    if (context.Input.Mouse.Get(MouseButton.Left) && !mouseOverUI)
                    {
                        _grabJoint.RA = mouseWorld;
                    }
                    else
                    {
                        Console.WriteLine($"Destroy grab joint");
                        _solver.Forces.Remove(_grabJoint);
                        _grabJoint.RemoveFromBodies();
                        _grabJoint = null;
                    }
                }

                break;
            }

            case Mode.CreateBody:
            {
                if (context.Input.Mouse.WasPressed(MouseButton.Left) && !mouseOverUI)
                {
                    var newBody = new Body(size: GetNewBodySize(),
                                           density: 1.0f, friction: 0.5f,
                                           position: new Vector3(mouseWorld, 0),
                                           velocity: vec3.Zero);

                    _solver.Bodies.Add(newBody);

                    _selectedBody = newBody;
                }

                if (context.Input.Mouse.WasPressed(MouseButton.Right) && !mouseOverUI)
                {
                    _selectedBody = null;
                }

                break;
            }

            case Mode.CreateJoint:
            {
                if (_solver.Pick(mouseWorld, out pickedBody, out vec2 pickedLocalPos))
                {
                    if (context.Input.Mouse.WasPressed(MouseButton.Left) && !mouseOverUI)
                    {
                        if (_selectedBody is null)
                        {
                            _selectedBody = pickedBody;
                        }
                        else if (_selectedBody != pickedBody)
                        {
                            Console.WriteLine($"new joint");

                            vec2 pA = _selectedBody.Position.XY;
                            vec2 pB = pickedBody.Position.XY;
                            vec2 pAB = pB - pA;

                            vec2 rA = Transform2D.WorldToLocal(_selectedBody.Position, pA + pAB * 0.5f);
                            vec2 rB = Transform2D.WorldToLocal(pickedBody.Position, pB - pAB * 0.5f);

                            vec3 stiffness = new vec3(1000f, 1000f, 0f);

                            var newJoint = new Joint(_selectedBody, pickedBody, rA, rB, stiffness);
                            _solver.Forces.Add(newJoint);

                            _selectedBody = null;
                        }
                    }
                }

                if (context.Input.Mouse.WasPressed(MouseButton.Right) && !mouseOverUI)
                {
                    _selectedBody = null;
                }

                break;
            }

            default: throw new NotImplementedException();
        }

        //
        // content
        //

        var font = _renderer.GetFont("font1");
        var defaultWriter = _renderer.GetWriter("default");
        var fontWriter = _renderer.GetWriter("font1");

        const f32 fontScale = 0.01f;

        var axisColor = new Vector4(1, 1, 1, 1);
        var bodyColor = new Vector4(0.6f, 0.3f, 0.3f, 1f);
        var selectedBodyColor = new Vector4(0.7f, 0.4f, 0.4f, 1f);
        var borderColor = new vec4(1, 1, 1, 1);
        var previewColor = new vec4(1, 0, 1, 1);

        void AddText(vec2 pos, ReadOnlySpan<char> text)
        {
            fontWriter.AddText(font, pos, vec4.One, fontScale, text, true);
        }

        if (_showAxis)
        {
            defaultWriter.AddLine(new Vector2(-10, 0), new Vector2(10, 0), axisColor);
            defaultWriter.AddLine(new Vector2(0, -10), new Vector2(0, 10), axisColor);

            AddText(new Vector2(-10, 0), "-10");
            AddText(new Vector2(+10, 0), "+10");
            AddText(new Vector2(0, -10), "-10");
            AddText(new Vector2(0, +10), "+10");
        }

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

            AddText(pos, Format($"{body.Id}"));
        }

        foreach (var force in _solver.Forces)
        {
            switch (force)
            {
                case Manifold manifold when _showManifolds:
                {
                    for (int i = 0; i < manifold.NumContacts; i++)
                    {
                        var contact = manifold.Contacts[i];
                        vec2 n = contact.Normal;
                        vec2 pA1 = Transform2D.LocalToWorld(manifold.BodyA!.Position, contact.RA);
                        vec2 pA2 = pA1 + n * 0.25f;
                        defaultWriter.AddLine(pA1, pA2, new vec4(1, 0, 0, 1));
                    }
                    //defaultWriter.AddLine(manifold.BodyA!.Position.XY, manifold.BodyB!.Position.XY, new vec4(1, 0, 0, 1));
                    break;
                }

                case Joint joint when _showJoints:
                {
                    vec2 centerA = joint.BodyA is not null
                        ? joint.BodyA.Position.XY
                        : joint.RA;

                    vec2 centerB = joint.BodyB is not null
                        ? joint.BodyB.Position.XY
                        : joint.RB;

                    vec2 posA = joint.BodyA is not null
                        ? Transform2D.LocalToWorld(joint.BodyA!.Position, joint.RA)
                        : joint.RA;

                    vec2 posB = joint.BodyB is not null
                        ? Transform2D.LocalToWorld(joint.BodyB!.Position, joint.RB)
                        : joint.RB;

                    defaultWriter.AddLine(centerA, posA, new vec4(0, 1, 0, 1));
                    defaultWriter.AddLine(centerB, posB, new vec4(0, 0, 1, 1));
                    break;
                }
            }
        }

        // preview
        if (_mode == Mode.CreateBody && !context.Input.Mouse.Get(MouseButton.Left) && !mouseOverUI)
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

    private vec2 GetNewBodySize() => _newBodyType switch
    {
        BodyType.SquareSmall => new vec2(1f, 1f),
        BodyType.SquareBig => new vec2(2f, 2f),
        BodyType.RectVert => new vec2(1f, 3f),
        BodyType.RectHor => new vec2(3f, 1f),
        _ => throw new NotImplementedException(),
    };
}
