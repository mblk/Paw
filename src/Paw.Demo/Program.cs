using Paw.Core;
using Paw.Core.Platforms;
using Paw.Core.Resources;
using Paw.Core.Scenes;
using Paw.Core.UI;
using Paw.Demo.Scenes;

namespace Paw.Demo;

public class Program : App
{
    public Program(PlatformOptions platformOptions, WindowOptions windowOptions)
        : base(platformOptions, windowOptions) { }

    [STAThread]
    private static void Main(string[] args)
    {
        var platformOptions = new PlatformOptions();
        var windowOptions = new WindowOptions(1920, 1080, "Paw Demo", SwapInterval: 1);

        var app = new Program(platformOptions, windowOptions);
        app.Run();
    }

    protected override void RegisterScenes(SceneManager sceneManager) // TODO pass as config instead? like the other options
    {
        sceneManager.RegisterScene("menu", c => new MenuScene(c));
        sceneManager.RegisterScene("rendertest1", c => new RenderTest1Scene(c));
        sceneManager.RegisterScene("uitest1", c => new UiTestScene(c));
        sceneManager.RegisterScene("materialbrowser", c => new MaterialBrowser(c));

        //sceneManager.SetCurrentScene("uitest1");
        sceneManager.SetCurrentScene("materialbrowser");
        //sceneManager.SetCurrentScene("menu");
    }
}
