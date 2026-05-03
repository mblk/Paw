using Paw.Core.Graphics;
using Paw.Core.Platforms;
using Paw.Core.Resources;
using System.Numerics;

namespace Paw.Demo.Scenes;

internal class MenuScene : Scene
{
    private DynamicGeometryRenderer2D _renderer = null!;

    private string[]? _allScenes;

    public MenuScene(SceneContext context)
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
        _allScenes ??= context.SceneController.GetAllSceneIds().ToArray();

        var dt = context.DeltaTime;
        var kb = context.Input.Keyboard;

        if (kb.WasPressed(Key.Escape))
        {
            context.SceneController.RequestExit();
        }

        UI.SetNextWindowPositionMode(Core.UI.UI.WindowPositionMode.Center); // TODO should UI calls be in Update or in Render?
        using (UI.BeginWindow(new Vector2(200, 400), "Menu"))
        {
            foreach (var sceneId in _allScenes)
            {
                if (UI.Button($"Load Scene: {sceneId}"))
                {
                    context.SceneController.RequestSceneChange(sceneId);
                }
            }

            if (UI.Button("Exit"))
            {
                context.SceneController.RequestExit();
            }
        }
    }

    public override void Render(RenderContext context)
    {
        var dt = context.DeltaTime;
        var (width, height) = context.WindowSize;

        var mOrthoProj = Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, -1, 1);
        var mModel = Matrix4x4.Identity;
        var mView = Matrix4x4.Identity;

        var mvp = mModel * mView * mOrthoProj;

        var defaultWriter = _renderer.GetWriter("default");
        var tex1Writer = _renderer.GetWriter("textured1");
        var tex2Writer = _renderer.GetWriter("textured2");
        var fontWriter = _renderer.GetWriter("font1");
        var font = _renderer.GetFont("font1");

        defaultWriter.AddRectangle(new Vector2(300, 300), new Vector2(200, 200), new Vector4(0.6f, 0.3f, 0.3f, 1));
        tex1Writer.AddRectangle(new Vector2(500, 500), new Vector2(200, 200), new Vector4(1));
        tex2Writer.AddRectangle(new Vector2(700, 700), new Vector2(200, 200), new Vector4(1));
        fontWriter.AddText(font, new Vector2(200, 600), new Vector4(1, 1, 1, 1), 1f, "Hello!");

        _renderer.Render(mvp);
    }
}
