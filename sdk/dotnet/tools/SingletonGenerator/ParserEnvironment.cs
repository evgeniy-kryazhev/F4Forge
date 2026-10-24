using System.Diagnostics;

namespace F4Forge.SingletonGenerator;

internal sealed record ParserEnvironment(string Source, string[] Arguments)
{
    internal static ParserEnvironment Create(string root)
    {
        var commonLib = Path.Combine(root, "native/lib/commonlibf4");
        var source = Path.Combine(root, "build/singletons/input.cpp");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        var headers = Directory.GetFiles(Path.Combine(commonLib, "include/RE"), "*.h", SearchOption.AllDirectories)
            .Where(header => File.ReadAllText(header).Contains("GetSingleton(", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(header => Path.GetRelativePath(Path.Combine(commonLib, "include"), header).Replace('\\', '/'));
        File.WriteAllText(source, "#include <F4SE/Impl/PCH.h>\n#include <RE/Fallout.h>\n" +
            string.Join("\n", headers.Select(header => $"#include <{header}>")) + "\n");

        var arguments = new List<string>
        {
            "-x", "c++", "-std=c++23", "--target=x86_64-pc-windows-msvc", "-fms-extensions",
            "-fms-compatibility", "-fms-compatibility-version=19.51", "-Wno-everything",
            "-D_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH", "-DNOMINMAX", "-DWIN32_LEAN_AND_MEAN",
            "-I" + Path.Combine(commonLib, "include"),
            "-I" + Path.Combine(commonLib, "lib/commonlib-shared/include")
        };
        var visualStudio = FindVisualStudio();
        var toolset = LatestDirectory(Path.Combine(visualStudio, "VC/Tools/MSVC"));
        arguments.Add("-isystem" + Path.Combine(toolset, "include"));
        var kits = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Windows Kits/10/Include");
        var kit = LatestDirectory(kits);
        foreach (var directory in new[] { "ucrt", "shared", "um", "winrt" })
        {
            arguments.Add("-isystem" + Path.Combine(kit, directory));
        }
        var spdlogInclude = Environment.GetEnvironmentVariable("F4FORGE_SPDLOG_INCLUDE");
        if (string.IsNullOrWhiteSpace(spdlogInclude))
        {
            var packages = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE")!, "AppData/Local/.xmake/packages/s/spdlog");
            if (!Directory.Exists(packages))
            {
                throw new InvalidOperationException("Configure xmake dependencies first or set F4FORGE_SPDLOG_INCLUDE to the spdlog include directory.");
            }
            var spdlog = Directory.GetFiles(packages, "spdlog.h", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal).LastOrDefault()
                ?? throw new InvalidOperationException("No spdlog headers were found in the xmake cache.");
            spdlogInclude = Directory.GetParent(Path.GetDirectoryName(spdlog)!)!.FullName;
        }
        if (!File.Exists(Path.Combine(spdlogInclude, "spdlog/spdlog.h")))
        {
            throw new InvalidOperationException("F4FORGE_SPDLOG_INCLUDE must contain spdlog/spdlog.h.");
        }
        arguments.Add("-I" + spdlogInclude);
        return new ParserEnvironment(source, arguments.ToArray());
    }

    private static string FindVisualStudio()
    {
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio/Installer/vswhere.exe");
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "-latest", "-products", "*", "-requires", "Microsoft.VisualStudio.Component.VC.Tools.x86.x64", "-property", "installationPath" })
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start vswhere.");
        var path = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        if (process.ExitCode != 0 || !Directory.Exists(path))
        {
            throw new InvalidOperationException("Install Visual Studio C++ x64 tools before generating singleton bindings.");
        }
        return path;
    }

    private static string LatestDirectory(string parent)
    {
        return Directory.GetDirectories(parent).OrderBy(path => Version.Parse(Path.GetFileName(path))).Last();
    }
}
