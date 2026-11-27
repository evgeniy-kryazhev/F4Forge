#include "clang_helpers.h"
#include "event_catalog.h"

#include <algorithm>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <map>
#include <memory>
#include <optional>
#include <set>
#include <sstream>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

namespace fs = std::filesystem;

namespace {

bool Identifier(const std::string& name)
{
    return !name.empty() && (name.front() < '0' || name.front() > '9')
        && name.find_first_not_of("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_0123456789") == std::string::npos;
}

bool GameRecord(CXCursor record)
{
    const auto kind = clang_getCursorKind(record);
    if (kind != CXCursor_ClassDecl && kind != CXCursor_StructDecl) {
        return false;
    }
    const auto scope = clang_getCursorSemanticParent(record);
    return clang_getCursorKind(scope) == CXCursor_Namespace && Text(clang_getCursorSpelling(scope)) == "RE"
        && clang_getCursorKind(clang_getCursorSemanticParent(scope)) == CXCursor_TranslationUnit
        && clang_Cursor_isNull(clang_getSpecializedCursorTemplate(record))
        && Identifier(Text(clang_getCursorSpelling(record)));
}

struct Type {
    std::string native;
    std::string managed;
};

std::optional<Type> MapType(CXType original, bool result)
{
    const auto type = clang_getCanonicalType(original);
    switch (type.kind) {
    case CXType_Void: return Type{"void", "void"};
    case CXType_Bool: return Type{"bool", "bool"};
    case CXType_Int: return Type{"int", "int"};
    case CXType_UInt: return Type{"unsigned int", "uint"};
    case CXType_Float: return Type{"float", "float"};
    case CXType_Enum: {
        const auto declaration = clang_getTypeDeclaration(type);
        const auto scope = clang_getCursorSemanticParent(declaration);
        const auto enumName = Text(clang_getCursorSpelling(declaration));
        if (!Identifier(enumName)) {
            return std::nullopt;
        }
        std::string qualified = "RE::";
        if (GameRecord(scope) && clang_getCXXAccessSpecifier(declaration) == CX_CXXPublic) {
            qualified += Text(clang_getCursorSpelling(scope)) + "::";
        } else if (clang_getCursorKind(scope) != CXCursor_Namespace || Text(clang_getCursorSpelling(scope)) != "RE"
            || clang_getCursorKind(clang_getCursorSemanticParent(scope)) != CXCursor_TranslationUnit) {
            return std::nullopt;
        }
        const auto underlying = clang_getCanonicalType(clang_getEnumDeclIntegerType(declaration));
        if (underlying.kind != CXType_Int && underlying.kind != CXType_UInt) {
            return std::nullopt;
        }
        return Type{qualified + enumName, underlying.kind == CXType_Int ? "int" : "uint"};
    }
    case CXType_LValueReference:
        if (result) {
            return std::nullopt;
        }
        [[fallthrough]];
    case CXType_Pointer: {
        const auto object = clang_getCanonicalType(clang_getPointeeType(type));
        const auto record = clang_getTypeDeclaration(object);
        if (!GameRecord(record) || clang_isVolatileQualifiedType(object)) {
            return std::nullopt;
        }
        std::string name = clang_isConstQualifiedType(object) ? "const RE::" : "RE::";
        name += Text(clang_getCursorSpelling(record));
        name += type.kind == CXType_Pointer ? "*" : "&";
        return Type{name, "nint"};
    }
    default: return std::nullopt;
    }
}

struct Parameter {
    Type type;
    std::string name;
};

struct Method {
    std::string owner;
    std::string name;
    bool isStatic;
    bool isConst;
    Type result;
    std::vector<Parameter> parameters;

    std::string ParameterTypes() const
    {
        std::string types;
        for (const auto& parameter : parameters) {
            if (!types.empty()) {
                types += ", ";
            }
            types += parameter.type.native;
        }
        return types;
    }

    std::string Route() const
    {
        return "RE::" + owner + "::" + name + "(" + ParameterTypes() + ")" + (isConst ? " const" : "");
    }
};

struct Catalog {
    EventCatalog events;
    std::map<std::string, Method> methods;
    std::vector<std::string> skipped;

    void ReadMethod(CXCursor cursor, const std::string& owner)
    {
        const auto name = Text(clang_getCursorSpelling(cursor));
        const auto skip = [&](std::string reason) {
            skipped.push_back("RE::" + owner + "::" + Text(clang_getCursorDisplayName(cursor)) + " — " + reason);
        };
        if (clang_getCXXAccessSpecifier(cursor) != CX_CXXPublic || !Identifier(name)
            || clang_CXXMethod_isDeleted(cursor) || clang_getCursorAvailability(cursor) != CXAvailability_Available
            || clang_Cursor_isVariadic(cursor) || clang_Type_getCXXRefQualifier(clang_getCursorType(cursor)) != CXRefQualifier_None
            || Text(clang_getTypeSpelling(clang_getCursorType(cursor))).find(" volatile") != std::string::npos) {
            skip("unsupported declaration or access");
            return;
        }
        if (!clang_CXXMethod_isVirtual(cursor) && !clang_Cursor_isFunctionInlined(cursor)) {
            skip("no inline or virtual implementation");
            return;
        }
        const auto nativeResult = clang_getCanonicalType(clang_getCursorResultType(cursor));
        auto result = MapType(nativeResult, true);
        if (!result) {
            skip("unsupported result type");
            return;
        }
        if (clang_isConstQualifiedType(nativeResult)) {
            result->native += " const";
        }
        Method method{owner, name, clang_CXXMethod_isStatic(cursor) != 0, clang_CXXMethod_isConst(cursor) != 0, *result, {}};
        std::set<std::string> usedNames{"self"};
        for (int index = 0; index < clang_Cursor_getNumArguments(cursor); ++index) {
            const auto argument = clang_Cursor_getArgument(cursor, index);
            const auto parameter = MapType(clang_getCursorType(argument), false);
            if (!parameter) {
                skip("unsupported argument: " + Text(clang_getTypeSpelling(clang_getCursorType(argument))));
                return;
            }
            auto argumentName = Text(clang_getCursorSpelling(argument));
            if (argumentName.starts_with("a_") && Identifier(argumentName.substr(2))) {
                argumentName.erase(0, 2);
            }
            if (!Identifier(argumentName)) {
                argumentName = "arg" + std::to_string(index);
            }
            while (!usedNames.insert(argumentName).second) {
                argumentName += "_";
            }
            method.parameters.push_back({*parameter, "@" + argumentName});
        }
        const auto key = method.Route();
        if (!methods.emplace(key, std::move(method)).second) {
            throw std::runtime_error("Duplicate binding: " + key);
        }
    }

    void Read(CXCursor root, const fs::path& includeRoot)
    {
        for (const auto scope : Children(root)) {
            if (clang_getCursorKind(scope) != CXCursor_Namespace || Text(clang_getCursorSpelling(scope)) != "RE") {
                continue;
            }
            events.ReadScope(scope, includeRoot);
            for (const auto record : Children(scope)) {
                if (!GameRecord(record) || !clang_isCursorDefinition(record)) {
                    continue;
                }
                CXFile file{};
                clang_getSpellingLocation(clang_getCursorLocation(record), &file, nullptr, nullptr, nullptr);
                const auto relative = fs::absolute(Text(clang_getFileName(file))).lexically_normal().lexically_relative(includeRoot);
                if (relative.empty() || *relative.begin() == "..") {
                    continue;
                }
                for (const auto member : Children(record)) {
                    if (clang_getCursorKind(member) == CXCursor_CXXMethod) {
                        ReadMethod(member, Text(clang_getCursorSpelling(record)));
                    }
                }
            }
        }
        std::ranges::sort(skipped);
        if (methods.empty()) {
            throw std::runtime_error("No supported CommonLibF4 methods found.");
        }
    }
};

void WriteIfChanged(const fs::path& path, const std::string& contents)
{
    std::ifstream previous(path, std::ios::binary);
    if (previous && std::string(std::istreambuf_iterator<char>(previous), {}) == contents) {
        return;
    }
    previous.close();
    auto temporary = path;
    temporary += ".tmp";
    std::ofstream output(temporary, std::ios::binary);
    output.exceptions(std::ios::badbit | std::ios::failbit);
    output << contents;
    output.close();
    fs::rename(temporary, path);
}

std::string SignatureSuffix(std::string_view route)
{
    std::uint64_t hash = 14695981039346656037ull;
    for (const unsigned char value : route) {
        hash = (hash ^ value) * 1099511628211ull;
    }
    std::ostringstream suffix;
    suffix << '_' << std::hex << hash;
    return suffix.str();
}

void Emit(const Catalog& catalog, const fs::path& directory, const fs::path& umbrella)
{
    std::ostringstream native, managed, docs;
    native << "// Generated by HeaderParser.\n#include <" << umbrella.generic_string() << ">\n"
              "#include \"generated_method.h\"\n#include \"AutoEventSinks.h\"\n\nnamespace f4forge {\nvoid RegisterCommonLibMethods(MethodRegistry& registry)\n{\n";
    managed << "// <auto-generated/>\n#nullable enable\nnamespace F4Forge;\n\n"
               "/// <summary>Low-level bindings to CommonLibF4. Object addresses are borrowed.</summary>\n"
               "public static class AutoBindings\n{\n";
    docs << "# CommonLibF4 bindings\n\n" << catalog.methods.size() << " registered methods. "
         << "Instance methods take the borrowed address of the declaring C++ subobject as their first argument. "
            "References must be non-null. Callers must respect engine lifetimes and thread requirements.\n\n"
            "The full signature is a stable route. The generated wrapper name (without AutoBindings.) is also accepted by Invoke.\n\n"
            "| Bridge.Invoke name | C# wrapper | Result |\n|---|---|---|\n";
    std::map<std::string, int> nameCounts;
    for (const auto& [route, method] : catalog.methods) {
        ++nameCounts[method.owner + "_" + method.name];
    }
    for (const auto& [route, method] : catalog.methods) {
        const std::string owner = "RE::" + method.owner;
        const std::string pointer = method.isStatic ? "*" : owner + "::*";
        const std::string qualifier = method.isConst ? " const" : "";
        std::string wrapper = method.owner + "_" + method.name;
        if (nameCounts.at(wrapper) > 1) {
            wrapper += SignatureSuffix(route);
        }
        native << "    bindings::Register(registry, \"" << route << "\", \"" << wrapper << "\",\n        bindings::Call<static_cast<"
               << method.result.native << " (" << pointer << ")(" << method.ParameterTypes() << ")" << qualifier
               << ">(&" << owner << "::" << method.name << ")";
        std::vector<std::pair<std::string, std::string>> arguments;
        if (!method.isStatic) {
            native << ", " << (method.isConst ? "const " : "") << owner << "&";
            arguments.emplace_back("nint", "self");
        }
        for (const auto& parameter : method.parameters) {
            native << ", " << parameter.type.native;
            arguments.emplace_back(parameter.type.managed, parameter.name);
        }
        native << ">);\n";
        managed << "    /// <summary>Calls " << owner << "::" << method.name << ".</summary>\n";
        managed << "    public static " << method.result.managed << ' ' << wrapper << '(';
        for (std::size_t index = 0; index < arguments.size(); ++index) {
            if (index > 0) {
                managed << ", ";
            }
            managed << arguments[index].first << ' ' << arguments[index].second;
        }
        managed << ") =>\n        Bridge.";
        if (method.result.native == "void") {
            managed << "InvokeVoid";
        } else {
            managed << "Invoke<" << method.result.managed << ">";
        }
        managed << "(\"" << route << "\"";
        for (const auto& argument : arguments) {
            managed << ", " << argument.second;
        }
        managed << ");\n\n";
        docs << "| `" << route << "` | `AutoBindings." << wrapper << "` | `" << method.result.managed << "` |\n";
    }
    native << "}\n}\n";
    managed << "}\n";
    std::ostringstream eventHeader;
    eventHeader << "// Generated by HeaderParser.\n#pragma once\n#include <" << umbrella.generic_string()
                << ">\n#include \"commonlib_event_sink.h\"\n";
    catalog.events.Emit(eventHeader, native, managed, docs);
    docs << "\n## Skipped declarations in scanned records\n\n";
    for (const auto& entry : catalog.skipped) {
        docs << "- `" << entry << "`\n";
    }
    fs::create_directories(directory);
    WriteIfChanged(directory / "AutoRegistry.cpp", native.str());
    WriteIfChanged(directory / "AutoEventSinks.h", eventHeader.str());
    WriteIfChanged(directory / "AutoBindings.g.cs", managed.str());
    WriteIfChanged(directory / "API.md", docs.str());
}

}

int main(int argc, char** argv)
{
    try {
        if (argc < 4) {
            throw std::invalid_argument("Usage: HeaderParser <umbrella-header> <RE-include-root> <output-directory> [clang arguments]");
        }
        std::unique_ptr<void, decltype(&clang_disposeIndex)> index(clang_createIndex(0, 0), clang_disposeIndex);
        std::vector<const char*> flags(argv + 4, argv + argc);
        std::unique_ptr<CXTranslationUnitImpl, decltype(&clang_disposeTranslationUnit)> unit(
            clang_parseTranslationUnit(index.get(), argv[1], flags.data(), static_cast<int>(flags.size()),
                nullptr, 0, CXTranslationUnit_SkipFunctionBodies), clang_disposeTranslationUnit);
        if (!unit) {
            throw std::runtime_error("libclang could not parse the translation unit.");
        }
        for (unsigned i = 0; i < clang_getNumDiagnostics(unit.get()); ++i) {
            const auto diagnostic = clang_getDiagnostic(unit.get(), i);
            const auto severity = clang_getDiagnosticSeverity(diagnostic);
            const auto message = Text(clang_formatDiagnostic(diagnostic, clang_defaultDiagnosticDisplayOptions()));
            clang_disposeDiagnostic(diagnostic);
            if (severity >= CXDiagnostic_Error) {
                throw std::runtime_error(message);
            }
        }
        Catalog catalog;
        const auto recordRoot = fs::absolute(argv[2]).lexically_normal();
        catalog.Read(clang_getTranslationUnitCursor(unit.get()), recordRoot);
        const auto umbrella = fs::absolute(argv[1]).lexically_normal().lexically_relative(recordRoot.parent_path());
        Emit(catalog, argv[3], umbrella);
        std::cout << "Generated " << catalog.methods.size() << " methods and " << catalog.events.Size()
                  << " event sources; skipped " << catalog.skipped.size() << " method declarations.\n";
        return 0;
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
