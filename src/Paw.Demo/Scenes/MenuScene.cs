using Paw.Core.Graphics;
using Paw.Core.Platforms;
using Paw.Core.Resources;
using System.Numerics;

namespace Paw.Demo.Scenes;

internal class MenuScene : Scene
{
    private DynamicGeometryRenderer2D _renderer = null!;

    private int _selected = 0;

    public MenuScene(SceneContext context)
        : base(context)
    {
    }

    public override void Load()
    {
        _renderer = new DynamicGeometryRenderer2D(AssetManager, ["default", "font1"], ["font1"]);
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
            context.SceneController.RequestExit();
        }

        if (kb.WasPressed(Key.W) || kb.WasPressed(Key.Up))
        {
            _selected = Math.Max(0, _selected - 1);
        }

        if (kb.WasPressed(Key.S) || kb.WasPressed(Key.Down))
        {
            _selected = Math.Min(2, _selected + 1);
        }

        if (kb.WasPressed(Key.Tab))
        {
            _selected = (_selected + 1) % 3;
        }

        if (kb.WasPressed(Key.Enter))
        {
            switch (_selected)
            {
                case 0: context.SceneController.RequestSceneChange("rendertest1"); break;
                case 1: context.SceneController.RequestSceneChange("rendertest2"); break;
                case 2: context.SceneController.RequestExit(); break;
            }
        }
    }

    public override void Render(RenderContext context)
    {
        const float WorldWidth = 10f;
        const float WorldHeight = 10f;

        var (windowWidth, windowHeight) = context.WindowSize;

        float scale = MathF.Min(windowWidth / WorldWidth, windowHeight / WorldHeight);
        float viewportW = WorldWidth * scale;
        float viewportH = WorldHeight * scale;
        float viewportX = (windowWidth - viewportW) / 2f;
        float viewportY = (windowHeight - viewportH) / 2f;

        var gl = AssetManager.GL;
        gl.Viewport((int)viewportX, (int)viewportY, (int)viewportW, (int)viewportH); // TODO rounding issues?

        var mOrthoProj = Matrix4x4.CreateOrthographicOffCenter(0, WorldWidth, WorldHeight, 0, -1, 1);
        var mModel = Matrix4x4.Identity;
        var mView = Matrix4x4.Identity;
        var mvp = mModel * mView * mOrthoProj;


        var defaultWriter = _renderer.GetWriter("default");
        var fontWriter = _renderer.GetWriter("font1");
        var font = _renderer.GetFont("font1");


        //defaultWriter.AddRectangle(
        //    new Vector2(WorldWidth * 0.5f, WorldHeight * 0.5f),
        //    new Vector2(WorldWidth, WorldHeight),
        //    new Vector3(0.2f, 0.1f, 0.1f));

        //const float textScale = 1f / 64f;

        //fontWriter.AddText(font, new Vector2(1, 1), textScale, "Hello!");
        //fontWriter.AddText(font, new Vector2(1, 3), textScale, $"1: Render test 1 {(_selected == 0 ? "<" : "")}");
        //fontWriter.AddText(font, new Vector2(1, 4), textScale, $"2: Render test 2 {(_selected == 1 ? "<" : "")}");
        //fontWriter.AddText(font, new Vector2(1, 5), textScale, $"3: Exit {(_selected == 2 ? "<" : "")}");

        _renderer.Render(mvp);
    }
}
