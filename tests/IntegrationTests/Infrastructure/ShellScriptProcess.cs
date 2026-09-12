using System.Diagnostics;

namespace IntegrationTests.Infrastructure;

internal static class ShellScriptProcess
{
    internal static bool IsNativeCommandAvailable(string executableName)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => directory.Trim().Trim('"'))
            .Any(directory => File.Exists(Path.Combine(directory, executableName)));
    }

    internal static ProcessStartInfo Create(
        string fileName,
        IReadOnlyList<string>? arguments = null)
    {
        bool isBashCommand = string.Equals(fileName, "bash", StringComparison.OrdinalIgnoreCase);
        bool isShellScript = fileName.EndsWith(".sh", StringComparison.OrdinalIgnoreCase);
        var startInfo = new ProcessStartInfo(
            isBashCommand || isShellScript ? ResolveBashExecutable() : fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        if (isShellScript)
        {
            startInfo.ArgumentList.Add(fileName);
        }

        foreach (string argument in arguments ?? [])
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static string ResolveBashExecutable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "bash";
        }

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string[] candidates =
        [
            Path.Combine(programFiles, "Git", "bin", "bash.exe"),
            Path.Combine(programFiles, "Git", "usr", "bin", "bash.exe")
        ];

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException(
                "Bash is required for shell-script contract tests. Install Git for Windows or run the tests on a host with Bash.");
    }
}
