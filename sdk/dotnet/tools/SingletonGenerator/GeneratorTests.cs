using ClangSharp.Interop;

namespace F4Forge.SingletonGenerator;

internal static class GeneratorTests
{
    internal static void Run(string root)
    {
        var include = Path.Combine(root, "native/tests/singletons/fixtures");
        var input = Path.Combine(include, "RE/SingletonFixture.h");
        using var index = CXIndex.Create();
        using var unit = CXTranslationUnit.Parse(index, input,
            ["-x", "c++", "-std=c++23", "--target=x86_64-pc-windows-msvc"], [],
            CXTranslationUnit_Flags.CXTranslationUnit_SkipFunctionBodies);
        for (uint diagnosticIndex = 0; diagnosticIndex < unit.NumDiagnostics; diagnosticIndex++)
        {
            using var diagnostic = unit.GetDiagnostic(diagnosticIndex);
            Require(diagnostic.Severity < CXDiagnosticSeverity.CXDiagnostic_Error, diagnostic.Spelling.ToString());
        }
        var model = BindingModel.Read(unit.Cursor, include);
        Require(model.Singletons.Count == 2, "Only genuine singleton declarations should be discovered.");
        var singleton = model.Singletons.Single(item => item.Name == "FixtureSingleton");
        Require(singleton.ReturnsPointer, "Pointer singleton must retain nullability.");
        Require(!model.Singletons.Single(item => item.Name == "FixtureReferenceSingleton").ReturnsPointer,
            "Reference singleton must generate address-of access.");
        Require(singleton.Members.Count(member => member.Name == "Overload") == 2, "Supported overloads must be preserved.");
        Require(singleton.Members.Where(member => member.Name == "Overload").Select(member => member.Route).Distinct().Count() == 2,
            "Overloads must have distinct stable routes.");
        Require(singleton.Members.Any(member => member.Name == "Echo" && member.Parameters[0].Type.Managed == "string"),
            "BSFixedString must map to a copied string.");
        Require(singleton.Members.Any(member => member.Name == "Flags" && member.IsField), "Bitfields must be copied by value.");
        foreach (var unsupported in new[] { "BorrowPointer", "MutateReference", "StaticMethod", "VirtualMethod", "TemplateMethod", "HiddenMethod" })
        {
            Require(singleton.Members.All(member => member.Name != unsupported), "Unsupported member was exposed: " + unsupported);
        }
        Require(model.Enums["FixtureMode"].Values.Any(value => value.Value == "4294967295"), "Unsigned enum values must not overflow.");
        Require(model.Skipped.Any(message => message.Contains("MutateReference", StringComparison.Ordinal)), "Skipped declarations must be explained.");

        var outputRoot = Path.Combine(root, "build/singletons/fixture");
        BindingWriter.Write(model, outputRoot);
        var output = Path.Combine(outputRoot, "native/modules/singletons/generated.inc");
        var original = File.ReadAllText(output);
        var timestamp = File.GetLastWriteTimeUtc(output);
        BindingWriter.Write(model, outputRoot);
        Require(File.ReadAllText(output) == original && File.GetLastWriteTimeUtc(output) == timestamp,
            "Unchanged input must generate identical output without rewriting files.");
        var managed = File.ReadAllText(Path.Combine(outputRoot, "sdk/dotnet/F4Forge.Net/Singletons/Generated/Singletons.g.cs"));
        Require(managed.Contains("IGameSingleton<FixtureSingleton>", StringComparison.Ordinal), "Generic proxy factory was not generated.");
        Require(managed.Contains("ArgumentNullException.ThrowIfNull", StringComparison.Ordinal), "Generated strings must validate null.");
        Console.WriteLine("Singleton generator tests passed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
