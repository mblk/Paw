using Paw.Core.Resources;
using System.Numerics;

namespace Paw.Core.UI;

public class UiTestScene : Scene
{
    private int _mouseX;
    private int _mouseY;



    private string _string1 = "Hello";
    private string _string2 = "World";
    private float _float1 = 0;
    private float _float2 = 12.34f;

    private int _rowCount = 10;
    private int _colCount = 10;

    private bool _showWindow3;
    private bool _showWindow4;

    private float _time;


    public UiTestScene(SceneContext context)
        : base(context)
    {
    }

    public override void Load()
    {
    }

    public override void Unload()
    {
    }

    public override void Update(UpdateContext context)
    {
        if (context.Input.Keyboard.WasPressed(Platforms.Key.Escape))
        {
            context.SceneController.RequestSceneChange("menu");
        }

        // TODO add Input to RenderContext?
        _mouseX = context.Input.Mouse.X;
        _mouseY = context.Input.Mouse.Y;
    }

    public override void Render(RenderContext context)
    {
        _time += context.DeltaTime;

        //
        // overlays
        //
        UI.Overlay($"Hello");
        UI.Overlay($"World");

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
            UI.Input("float1", ref _float1);
            UI.Input("float2", ref _float2);
            UI.Label($"{_string1} {_string2} {_float1} {_float2}");

            using (UI.BeginScrollable(new Vector2(200, 200), "Scrollable1"))
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

                using (UI.BeginScrollable(new Vector2(200, 100), "Scrollable3"))
                {
                    for (int i = 0; i < 100; i++)
                    {
                        UI.Label($"Foo {i}");
                    }
                }

                if (UI.Button("button 1.8"))
                {
                    Console.WriteLine($"button 1.8");
                }
            }

            UI.Label("label 5");

            using (UI.BeginScrollable(new Vector2(1000, 100), "Scrollable2"))
            {
                UI.Label("label 6");
                UI.Label("label 7");
                //UI.Label("label 8");
                //UI.Label("label 9");
            }

            UI.Label("label 10");
        }

        //
        // window 2
        //

        UI.SetNextWindowPositionMode(UI.WindowPositionMode.Right);

        using (var window = UI.BeginWindow(new Vector2(300, 300), "window 2"))
        {
            if (UI.Button("button 10"))
            {
                Console.WriteLine($"button 10");
            }
            if (UI.Button("button 11"))
            {
                Console.WriteLine($"button 11");
            }

            UI.Input("Rows", ref _rowCount);
            UI.Input("Cols", ref _colCount);

            UI.Label($"Rows={_rowCount} Cols={_colCount}");

            int rows = _rowCount;
            if (rows < 1) rows = 1;

            int cols = _colCount;
            if (cols < 1) cols = 1;

            using (UI.BeginScrollable(new Vector2(0, 200), "Scroll1", UI.ScrollFlags.Both))
            {
                UI.Label("a111");

                var colWidths = Enumerable.Range(0, cols).Select(_ => 100f).ToArray();

                using (UI.BeginTable(colWidths))
                {
                    for (int row = 0; row < rows; row++)
                    {
                        for (int col = 0; col < cols; col++)
                        {
                            UI.Button($"{row}.{col}");

                            if (col < cols - 1) // XXX
                                UI.NextColumn();
                        }
                        UI.NextRow();
                    }
                }

                UI.Label("bbb");
            }

            using (UI.BeginScrollable(new Vector2(0, 100), "Scroll2"))
            {
                UI.Label("bbb1");

                using (UI.BeginCanvas())
                {
                    UI.SetCanvasPosition(new Vector2(0, 0));
                    UI.Button("c1");

                    UI.SetCanvasPosition(new Vector2(100, 0));
                    UI.Button("c2");

                    UI.SetCanvasPosition(new Vector2(100, 100));
                    UI.Button("c3");

                    float x = 100f + 100f * MathF.Sin(_time);
                    float y = 100f + 100f * MathF.Cos(_time);

                    UI.SetCanvasPosition(new Vector2(x, y));
                    UI.Button("c4");
                }

                UI.Label("bbb2");

            }

            UI.Label("ccc");

            if (UI.Button("button 12"))
            {
                Console.WriteLine($"button 11");
            }
        }

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
    }
}
