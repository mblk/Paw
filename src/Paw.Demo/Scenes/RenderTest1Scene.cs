using Paw.Core.Graphics;
using Paw.Core.Platforms;
using Paw.Core.Resources;
using System.Numerics;

namespace Paw.Demo.Scenes;

internal class RenderTest1Scene : Scene
{
    private DynamicGeometryRenderer2D _renderer = null!;

    private Vector2 _playerPos = new Vector2(100, 100);

    private float _time = 0f;

    public RenderTest1Scene(SceneContext context)
        : base(context)
    {
    }

    public override void Load()
    {
        _renderer = new DynamicGeometryRenderer2D(AssetManager, ["default", "font1", "textured1", "textured2"], ["font1"]);
    }

    public override void Unload()
    {
        _renderer.Dispose();
    }

    public override void Update(UpdateContext context)
    {
        var dt = context.DeltaTime;
        var kb = context.Input.Keyboard;

        if (kb.WasPressed(Key.Escape))
        {
            context.SceneController.RequestSceneChange("menu");
        }

        if (kb.Get(Key.A)) _playerPos.X -= 100f * dt;
        if (kb.Get(Key.D)) _playerPos.X += 100f * dt;
        if (kb.Get(Key.W)) _playerPos.Y -= 100f * dt;
        if (kb.Get(Key.S)) _playerPos.Y += 100f * dt;

        _time += dt;
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
        var textured1Writer = _renderer.GetWriter("textured1");
        var textured2Writer = _renderer.GetWriter("textured2");

        defaultWriter.AddRectangle(new Vector2(300, 300), new Vector2(200, 100), new Vector3(1, 0, 0));

        defaultWriter.AddTriangle(
            _playerPos + new Vector2(0, 0), new Vector3(1.0f, 0.0f, 0.0f),
            _playerPos + new Vector2(50, 0), new Vector3(0.0f, 1.0f, 0.0f),
            _playerPos + new Vector2(50, 50), new Vector3(0.0f, 0.0f, 1.0f)
        );

        defaultWriter.AddTriangle(
            _playerPos + new Vector2(100, 0), new Vector3(1.0f, 0.0f, 0.0f),
            _playerPos + new Vector2(150, 0), new Vector3(0.0f, 1.0f, 0.0f),
            _playerPos + new Vector2(150, 50), new Vector3(0.0f, 0.0f, 1.0f)
        );

        textured1Writer.AddRectangle(new Vector2(800, 500), new Vector2(800, 800), new Vector3(1, 1, 1));
        textured2Writer.AddRectangle(new Vector2(1400, 200), new Vector2(100, 100), new Vector3(1, 1, 1));
        textured2Writer.AddRectangle(new Vector2(1400, 300), new Vector2(100, 100), new Vector3(1, 1, 1));
        textured2Writer.AddRectangle(new Vector2(1400, 400), new Vector2(100, 100), new Vector3(1, 1, 1));

        textured2Writer.AddRotatedRectangle(new Vector2(1400, 500), new Vector2(100, 100), _time, new Vector3(1, 1, 1));

        float fontScale = 1.0f + MathF.Max(0.0f, MathF.Sin(_time)) * 5.0f;

        fontWriter.AddText(font, new Vector2(100, 200), fontScale, "Hello World! 123 (4) [5] {6} .,/-+_");

        _renderer.Render(mvp);
    }
}
