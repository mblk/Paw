using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Paw.Core.Utils;

public class SystemInfo
{
    public static void PrintSystemAndBuildInfo()
    {
        Console.WriteLine("Hello!");
        Console.WriteLine();
        Console.WriteLine($"RuntimeInformation:");
        Console.WriteLine($"  FrameworkDescription: {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"  RuntimeIdentifier:    {RuntimeInformation.RuntimeIdentifier}");
        Console.WriteLine($"  ProcessArchitecture:  {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"  OSArchitecture:       {RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"  OSDescription:        {RuntimeInformation.OSDescription}");
        Console.WriteLine();
        Console.WriteLine($"RuntimeFeature:");
        Console.WriteLine($"  IsDynamicCodeCompiled:  {RuntimeFeature.IsDynamicCodeCompiled}");
        Console.WriteLine($"  IsDynamicCodeSupported: {RuntimeFeature.IsDynamicCodeSupported}");
        Console.WriteLine();
        Console.WriteLine($"Environment:");
        Console.WriteLine($"  Version:          {Environment.Version}");
        Console.WriteLine($"  OSVersion:        {Environment.OSVersion}");
        Console.WriteLine($"  Is64BitOS:        {Environment.Is64BitOperatingSystem}");
        Console.WriteLine($"  Is64BitProcess:   {Environment.Is64BitProcess}");
        Console.WriteLine($"  ProcessorCount:   {Environment.ProcessorCount}");
        Console.WriteLine($"  CommandLine:      {Environment.CommandLine}");
        Console.WriteLine($"  CurrentDirectory: {Environment.CurrentDirectory}");
        Console.WriteLine($"  SystemDirectory:  {Environment.SystemDirectory}");
        Console.WriteLine($"  TickCount:        {Environment.TickCount}");
        Console.WriteLine();
        Console.WriteLine($"Build Info:");
#if DEBUG
        Console.WriteLine($"  Configuration: DEBUG");
#else
        Console.WriteLine($"  Configuration: RELEASE");
#endif

        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? throw new Exception("Missing assembly version");
        var buildDate = (assembly.GetCustomAttribute<BuildDateAttribute>() ?? throw new Exception("Missing BuildDateAttribute")).DateTime;

        Console.WriteLine($"  Version:       {version}");
        Console.WriteLine($"  Build Date:    {buildDate:yyyy-MM-dd HH:mm:ss} UTC");
    }
}
