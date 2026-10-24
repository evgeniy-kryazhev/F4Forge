using ClangSharp.Interop;

namespace F4Forge.SingletonGenerator;

internal static class Program
{
    private static int Main(string[] arguments)
    {
        try
        {
            var root = Path.GetFullPath(arguments.Length == 0 ? "." : arguments[0]);
            if (arguments.Length == 2 && arguments[1] == "--self-test")
            {
                GeneratorTests.Run(root);
                return 0;
            }
            var options = ParserEnvironment.Create(root);
            using var index = CXIndex.Create();
            using var translationUnit = CXTranslationUnit.Parse(index, options.Source, options.Arguments, [],
                CXTranslationUnit_Flags.CXTranslationUnit_SkipFunctionBodies);
            var errors = new List<string>();
            for (uint diagnosticIndex = 0; diagnosticIndex < translationUnit.NumDiagnostics; diagnosticIndex++)
            {
                using var diagnostic = translationUnit.GetDiagnostic(diagnosticIndex);
                if (diagnostic.Severity >= CXDiagnosticSeverity.CXDiagnostic_Error)
                {
                    errors.Add(diagnostic.Format(CXDiagnostic.DefaultDisplayOptions).ToString());
                }
            }
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
            }
            var model = BindingModel.Read(translationUnit.Cursor, Path.Combine(root, "native/lib/commonlibf4/include"));
            if (model.Singletons.Count == 0)
            {
                throw new InvalidOperationException("No CommonLibF4 singletons were discovered.");
            }
            BindingWriter.Write(model, root);
            Console.WriteLine($"Generated {model.Singletons.Count} singleton proxies and {model.Singletons.Sum(singleton => singleton.Members.Count)} members; {model.Skipped.Count} declarations skipped.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
