using Paw.Core.Graphics;
using Paw.Core.Platforms;
using Paw.Core.Resources;
using Paw.Core.Utils;
using System.Diagnostics.Tracing;

namespace Paw.Core.Physics;

public class PhysicsTestScene : Scene
{
    private readonly Solver _solver = new();

    private DynamicGeometryRenderer2D _renderer = null!;

    private int _zoom = 0;
    private bool _showAxis = true;

    private vec2 _mouseScreen;

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

        _zoom -= context.Input.Mouse.WheelDelta;

        _solver.Step();

        _mouseScreen = new vec2(context.Input.Mouse.X, context.Input.Mouse.Y);
    }

    public override void Render(RenderContext context)
    {
        var dt = context.DeltaTime;

        //
        // UI
        //

        UI.SetNextWindowPositionMode(Core.UI.UI.WindowPositionMode.Left);
        using (UI.BeginWindow(new Vector2(200, 400), "Physics test"))
        {
            if (UI.Button("Reset"))
            {
                _zoom = 0;
            }

            UI.Label(Format($"Zoom: {_zoom}"));
            UI.Label(Format($"Pos: ..."));

            UI.Checkbox("Show axis", ref _showAxis);

            UI.Label(Format($"Bodies: {_solver.Bodies.Count}"));
            UI.Label(Format($"Forces: {_solver.Forces.Count}"));
        }

        //
        // setup projection
        //

        (int width, int height) = context.WindowSize;

        f32 ratio = (f32)width / (f32)height;
        f32 visibleWidth = 50f * MathF.Pow(1.25f, _zoom);
        f32 visibleHeight = visibleWidth / ratio;

        mat4 mProj = mat4.CreateOrthographic(visibleWidth, visibleHeight, -1f, 1f);
        mat4 mModel = mat4.Identity;
        mat4 mView = mat4.Identity;
        mat4 mvp = mModel * mView * mProj;

        if (!mat4.Invert(mProj, out mat4 mInvProj))
            throw new InvalidOperationException("can't invert projection matrix");

        vec2 mouseNDC = _mouseScreen / new vec2(width, -height) * 2f + new vec2(-1f, 1f);
        vec2 mouseWorld = vec4.Transform(new vec4(mouseNDC, 0f, 1f), mInvProj).XY;

        //
        // input
        //

        if (_solver.Pick(mouseWorld, out Body? pickedBody, out vec2 pickedLocalPos))
        {
            //
        }

        if (pickedBody is not null)
        {
            if (context.Input.Mouse.Get(MouseButton.Left))
            {
                pickedBody.Position = new vec3(mouseWorld, pickedBody.Position.Z);
            }

            if (context.Input.Mouse.WasPressed(MouseButton.Left))
            {
                //
            }

            if (context.Input.Mouse.WasPressed(MouseButton.Right))
            {
                //_solver.Bodies.Remove(pickedBody);
            }
        }
        else
        {
            if (context.Input.Mouse.WasPressed(MouseButton.Left))
            {
                _solver.Bodies.Add(new Body(
                    size: new vec2(1, 1),
                    density: 1.0f,
                    friction: 0.5f,
                    position: new Vector3(mouseWorld.X, mouseWorld.Y, 0),
                    velocity: vec3.Zero
                    ));
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
            AddText(new Vector2(+10, 0), "10");
            AddText(new Vector2(0, -10), "-10");
            AddText(new Vector2(0, +10), "10");
        }

        var bodyColor = new Vector4(0.6f, 0.3f, 0.3f, 1f);
        var selectedBodyColor = new Vector4(0.7f, 0.4f, 0.4f, 1f);

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

            var fillColor = pickedBody == body ? selectedBodyColor : bodyColor;
            var borderColor = new vec4(1, 1, 1, 1);

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

            //AddText(pos, Format($"{body.Forces.Count} forces"));
        }

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

        // mouse
        defaultWriter.AddRectangle(mouseWorld, new vec2(0.1f, 0.1f), new vec4(1f));

        _renderer.Render(mvp);
    }
}
