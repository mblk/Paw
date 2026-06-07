using Paw.Core.Graphics;
using Paw.Core.Platforms;
using Paw.Core.Resources;
using Paw.Core.Utils;
using System.Diagnostics;
using System.Threading;

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

        Thread.CurrentThread.Name = "Main thread";

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

        // timing
        int numFrames = 0;
        double totalTime = 0;
        double avgFramerate = 0;
        double avgAllocedBytesPerFrame = 0;
        //long gcTotalLast = GC.GetTotalAllocatedBytes(); // ignore other .net threads
        long gcTotalLast = GC.GetAllocatedBytesForCurrentThread();
        long lastTime = Stopwatch.GetTimestamp();

        // prepare reusable buffers
        var updateContext = new UpdateContext()
        {
            DeltaTime = 0f,
            WindowSize = window.Size,
            Input = window.Input,
            SceneController = sceneManager,
        };

        var renderContext = new RenderContext()
        {
            DeltaTime = 0f,
            WindowSize = window.Size,
            Input = window.Input,
            SceneController = sceneManager,
        };

        // main loop
        while (window.ProcessEvents() && !sceneManager.ExitRequested)
        {
            //
            // timing
            //

            long now = Stopwatch.GetTimestamp();
            TimeSpan dt = Stopwatch.GetElapsedTime(lastTime, now);
            lastTime = now;

            //
            // update
            //

            (assetManager as AssetManagerWithHotReload)?.ProcessChanges();

            updateContext.DeltaTime = (float)dt.TotalSeconds;
            updateContext.WindowSize = window.Size;

            sceneManager.Update(updateContext);

            ui.Update(updateContext);

            if (true)
            {
                ui.Overlay(Format($"FPS: {avgFramerate:F1}"));
                ui.Overlay(Format($"Alloc: {avgAllocedBytesPerFrame:F0} bytes/frame"));

                int gen0 = GC.CollectionCount(0);
                int gen1 = GC.CollectionCount(1);
                int gen2 = GC.CollectionCount(2);
                var gcPause = GC.GetTotalPauseDuration();
                ui.Overlay(Format($"GC: {gen0}/{gen1}/{gen2}/{gcPause.TotalMilliseconds:F1}ms"));

                ui.ShowDebuggingOverlay();
            }

            //
            // render
            //

            var (windowWidth, windowHeight) = window.Size;

            renderContext.DeltaTime = (float)dt.TotalSeconds;
            renderContext.WindowSize = window.Size;

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

            //
            // stats
            //

            totalTime += dt.TotalSeconds;
            numFrames++;

            if (totalTime > 0.5)
            {
                avgFramerate = numFrames / totalTime;

                //long gcTotal = GC.GetTotalAllocatedBytes(); // ignore other .net threads
                long gcTotal = GC.GetAllocatedBytesForCurrentThread();
                avgAllocedBytesPerFrame = (double)(gcTotal - gcTotalLast) / numFrames;

                gcTotalLast = gcTotal;
                totalTime = 0;
                numFrames = 0;
            }
        }

        // cleanup

        Console.WriteLine("after mainloop");

        Environment.Exit(0);
    }
}
