using System.Security.Cryptography;
using System.Text;
using ClangSharp.Interop;

namespace F4Forge.SingletonGenerator;

internal sealed record WireType(string Managed, string Native, bool IsString = false, bool IsEnum = false,
    string? ArgumentCast = null);
internal sealed record Parameter(string Name, WireType Type);
internal sealed record Member(string Name, string NativeName, string Route, WireType Result,
    IReadOnlyList<Parameter> Parameters, bool IsField);
internal sealed record Singleton(string Name, string NativeName, string Header, bool ReturnsPointer,
    IReadOnlyList<Member> Members);
internal sealed record EnumBinding(string Name, string UnderlyingType, IReadOnlyList<(string Name, string Value)> Values);

internal sealed class BindingModel
{
    internal List<Singleton> Singletons { get; } = [];
    internal SortedDictionary<string, EnumBinding> Enums { get; } = new(StringComparer.Ordinal);
    internal List<string> Skipped { get; } = [];

    internal static BindingModel Read(CXCursor root, string includeDirectory)
    {
        var model = new BindingModel();
        model.Visit(root, Path.GetFullPath(includeDirectory));
        model.Singletons.Sort((left, right) => StringComparer.Ordinal.Compare(left.NativeName, right.NativeName));
        var duplicates = model.Singletons.GroupBy(singleton => singleton.Name).Where(group => group.Count() > 1).ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException("Duplicate managed singleton names: " + string.Join(", ", duplicates.Select(group => group.Key)));
        }
        return model;
    }

    private void Visit(CXCursor cursor, string includeDirectory)
    {
        foreach (var child in Children(cursor))
        {
            if (child.Kind == CXCursorKind.CXCursor_Namespace)
            {
                Visit(child, includeDirectory);
            }
            else if (child.Kind is CXCursorKind.CXCursor_ClassDecl or CXCursorKind.CXCursor_StructDecl && child.IsDefinition)
            {
                child.Location.GetSpellingLocation(out var file, out _, out _, out _);
                var path = Path.GetFullPath(file.Name.ToString());
                if (!path.StartsWith(includeDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                ReadSingleton(child, path, includeDirectory);
                Visit(child, includeDirectory);
            }
        }
    }

    private void ReadSingleton(CXCursor record, string path, string includeDirectory)
    {
        var children = Children(record);
        var getters = children.Where(child => child.Kind == CXCursorKind.CXCursor_CXXMethod &&
            child.Spelling.ToString() == "GetSingleton" && child.CXXMethod_IsStatic && child.NumArguments == 0 &&
            child.CXXAccessSpecifier == CX_CXXAccessSpecifier.CX_CXXPublic).ToArray();
        if (getters.Length == 0)
        {
            return;
        }
        var getter = getters.Single();
        var nativeName = QualifiedName(record);
        var result = getter.ResultType.CanonicalType;
        if (result.kind is not (CXTypeKind.CXType_Pointer or CXTypeKind.CXType_LValueReference) ||
            result.PointeeType.CanonicalType != record.Type.CanonicalType)
        {
            Skipped.Add($"{nativeName}: GetSingleton does not return its enclosing type by pointer or reference.");
            return;
        }
        var name = ManagedTypeName(nativeName);
        var members = new List<Member>();
        foreach (var child in children)
        {
            if (child.CXXAccessSpecifier != CX_CXXAccessSpecifier.CX_CXXPublic)
            {
                continue;
            }
            var memberName = child.Spelling.ToString();
            if (child.Kind == CXCursorKind.CXCursor_FieldDecl)
            {
                var type = MapType(child.Type, false);
                if (type is null || child.Type.IsVolatileQualified)
                {
                    Skipped.Add($"{nativeName}::{memberName}: field type {child.Type.Spelling} is unsupported.");
                    continue;
                }
                var property = char.ToUpperInvariant(memberName[0]) + memberName[1..];
                members.Add(new Member(property, memberName, $"singletons.{nativeName}.{memberName}.get", type, [], true));
            }
            else if (child.Kind == CXCursorKind.CXCursor_CXXMethod && memberName != "GetSingleton")
            {
                var returnType = MapType(child.ResultType, true);
                var parameters = new List<Parameter>();
                for (uint index = 0; index < child.NumArguments; index++)
                {
                    var argument = child.GetArgument(index);
                    var type = MapType(argument.Type, false, parameter: true);
                    if (type is null)
                    {
                        break;
                    }
                    var argumentName = argument.Spelling.ToString();
                    if (argumentName.StartsWith("a_", StringComparison.Ordinal))
                    {
                        argumentName = argumentName[2..];
                    }
                    if (argumentName.Length == 0)
                    {
                        argumentName = $"argument{index}";
                    }
                    else
                    {
                        argumentName = char.ToLowerInvariant(argumentName[0]) + argumentName[1..];
                    }
                    parameters.Add(new Parameter(argumentName, type));
                }
                if (child.CXXMethod_IsStatic || child.CXXMethod_IsVirtual || child.IsVariadic ||
                    !child.IsFunctionInlined || returnType is null || parameters.Count != child.NumArguments ||
                    memberName.StartsWith("operator", StringComparison.Ordinal) ||
                    memberName is "Equals" or "GetHashCode" or "GetType" or "ToString")
                {
                    Skipped.Add($"{nativeName}::{child.DisplayName}: requires an unsupported signature, virtual dispatch, or out-of-line definition.");
                    continue;
                }
                var signature = child.Type.Spelling.ToString();
                var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(signature)))[..12];
                members.Add(new Member(memberName, memberName, $"singletons.{nativeName}.{memberName}.{hash}", returnType, parameters, false));
            }
            else if (child.Kind == CXCursorKind.CXCursor_FunctionTemplate)
            {
                Skipped.Add($"{nativeName}::{child.Spelling}: templates require an explicit instantiation.");
            }
        }
        var conflicts = members.GroupBy(member => member.Name + "(" + string.Join(",", member.Parameters.Select(parameter => parameter.Type.Managed)) + ")")
            .Where(group => group.Count() > 1).SelectMany(group => group).ToHashSet();
        foreach (var conflict in conflicts)
        {
            Skipped.Add($"{nativeName}::{conflict.NativeName}: colliding managed signature.");
        }
        members.RemoveAll(member => conflicts.Contains(member) || member.Name == name || member.Name == "IsAvailable");
        Singletons.Add(new Singleton(name, nativeName, Path.GetRelativePath(includeDirectory, path).Replace('\\', '/'),
            result.kind == CXTypeKind.CXType_Pointer, members.OrderBy(member => member.Route, StringComparer.Ordinal).ToArray()));
    }

    private WireType? MapType(CXType original, bool allowVoid, bool parameter = false)
    {
        var type = original.CanonicalType;
        if (type.kind == CXTypeKind.CXType_LValueReference)
        {
            if (!parameter || !type.PointeeType.IsConstQualified)
            {
                return null;
            }
            type = type.PointeeType.CanonicalType;
        }
        var spelling = type.Spelling.ToString();
        if (spelling is "RE::detail::BSFixedString<char, false>" or "const RE::detail::BSFixedString<char, false>")
        {
            return new WireType("string", "RE::BSFixedString", IsString: true);
        }
        if (type.kind == CXTypeKind.CXType_Enum)
        {
            var declaration = type.Declaration.Definition;
            if (declaration.IsNull)
            {
                return null;
            }
            var underlying = MapType(declaration.EnumDecl_IntegerType, false);
            if (underlying is null)
            {
                return null;
            }
            var native = QualifiedName(declaration);
            var name = ManagedTypeName(native);
            if (!Enums.ContainsKey(name))
            {
                var unsigned = underlying.Managed is "byte" or "ushort" or "uint" or "ulong";
                var values = Children(declaration).Where(child => child.Kind == CXCursorKind.CXCursor_EnumConstantDecl)
                    .Select(child => (child.Spelling.ToString(), unsigned
                        ? child.EnumConstantDeclUnsignedValue.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : child.EnumConstantDeclValue.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToArray();
                Enums.Add(name, new EnumBinding(name, underlying.Managed, values));
            }
            return new WireType(name, native, IsEnum: true);
        }
        var scalar = type.kind switch
        {
            CXTypeKind.CXType_Void when allowVoid => new WireType("void", "void"),
            CXTypeKind.CXType_Bool => new WireType("bool", "bool"),
            CXTypeKind.CXType_Char_S or CXTypeKind.CXType_SChar => new WireType("sbyte", "std::int8_t"),
            CXTypeKind.CXType_Char_U or CXTypeKind.CXType_UChar => new WireType("byte", "std::uint8_t"),
            CXTypeKind.CXType_Short => new WireType("short", "std::int16_t"),
            CXTypeKind.CXType_UShort => new WireType("ushort", "std::uint16_t"),
            CXTypeKind.CXType_Int or CXTypeKind.CXType_Long when type.SizeOf == 4 => new WireType("int", "std::int32_t"),
            CXTypeKind.CXType_UInt or CXTypeKind.CXType_ULong when type.SizeOf == 4 => new WireType("uint", "std::uint32_t"),
            CXTypeKind.CXType_LongLong => new WireType("long", "std::int64_t"),
            CXTypeKind.CXType_ULongLong => new WireType("ulong", "std::uint64_t"),
            CXTypeKind.CXType_Float => new WireType("float", "float"),
            CXTypeKind.CXType_Double => new WireType("double", "double"),
            _ => null
        };
        // Wire integers use fixed widths, but overload resolution needs the exact C++ type.
        return scalar is null ? null : scalar with { ArgumentCast = spelling.Replace("const ", "", StringComparison.Ordinal) };
    }

    private static string ManagedTypeName(string nativeName)
    {
        return (nativeName.StartsWith("RE::", StringComparison.Ordinal) ? nativeName[4..] : nativeName).Replace("::", "_");
    }

    private static string QualifiedName(CXCursor cursor)
    {
        var names = new List<string>();
        while (!cursor.IsNull && cursor.Kind != CXCursorKind.CXCursor_TranslationUnit)
        {
            names.Add(cursor.Spelling.ToString());
            cursor = cursor.SemanticParent;
        }
        names.Reverse();
        return string.Join("::", names);
    }

    private static unsafe IReadOnlyList<CXCursor> Children(CXCursor cursor)
    {
        var children = new List<CXCursor>();
        Exception? failure = null;
        cursor.VisitChildren((child, _, _) =>
        {
            try
            {
                children.Add(child);
                return CXChildVisitResult.CXChildVisit_Continue;
            }
            catch (Exception exception)
            {
                // Never unwind a managed exception through libClang's native callback.
                failure = exception;
                return CXChildVisitResult.CXChildVisit_Break;
            }
        }, default);
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
        return children;
    }
}
