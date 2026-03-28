using Paw.Core.Engine;
using Paw.Core.Graphics;
using Paw.Core.Platforms;
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
        _renderer = new DynamicGeometryRenderer2D(AssetManager);
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

        _renderer.AddTriangle(
            _playerPos + new Vector2(0, 0), new Vector3(1.0f, 0.0f, 0.0f),
            _playerPos + new Vector2(50, 0), new Vector3(0.0f, 1.0f, 0.0f),
            _playerPos + new Vector2(50, 50), new Vector3(0.0f, 0.0f, 1.0f)
        );

        _renderer.AddTriangle(
            _playerPos + new Vector2(100, 0), new Vector3(1.0f, 0.0f, 0.0f),
            _playerPos + new Vector2(150, 0), new Vector3(0.0f, 1.0f, 0.0f),
            _playerPos + new Vector2(150, 50), new Vector3(0.0f, 0.0f, 1.0f)
        );

        //_renderer.AddRectangle(new Vector2(300, 300), new Vector2(300, 300), new Vector3(1, 1, 0));

        _renderer.AddRectangleWithTexture(new Vector2(800, 500), new Vector2(800, 800), new Vector3(1, 1, 1), new Vector2(0, 0), new Vector2(1, 1));

        //_renderer.AddRectangleWithTexture(new Vector2(1100, 300), new Vector2(300, 300), new Vector3(1, 1, 1), new Vector2(0, 0), new Vector2(1, 1));

        float fontScale = 1.0f + MathF.Max(0.0f, MathF.Sin(_time)) * 5.0f;

        _renderer.AddText(new Vector2(100, 200), fontScale, "Hello World! 123 (4) [5] {6} .,/-+_");

        _renderer.Render(mvp);
    }
}
