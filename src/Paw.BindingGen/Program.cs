namespace Paw.BindingGen;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Paw.BindingGen");
            Console.WriteLine("==============");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  Paw.BindingGen [gl spec repo] [gl bindings dir]");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine(@"  Paw.BindingGen C:\workspace\repos\OpenGL-Registry\ C:\workspace\repos\Paw\src\Paw.Core\Engine\");
            Console.WriteLine(@"  Paw.BindingGen ~/repos/OpenGL-Registry/ ~/repos/Paw/src/Paw.Core/Engine/");
            Console.WriteLine();
            return;
        }

        // Check input
        var glSpecDir = new DirectoryInfo(args[0]);
        var glBindingDir = new DirectoryInfo(args[1]);

        var glSpecFile = new FileInfo(Path.Combine(glSpecDir.FullName, "xml", "gl.xml"));
        var glBindingFile = new FileInfo(Path.Combine(glBindingDir.FullName, "GL.cs"));

        if (!glSpecFile.Exists)
        {
            Console.WriteLine($"GL spec file not found at: {glSpecFile.FullName}");
            return;
        }

        if (!glBindingFile.Exists)
        {
            Console.WriteLine($"GL binding file not found at: {glBindingFile.FullName}");
            return;
        }

        Console.WriteLine($"GL spec dir:    {glSpecDir.FullName}");
        Console.WriteLine($"GL binding dir: {glBindingDir.FullName}");

        // Generate bindings
        var specGitInfo = GitInfo.FromDirectory(glSpecDir);
        Console.WriteLine($"Spec commit: {specGitInfo.CommitHash[..12]} ({specGitInfo.CommitDate}): {specGitInfo.CommitSubject}");

        var parser = new GlSpecParser(glSpecFile);
        var glSpec = parser.Parse();

        var generator = new GlBindingGenerator(glSpec, glBindingDir);
        generator.Generate(4, 6, specGitInfo);

        Console.WriteLine("All done");
    }
}

