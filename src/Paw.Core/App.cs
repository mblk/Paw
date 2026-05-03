using Paw.Core.Graphics;
using Paw.Core.Platforms;
using Paw.Core.Resources;
using Paw.Core.Utils;

namespace Paw.Core;

public abstract class App
{
    private readonly PlatformOptions _platformOptions;
    private readonly WindowOptions _windowOptions;

    public App(PlatformOptions platformOptions, WindowOptions windowOptions)
    {
        _platformOptions = platformOptions;
        _windowOptions = windowOptions;
    }

    protected abstract void RegisterScenes(SceneManager sceneManager);

    public void Run()
    {
        SystemInfo.PrintSystemAndBuildInfo();

        // init platform

        using var platform = PlatformFactory.CreatePlatform(_platformOptions);
        using var window = platform.CreateWindow(_windowOptions);

        var gl = window.GL;

        // init assetmanager

        var assetBaseDir = AssetManager.FindBaseDirectory();

        var useAssetHotReloading = true;

        AssetManager assetManager = useAssetHotReloading
            ? new AssetManagerWithHotReload(assetBaseDir, gl)
            : new AssetManager(assetBaseDir, gl);

        // init ui

        using var ui = new UI.UI(assetManager);

        // init scenes

        using SceneManager sceneManager = new SceneManager(new SceneContext()
        {
            AssetManager = assetManager,
            UI = ui,
        });

        RegisterScenes(sceneManager);

        // main loop
        int numFrames = 0;
        double totalTime = 0;
        double avgFramerate = 0;

        var lastTime = DateTime.Now;

        while (window.ProcessEvents() && !sceneManager.ExitRequested)
        {
            //
            // timing
            //

            var now = DateTime.Now;
            double dt = (double)(now - lastTime).TotalSeconds;
            lastTime = now;

            totalTime += dt;
            numFrames++;
            if (totalTime > 0.25)
            {
                avgFramerate = 1.0 / (totalTime / numFrames);
                totalTime = 0;
                numFrames = 0;
            }

            //
            // update
            //

            (assetManager as AssetManagerWithHotReload)?.ProcessChanges();

            var updateContext = new UpdateContext()
            {
                DeltaTime = (float)dt,
                WindowSize = window.Size,
                Input = window.Input,
                SceneController = sceneManager,
            };

            sceneManager.Update(updateContext);

            ui.Update(updateContext);

            ui.Overlay($"FPS: {avgFramerate:F1}");
            ui.ShowDebuggingOverlay();

            //
            // render
            //

            var (windowWidth, windowHeight) = window.Size;

            var renderContext = new RenderContext()
            {
                DeltaTime = (float)dt,
                WindowSize = window.Size,
            };

            using (gl.PushDebugGroup("Frame"))
            {
                gl.Viewport(0, 0, windowWidth, windowHeight);

                gl.ClearColor(0.1f, 0.1f, 0.3f, 1f);
                gl.Clear(GL.ClearBufferMask.COLOR_BUFFER_BIT);

                using (gl.PushDebugGroup("Scene"))
                {
                    sceneManager.Render(renderContext);
                }

                using (gl.PushDebugGroup("UI"))
                {
                    ui.Render(renderContext);
                }
            }

            window.SwapBuffers();
        }

        // cleanup

        Console.WriteLine("after mainloop");

        Environment.Exit(0);
    }
}
