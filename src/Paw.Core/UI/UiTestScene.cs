using Paw.Core.Resources;
using System.Numerics;

namespace Paw.Core.UI;

public class UiTestScene : Scene
{
    private int _mouseX;
    private int _mouseY;

    private int _numFrames;
    private double _totalTime;
    private double _avgFramerate;


    private UI UI { get; set; } = null!;


    private string _string1 = "Hello";
    private string _string2 = "World";
    private string _string3 = "";
    private string _string4 = "";

    private bool _showWindow3;
    private bool _showWindow4;



    public UiTestScene(SceneContext context)
        : base(context)
    {
    }

    public override void Load()
    {
        UI = new UI(AssetManager);
    }

    public override void Unload()
    {
        UI.Dispose();
    }

    public override void Update(UpdateContext context)
    {
        if (context.Input.Keyboard.WasPressed(Platforms.Key.Escape))
        {
            context.SceneController.RequestExit();
        }

        // TODO add Input to RenderContext?
        _mouseX = context.Input.Mouse.X;
        _mouseY = context.Input.Mouse.Y;

        UI.Update(context);
    }

    public override void Render(RenderContext context)
    {
        _totalTime += context.DeltaTime;
        _numFrames++;
        if (_totalTime > 0.25)
        {
            _avgFramerate = 1.0 / (_totalTime / _numFrames);
            _totalTime = 0;
            _numFrames = 0;
        }

        //
        // overlays
        //
        UI.Overlay($"Hello");
        UI.Overlay($"World");
        UI.Overlay($"FPS: {_avgFramerate:F1}");

        UI.ShowDebuggingOverlay();

        //
        // window 1
        //

        using (var window = UI.BeginWindow(new Vector2(500, 500), "window 1"))
        {
            UI.Label("label 1");
            UI.Label("label 2");

            if (UI.Button(_showWindow3 ? "hide window3" : "show window3"))
            {
                Console.WriteLine($"button 1");
                _showWindow3 = !_showWindow3;
            }
            if (UI.Button(_showWindow4 ? "hide window4" : "show window4"))
            {
                Console.WriteLine($"button 2");
                _showWindow4 = !_showWindow4;
            }

            UI.Input("string1", ref _string1);
            UI.Input("string2", ref _string2);
            UI.Input("string3", ref _string3);
            UI.Input("string4", ref _string4);

            using (UI.BeginScrollable(new Vector2(200, 100)))
            {
                UI.Label("label 1.1");
                UI.Label("label 1.2");
                if (UI.Button("button 1.3"))
                {
                    Console.WriteLine($"button 1.3");
                }
                if (UI.Button("button 1.4"))
                {
                    Console.WriteLine($"button 1.4");
                }
                UI.Label("label 1.5");
                UI.Label("label 1.6");
                if (UI.Button("button 1.7"))
                {
                    Console.WriteLine($"button 1.7");
                }
            }

            UI.Label("label 5");

            using (UI.BeginScrollable(new Vector2(1000, 100)))
            {
                UI.Label("label 6");
                UI.Label("label 7");
                UI.Label("label 8");
                UI.Label("label 9");
            }

            UI.Label("label 10");
        }

        //
        // window 2
        //

#if true
        UI.SetNextWindowPositionMode(UI.WindowPositionMode.Right);

        using (var window = UI.BeginWindow(new Vector2(300, 200), "window 2"))
        {
            //UI.Horizontal();

            if (UI.Button("button 10"))
            {
                Console.WriteLine($"button 10");
            }
            if (UI.Button("button 11"))
            {
                Console.WriteLine($"button 11");
            }

            using (UI.BeginTable(0.333f, 0.666f))
            {
                UI.Label("1");
                UI.NextColumn();

                UI.Label("2");
                UI.NextRow();

                UI.Label("3");
                UI.NextColumn();

                UI.Label("4");
                UI.NextRow();
            }
        }
#endif

        //
        // Window 3
        //

        if (_showWindow3)
        {
            UI.SetNextWindowPositionMode(UI.WindowPositionMode.Center);

            using (var window = UI.BeginWindow(new Vector2(100, 100), "window 3"))
            {
                UI.Label("hello");
            }
        }

        //
        // Window 4
        //

        if (_showWindow4)
        {
            UI.SetNextWindowPosition(new Vector2(_mouseX + 50, _mouseY + 50));

            using (var window = UI.BeginWindow(new Vector2(100, 100), "window 4"))
            {
                UI.Label("hello");
            }
        }

        //
        // Window 5
        //

        UI.SetNextWindowPositionMode(UI.WindowPositionMode.Bottom);

        using (var window = UI.BeginWindow(new Vector2(500, 200), "window 5"))
        {
            using (UI.BeginHorizontal())
            {
                UI.Button("button 1");
                UI.Label("label 1");
                UI.Button("button 2");
                UI.Label("label 2");
                UI.Button("button 3");
                UI.Label("label 3");

                using (UI.BeginVertical(100))
                {
                    UI.Label("label a");
                    UI.Label("label b");
                    UI.Label("label c");
                    UI.Button("button d");
                }

                UI.Button("button 4");
                UI.Label("label 4");

            }

        }


        //
        //
        //
        UI.Render(context);
    }
}
