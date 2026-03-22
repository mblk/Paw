using System.Diagnostics;

namespace Paw.BindingGen;

internal record GitInfo(string CommitHash, string CommitDate, string CommitSubject)
{
    public static GitInfo FromDirectory(string directory)
    {
        string output = RunGit(directory, "log -1 --format=%H|%ai|%s");

        string[] parts = output.Split('|', 3);

        if (parts.Length != 3)
            throw new InvalidOperationException($"Failed to parse git log output: '{output}'");

        return new GitInfo(
            CommitHash: parts[0].Trim(),
            CommitDate: parts[1].Trim(),
            CommitSubject: parts[2].Trim());
    }

    private static string RunGit(string workingDirectory, string arguments)
    {
        using var process = new Process();

        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        process.Start();

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git exited with code {process.ExitCode}: {error}");

        return output;
    }
}
