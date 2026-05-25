using Paw.Core.Graphics;
using Paw.Core.Platforms;
using Paw.Core.Resources;

namespace Paw.Core.Physics;


public class Rigid
{
    public vec2 Position;
    public vec2 Size;
}

public class Force
{
    //
}


public class Solver
{
    public readonly List<Rigid> Rigids = [];
    public readonly List<Force> Forces = [];



    public void Reset()
    {
        //
    }


    public void Step(float dt)
    {
        //
    }
}



public class PhysicsTestScene : Scene
{
    private readonly Solver _solver = new();

    private const float _stepDt = 0.01f;

    private DynamicGeometryRenderer2D _renderer = null!;

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

        _solver.Step(_stepDt);
    }

    public override void Render(RenderContext context)
    {
        var dt = context.DeltaTime;
        var (width, height) = context.WindowSize;

        var mOrthoProj = Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, -1, 1);
        var mModel = Matrix4x4.Identity;
        var mView = Matrix4x4.Identity;

        var mvp = mModel * mView * mOrthoProj;

        var font = _renderer.GetFont("font1");
        var defaultWriter = _renderer.GetWriter("default");
        var fontWriter = _renderer.GetWriter("font1");

        defaultWriter.AddRectangle(new Vector2(300, 300), new Vector2(200, 100), new Vector4(1, 0, 0, 1));



        fontWriter.AddText(font, new Vector2(100, 200), new Vector4(1), 1f, "Hello World! 123 (4) [5] {6} .,/-+_");

        _renderer.Render(mvp);
    }
}