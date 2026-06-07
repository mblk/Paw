using Paw.Core.Graphics;
using Paw.Core.Platforms;
using Paw.Core.Resources;
using Paw.Core.Utils;

namespace Paw.Core.Physics;

public class PhysicsTestScene : Scene
{
    private Solver _solver = null!;

    private DynamicGeometryRenderer2D _renderer = null!;


    private int _cameraZoom;
    private vec2 _cameraPos;

    private bool _movingCamera;
    private vec2 _cameraGrabPos;

    private bool _showAxis = true;
    private bool _showManifolds = true;
    private bool _showJoints = true;
    private bool _showSprings = true;


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


    private BodyRef _selectedBody;

    private Joint? _grabJoint;
    private float _grabStiffness = 1000f;
    private bool _grabLockAngle = true;


    private bool _simulate = true;
    private bool _singleStep = false;
    private int _tickCount = 0;


    private readonly Dictionary<BodyRef, (Key, float)> _thrusters = [];

    private BodyRef _settingThrusterKeyForBody;



    public PhysicsTestScene(SceneContext context)
        : base(context)
    {
    }

    public override void Load()
    {
        _renderer = new DynamicGeometryRenderer2D(AssetManager, ["default", "font1"], ["font1"]);

        _solver = new Solver();

        var b1 = _solver.AddBody(new vec3(-3, 0, 0), new vec2(1, 1));
        var b2 = _solver.AddBody(new vec3(-2, 0, 0), new vec2(1, 1));
        var b3 = _solver.AddBody(new vec3(-1, 0, 0), new vec2(1, 1));
        var b4 = _solver.AddBody(new vec3(0, 0, 0), new vec2(1, 1));
        var b5 = _solver.AddBody(new vec3(1, 0, 0), new vec2(1, 1));
        var b6 = _solver.AddBody(new vec3(2, 0, 0), new vec2(1, 1));
        var b7 = _solver.AddBody(new vec3(3, 0, 0), new vec2(1, 1));

        var b2b = _solver.AddBody(new vec3(-2, -1.5f, 0f), new vec2(0.25f, 2f));
        var b6b = _solver.AddBody(new vec3(+2, -1.5f, 0f), new vec2(0.25f, 2f));

        _thrusters.Add(b1, (Key.A, 1.5f));
        _thrusters.Add(b4, (Key.W, 10.0f));
        _thrusters.Add(b7, (Key.D, 1.5f));

        _solver.AddStiffAutoJoint(b1, b2);
        _solver.AddStiffAutoJoint(b2, b3);
        _solver.AddStiffAutoJoint(b3, b4);
        _solver.AddStiffAutoJoint(b4, b5);
        _solver.AddStiffAutoJoint(b5, b6);
        _solver.AddStiffAutoJoint(b6, b7);

        _solver.AddStiffAutoJoint(b2, b2b);
        _solver.AddStiffAutoJoint(b6, b6b);
    }

    public override void Unload()
    {
    }

    public override void Update(UpdateContext context)
    {
        float dt = context.DeltaTime;
        var kb = context.Input.Keyboard;

        //
        // Physics related input
        //

        foreach (BodyRef bodyRef in _solver.AliveBodies)
        {
            if (!_thrusters.TryGetValue(bodyRef, out (Key Key, float Power) thruster))
                continue;

            if (kb.Get(thruster.Key))
            {
                _solver.AddForceLocal(bodyRef, new vec2(0, 1) * 9.81f * _solver.GetMass(bodyRef) * thruster.Power);
            }
        }

        //
        // Physics step
        //

        if (_simulate || _singleStep)
        {
            _singleStep = false;
            _solver.Step();
            _tickCount++;
        }
    }

    public override void Render(RenderContext context)
    {
        (int windowWidth, int windowHeight) = context.WindowSize;
        float dt = context.DeltaTime;
        var kb = context.Input.Keyboard;

        bool modeChanged;
        BodyRef pickedBody = default;

        //
        // UI related input
        //

        if (kb.WasPressed(Key.Escape))
        {
            context.SceneController.RequestSceneChange("menu");
        }

        if (kb.Get(Key.Left)) _cameraPos.X -= 10.0f * dt;
        if (kb.Get(Key.Right)) _cameraPos.X += 10.0f * dt;
        if (kb.Get(Key.Up)) _cameraPos.Y += 10.0f * dt;
        if (kb.Get(Key.Down)) _cameraPos.Y -= 10.0f * dt;

        if (kb.WasPressed(Key.Delete))
        {
            if (_selectedBody is { })
            {
                _solver.RemoveBody(_selectedBody);
                _selectedBody = default;
            }
        }

        //
        // UI
        //

        //UI.Overlay(Format($"Bodies: {_solver.Bodies.Count}"));
        UI.Overlay(Format($"Forces: {_solver.Forces.Count}"));
        UI.Overlay(Format($"Ticks: {_tickCount}"));

        UI.SetNextWindowPositionMode(Core.UI.UI.WindowPositionMode.Left);
        using (UI.BeginWindow(new Vector2(300, 800), "Physics test"))
        {
            using (UI.BeginTable(0.333f, 0.333f, 0.333f))
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
            UI.Checkbox("Show springs", ref _showSprings);

            //
            modeChanged = UI.Radiobuttons("Mouse mode:", ref _mode, _allModes);

            //
            if (_mode == Mode.GrabAndSelect)
            {
                UI.Label("Grab:");
                UI.Input("Stiffness", ref _grabStiffness);
                UI.Checkbox("Lock angle", ref _grabLockAngle);
            }

            if (_mode == Mode.CreateBody)
            {
                UI.Radiobuttons("New body:", ref _newBodyType, _allBodyTypes);
            }
        }

        if (_selectedBody != default)
        {
            UI.SetNextWindowPositionMode(Core.UI.UI.WindowPositionMode.Right);
            using (UI.BeginWindow(new Vector2(300, 800), "Selected Body"))
            {
                if (_thrusters.TryGetValue(_selectedBody, out (Key, float) thruster))
                {
                    UI.Label(Format($"Thruster: key={Enum.GetName(thruster.Item1)} power={thruster.Item2}"));
                    if (UI.Button("Clear"))
                    {
                        _thrusters.Remove(_selectedBody);
                    }
                    _settingThrusterKeyForBody = default;
                }
                else
                {
                    if (_settingThrusterKeyForBody == _selectedBody)
                    {
                        UI.Label("Press key ...");

                        Key? firstPressed = kb.GetFirstPressedKey();
                        if (firstPressed.HasValue)
                        {
                            _thrusters.Add(_selectedBody, (firstPressed.Value, 1.0f));
                            _settingThrusterKeyForBody = default;
                        }
                    }
                    else if (UI.Button("Set thruster key"))
                    {
                        _settingThrusterKeyForBody = _selectedBody;
                    }
                }

                //UI.Label(Format($"Position: {_selectedBody.Position:F3}"));
                //UI.Label(Format($"Velocity: {_selectedBody.Velocity:F3}"));
                //UI.Label(Format($"Size: {_selectedBody.Size:0.###}"));
                //UI.Label(Format($"Mass: {_selectedBody.Mass:0.###}"));
                //UI.Label(Format($"Moment: {_selectedBody.Moment:F3}"));
                //UI.Label(Format($"Friction: {_selectedBody.Friction:F3}"));
                //UI.Label(Format($"Radius: {_selectedBody.Radius:F3}"));

                //UI.Label(Format($"Forces: {_selectedBody.Forces.Count}"));

                //foreach (var force in _selectedBody.Forces)
                //{
                //    UI.Label(Format($"Force {force.GetType().Name}"));

                //    for (int i = 0; i < force.Rows; i++)
                //    {
                //        UI.Label(Format($"C[{i}]: {force.C[i]:F3}"));
                //        UI.Label(Format($"Penalty[{i}]: {force.Penalty[i]:F3}"));
                //        UI.Label(Format($"Lambda[{i}]: {force.Lambda[i]:F3}"));
                //    }
                //}
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

        vec2 mouseScreen = new vec2(context.Input.Mouse.X, context.Input.Mouse.Y);
        vec2 mouseNDC = mouseScreen / new vec2(windowWidth, -windowHeight) * 2f + new vec2(-1f, 1f);
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

        // cleanup between modes
        if (modeChanged)
        {
            _selectedBody = default;

            if (_grabJoint is { })
            {
                _solver.RemoveForce(_grabJoint);
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

                        _grabJoint ??= _solver.AddJoint(default, pickedBody, mouseWorld, pickedLocalPos,
                                                        new vec3(_grabStiffness, _grabStiffness, _grabLockAngle ? _grabStiffness : 0f));
                    }
                }

                if (context.Input.Mouse.WasPressed(MouseButton.Right) && !mouseOverUI)
                {
                    _selectedBody = default;
                }

                if (_grabJoint is not null)
                {
                    if (context.Input.Mouse.Get(MouseButton.Left) && !mouseOverUI)
                    {
                        _grabJoint.RA = mouseWorld;
                    }
                    else
                    {
                        _solver.RemoveForce(_grabJoint);
                        _grabJoint = null;
                    }
                }

                break;
            }

            case Mode.CreateBody:
            {
                if (context.Input.Mouse.WasPressed(MouseButton.Left) && !mouseOverUI)
                {
                    _selectedBody = _solver.AddBody(position: new vec3(mouseWorld, 0f),
                                                    size: GetNewBodySize());
                }

                if (context.Input.Mouse.WasPressed(MouseButton.Right) && !mouseOverUI)
                {
                    _selectedBody = default;
                }

                break;
            }

            case Mode.CreateJoint:
            {
                if (_solver.Pick(mouseWorld, out pickedBody, out vec2 pickedLocalPos))
                {
                    if (context.Input.Mouse.WasPressed(MouseButton.Left) && !mouseOverUI)
                    {
                        if (_selectedBody == default)
                        {
                            _selectedBody = pickedBody;
                        }
                        else if (_selectedBody != pickedBody)
                        {
                            _ = _solver.AddStiffAutoJoint(_selectedBody, pickedBody);
                            _selectedBody = default;
                        }
                    }
                }

                if (context.Input.Mouse.WasPressed(MouseButton.Right) && !mouseOverUI)
                {
                    _selectedBody = default;
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

        foreach (BodyRef bodyRef in _solver.AliveBodies)
        {
            Body body = _solver.GetCopyOfBody(bodyRef);

            vec2 halfSize = body.Size * 0.5f;
            vec2 pBL = body.LocalToWorld(new vec2(-halfSize.X, -halfSize.Y)); // bottom left
            vec2 pTR = body.LocalToWorld(new vec2(+halfSize.X, +halfSize.Y)); // top right
            vec2 pTL = body.LocalToWorld(new vec2(-halfSize.X, +halfSize.Y)); // top left
            vec2 pBR = body.LocalToWorld(new vec2(+halfSize.X, -halfSize.Y)); // bottom right

            var fillColor = (pickedBody == bodyRef || _selectedBody == bodyRef) ? selectedBodyColor : bodyColor;

            defaultWriter.AddVertex(new DynamicGeometryRenderer2D.Vertex() { Position = pBL, Color = fillColor, UV = default, });
            defaultWriter.AddVertex(new DynamicGeometryRenderer2D.Vertex() { Position = pBR, Color = fillColor, UV = default, });
            defaultWriter.AddVertex(new DynamicGeometryRenderer2D.Vertex() { Position = pTR, Color = fillColor, UV = default, });

            defaultWriter.AddVertex(new DynamicGeometryRenderer2D.Vertex() { Position = pBL, Color = fillColor, UV = default, });
            defaultWriter.AddVertex(new DynamicGeometryRenderer2D.Vertex() { Position = pTR, Color = fillColor, UV = default, });
            defaultWriter.AddVertex(new DynamicGeometryRenderer2D.Vertex() { Position = pTL, Color = fillColor, UV = default, });

            defaultWriter.AddLine(pBL, pBR, borderColor);
            defaultWriter.AddLine(pBR, pTR, borderColor);
            defaultWriter.AddLine(pTR, pTL, borderColor);
            defaultWriter.AddLine(pTL, pBL, borderColor);

            if (_thrusters.TryGetValue(bodyRef, out (Key Key, float Power) thruster))
            {
                float ts = MathF.Sqrt(thruster.Power) * 0.5f;
                vec4 thrusterColor = bodyColor;

                if (kb.Get(thruster.Key))
                {
                    ts *= 1.2f;
                    thrusterColor = new vec4(1, 0, 0, 1);
                }

                vec2 ptTL = body.LocalToWorld(new vec2(-0.25f * ts, -halfSize.Y)); // top left
                vec2 ptTR = body.LocalToWorld(new vec2(+0.25f * ts, -halfSize.Y)); // top right
                vec2 ptBL = body.LocalToWorld(new vec2(-0.5f * ts, -halfSize.Y - 1f * ts)); // bottom left
                vec2 ptBR = body.LocalToWorld(new vec2(+0.5f * ts, -halfSize.Y - 1f * ts)); // bottom right

                defaultWriter.AddVertex(new DynamicGeometryRenderer2D.Vertex() { Position = ptTL, Color = thrusterColor, UV = default, });
                defaultWriter.AddVertex(new DynamicGeometryRenderer2D.Vertex() { Position = ptBL, Color = thrusterColor, UV = default, });
                defaultWriter.AddVertex(new DynamicGeometryRenderer2D.Vertex() { Position = ptBR, Color = thrusterColor, UV = default, });

                defaultWriter.AddVertex(new DynamicGeometryRenderer2D.Vertex() { Position = ptTL, Color = thrusterColor, UV = default, });
                defaultWriter.AddVertex(new DynamicGeometryRenderer2D.Vertex() { Position = ptBR, Color = thrusterColor, UV = default, });
                defaultWriter.AddVertex(new DynamicGeometryRenderer2D.Vertex() { Position = ptTR, Color = thrusterColor, UV = default, });
            }

            //AddText(pos, Format($"{body.Id}"));
        }

        foreach (var force in _solver.Forces)
        {
            switch (force)
            {
                case Manifold manifold when _showManifolds:
                {
                    for (int i = 0; i < manifold.NumContacts; i++)
                    {
                        Body bodyA = _solver.GetCopyOfBody(manifold.BodyA);

                        var contact = manifold.Contacts[i];
                        vec2 n = contact.Normal;
                        vec2 pA1 = bodyA.LocalToWorld(contact.RA);
                        vec2 pA2 = pA1 + n * 0.25f;
                        defaultWriter.AddLine(pA1, pA2, new vec4(1, 0, 0, 1));
                    }
                    break;
                }

                case Joint joint when _showJoints:
                {
                    vec2 centerA = joint.BodyA != default
                        ? _solver.GetCopyOfBody(joint.BodyA).Position.XY
                        : joint.RA;

                    vec2 centerB = joint.BodyB != default
                        ? _solver.GetCopyOfBody(joint.BodyB).Position.XY
                        : joint.RB;

                    vec2 posA = joint.BodyA != default
                        ? _solver.GetCopyOfBody(joint.BodyA).LocalToWorld(joint.RA)
                        : joint.RA;

                    vec2 posB = joint.BodyB != default
                        ? _solver.GetCopyOfBody(joint.BodyB).LocalToWorld(joint.RB)
                        : joint.RB;

                    defaultWriter.AddLine(centerA, posA, new vec4(0, 1, 0, 1));
                    defaultWriter.AddLine(centerB, posB, new vec4(0, 0, 1, 1));
                    break;
                }

                case Spring spring when _showSprings:
                {
                    vec2 centerA = _solver.GetCopyOfBody(spring.BodyA).Position.XY;
                    vec2 centerB = _solver.GetCopyOfBody(spring.BodyB).Position.XY;

                    defaultWriter.AddLine(centerA, centerB, new vec4(1, 1, 0, 1));
                    break;
                }
            }
        }

        // preview
        if (_mode == Mode.CreateBody && !context.Input.Mouse.Get(MouseButton.Left) && !mouseOverUI)
        {
            vec2 newSize = GetNewBodySize();
            vec2 halfSize = newSize * 0.5f;
            vec3 newPos = new vec3(mouseWorld, 0f);
            vec2 pBL = Transform2D.LocalToWorld(newPos, new vec2(-halfSize.X, -halfSize.Y)); // bottom left
            vec2 pBR = Transform2D.LocalToWorld(newPos, new vec2(+halfSize.X, -halfSize.Y)); // bottom right
            vec2 pTR = Transform2D.LocalToWorld(newPos, new vec2(+halfSize.X, +halfSize.Y)); // top right
            vec2 pTL = Transform2D.LocalToWorld(newPos, new vec2(-halfSize.X, +halfSize.Y)); // top left

            defaultWriter.AddLine(pBL, pBR, previewColor);
            defaultWriter.AddLine(pBR, pTR, previewColor);
            defaultWriter.AddLine(pTR, pTL, previewColor);
            defaultWriter.AddLine(pTL, pBL, previewColor);
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
