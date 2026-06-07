using Paw.Core;
using Paw.Core.Physics;
using Paw.Core.Platforms;
using Paw.Core.Resources;
using Paw.Core.Scenes;
using Paw.Core.UI;
using Paw.Demo.Scenes;
using System.Diagnostics;

namespace Paw.Demo;

public class Program : App
{
#if true
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
        sceneManager.RegisterScene("physicstest", c => new PhysicsTestScene(c));

        //sceneManager.SetCurrentScene("uitest1");
        //sceneManager.SetCurrentScene("materialbrowser");
        sceneManager.SetCurrentScene("physicstest");

        //sceneManager.SetCurrentScene("menu");
    }
#endif

    //
    // performance tests
    //

#if false
    private static void Main(string[] args)
    {
        // Result in Release build with NativeAOT:
        // size = 1000000 seq = 0,001 random = 0,002 >> 0,391 | 2,560x
        // size = 5000000 seq = 0,004 random = 0,027 >> 0,143 | 7,010x
        // size = 10000000 seq = 0,008 random = 0,063 >> 0,123 | 8,157x
        // size = 50000000 seq = 0,039 random = 0,447 >> 0,088 | 11,373x
        // size = 100000000 seq = 0,077 random = 0,972 >> 0,080 | 12,549x
        // size = 250000000 seq = 0,193 random = 3,761 >> 0,051 | 19,517x
        // size = 500000000 seq = 0,382 random = 15,306 >> 0,025 | 40,098x
        // size = 1000000000 seq = 1,000 random = 44,606 >> 0,022 | 44,625x
        // Note: C version had exactly the same numbers

        SequentialVsRandom(1_000_000);
        SequentialVsRandom(5_000_000);
        SequentialVsRandom(10_000_000);
        SequentialVsRandom(50_000_000);
        SequentialVsRandom(100_000_000);
        SequentialVsRandom(250_000_000);
        SequentialVsRandom(500_000_000);
        SequentialVsRandom(1_000_000_000);
    }

    public static double SequentialVsRandom(int size)
    {
        var r = new Random(123);

        double[] data = new double[size];
        int[] order = new int[size];

        for (int i = 0; i < size; i++)
        {
            data[i] = r.NextDouble();
            order[i] = i;
        }

        r.Shuffle(order);

        //
        // seq
        //

        long t1 = Stopwatch.GetTimestamp();
        double sum = 0;
        for (int i = 0; i < size; i++)
        {
            sum += data[i];
        }
        long t2 = Stopwatch.GetTimestamp();

        //
        // random
        //

        long t3 = Stopwatch.GetTimestamp();
        for (int i = 0; i < size; i++)
        {
            sum += data[order[i]];
        }
        long t4 = Stopwatch.GetTimestamp();

        //
        // ---
        //

        double dt1 = Stopwatch.GetElapsedTime(t1, t2).TotalSeconds;
        double dt2 = Stopwatch.GetElapsedTime(t3, t4).TotalSeconds;

        Console.WriteLine($"size={size} seq={dt1:F3} random={dt2:F3} >> {(dt1 / dt2):F3} | {(dt2 / dt1):F3}x");

        return sum;
    }
#endif
}