namespace Paw.Core.Utils;

public static class DebugUtils
{
    public static void DumpToConsole(this Span<byte> data, string? title = null)
    {
        ((ReadOnlySpan<byte>)data).DumpToConsole(title);
    }

    public static void DumpToConsole(this ReadOnlySpan<byte> data, string? title = null)
    {
        Console.WriteLine($"== DumpToConsole {title} ==");

        for (int i=0; i<data.Length; i++)
        {
            if (i % 32 == 0) Console.Write($"[{i:X4}] ");
            Console.Write($"{data[i]:X2} ");
            if ((i+1) % 32 == 0) Console.WriteLine();
        }

        Console.WriteLine();
    }



}