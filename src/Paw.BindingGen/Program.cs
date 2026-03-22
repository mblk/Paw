namespace Paw.BindingGen;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("hello");

        var glSpecXml = new FileInfo(@"C:\workspace\repos\OpenGL-Registry\xml\gl.xml");
        var outputDir = new DirectoryInfo(@"C:\workspace\repos\Paw\src\Paw.Core\Engine\");

        var specGitInfo = GitInfo.FromDirectory(glSpecXml.DirectoryName!);
        Console.WriteLine($"Spec commit: {specGitInfo.CommitHash[..12]} ({specGitInfo.CommitDate}) {specGitInfo.CommitSubject}");

        var parser = new GlSpecParser(glSpecXml);
        var glSpec = parser.Parse();

        var generator = new GlBindingGenerator(glSpec, outputDir);
        generator.Generate(4, 6, specGitInfo);

        Console.WriteLine("bye");
    }
}

