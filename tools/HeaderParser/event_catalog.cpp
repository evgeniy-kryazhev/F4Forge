#include "event_catalog.h"
#include "clang_helpers.h"

#include <cctype>
#include <optional>
#include <stdexcept>

namespace {

std::string QualifiedName(CXCursor declaration)
{
    std::string name;
    while (!clang_Cursor_isNull(declaration) && clang_getCursorKind(declaration) != CXCursor_TranslationUnit) {
        const auto part = Text(clang_getCursorSpelling(declaration));
        if (part.empty()) {
            return {};
        }
        name = name.empty() ? part : part + "::" + name;
        declaration = clang_getCursorSemanticParent(declaration);
    }
    return name;
}

std::optional<EventField> ReadField(CXCursor field, std::size_t index)
{
    auto name = Text(clang_getCursorSpelling(field));
    if (name.empty() || clang_getCXXAccessSpecifier(field) != CX_CXXPublic) {
        return std::nullopt;
    }
    auto value = "event." + name;
    const auto slot = "fields[" + std::to_string(index) + "]";
    name.front() = static_cast<char>(std::toupper(static_cast<unsigned char>(name.front())));
    auto type = clang_getCanonicalType(clang_getCursorType(field));
    if (type.kind == CXType_Enum) {
        type = clang_getCanonicalType(clang_getEnumDeclIntegerType(clang_getTypeDeclaration(type)));
    }
    std::string managed;
    switch (type.kind) {
    case CXType_Bool:
        return EventField{name, "bool", "static_cast<std::uint64_t>(" + value + ")", slot + " != 0"};
    case CXType_SChar: managed = "sbyte"; break;
    case CXType_UChar: managed = "byte"; break;
    case CXType_Short: managed = "short"; break;
    case CXType_UShort: managed = "ushort"; break;
    case CXType_Int:
    case CXType_Long: managed = "int"; break;
    case CXType_UInt:
    case CXType_ULong: managed = "uint"; break;
    case CXType_LongLong: managed = "long"; break;
    case CXType_ULongLong: managed = "ulong"; break;
    case CXType_Float:
        return EventField{name, "float", "std::bit_cast<std::uint32_t>(" + value + ")",
            "System.BitConverter.UInt32BitsToSingle((uint)" + slot + ")"};
    case CXType_Pointer:
        managed = "nint";
        break;
    case CXType_Record:
        if (QualifiedName(clang_getTypeDeclaration(type)) != "RE::NiPointer") {
            return std::nullopt;
        }
        managed = "nint";
        value += ".get()";
        break;
    default:
        return std::nullopt;
    }
    const auto cast = managed == "nint" ? "reinterpret_cast" : "static_cast";
    return EventField{name, managed, std::string(cast) + "<std::uint64_t>(" + value + ")",
        "unchecked((" + managed + ")" + slot + ")"};
}

}

void EventCatalog::ReadScope(CXCursor scope, const std::filesystem::path& includeRoot)
{
    for (const auto child : Children(scope)) {
        const auto kind = clang_getCursorKind(child);
        const auto access = clang_getCXXAccessSpecifier(child);
        if (access == CX_CXXPrivate || access == CX_CXXProtected) {
            continue;
        }
        if (kind == CXCursor_Namespace || kind == CXCursor_StructDecl || kind == CXCursor_ClassDecl) {
            if (clang_isCursorDefinition(child) && !Text(clang_getCursorSpelling(child)).empty()
                && clang_Cursor_isNull(clang_getSpecializedCursorTemplate(child))) {
                ReadScope(child, includeRoot);
            }
            continue;
        }
        if (kind != CXCursor_FunctionDecl && (kind != CXCursor_CXXMethod || !clang_CXXMethod_isStatic(child))) {
            continue;
        }
        if (Text(clang_getCursorSpelling(child)) != "GetEventSource" || clang_Cursor_getNumArguments(child) != 0) {
            continue;
        }
        CXFile file{};
        clang_getSpellingLocation(clang_getCursorLocation(child), &file, nullptr, nullptr, nullptr);
        const auto path = std::filesystem::absolute(Text(clang_getFileName(file))).lexically_normal();
        const auto relative = path.lexically_relative(includeRoot);
        if (!relative.empty() && *relative.begin() != "..") {
            ReadSource(child);
        }
    }
}

void EventCatalog::ReadSource(CXCursor getter)
{
    const auto result = clang_getCanonicalType(clang_getCursorResultType(getter));
    if (result.kind != CXType_Pointer) {
        return;
    }
    const auto sourceType = clang_getCanonicalType(clang_getPointeeType(result));
    const auto source = QualifiedName(clang_getTypeDeclaration(sourceType));
    if (source != "RE::BSTEventSource" && source != "RE::BSTGlobalEvent::EventSource") {
        return;
    }
    const auto payload = clang_getCanonicalType(clang_Type_getTemplateArgumentAsType(sourceType, 0));
    const auto definition = clang_getCursorDefinition(clang_getTypeDeclaration(payload));
    const auto nativeType = QualifiedName(definition);
    if (!nativeType.starts_with("RE::")) {
        return;
    }
    const auto route = nativeType.substr(4);
    auto managedName = route;
    for (auto pos = managedName.find("::"); pos != std::string::npos; pos = managedName.find("::")) {
        managedName.replace(pos, 2, "_");
    }
    EventDeclaration event{nativeType, managedName, QualifiedName(getter), {}, {}};
    for (const auto member : Children(definition)) {
        const auto kind = clang_getCursorKind(member);
        if (kind == CXCursor_CXXBaseSpecifier) {
            event.omitted.push_back("base " + Text(clang_getTypeSpelling(clang_getCursorType(member))));
        }
        if (kind != CXCursor_FieldDecl) {
            continue;
        }
        if (auto field = ReadField(member, event.fields.size())) {
            event.fields.push_back(std::move(*field));
        } else {
            event.omitted.push_back(Text(clang_getCursorSpelling(member)));
        }
    }
    const auto [found, inserted] = events_.emplace(route, event);
    if (!inserted && found->second.getter != event.getter) {
        throw std::runtime_error("Multiple event sources for " + nativeType);
    }
}

void EventCatalog::Emit(std::ostream& header, std::ostream& native, std::ostream& managed, std::ostream& docs) const
{
    header << "\nnamespace f4forge {\n";
    managed << "\n/// <summary>Generated CommonLibF4 event payloads. Native addresses are borrowed.</summary>\n"
               "public static class AutoEvents\n{\n";
    docs << "\n## CommonLibF4 events\n\n" << events_.size()
         << " event sources. Subscribe with `Bridge.On<AutoEvents.T>(AutoEvents.T.Name, handler)`. "
            "Handlers run on the notifying thread. Sender and nint fields are borrowed engine addresses.\n\n"
            "| Event name | Payload type | Copied fields | Omitted members |\n|---|---|---|---|\n";
    for (const auto& [route, event] : events_) {
        header << "\ntemplate <>\nstruct EventBinding<" << event.nativeType << "> {\n"
               << "    static constexpr auto Name = \"" << route << "\";\n"
               << "    static std::array<std::uint64_t, " << event.fields.size() << "> Snapshot("
               << "[[maybe_unused]] const " << event.nativeType << "& event)\n    {\n        return {\n";
        managed << "    /// <summary>Snapshot of " << event.nativeType << ".</summary>\n"
                << "    public readonly record struct " << event.managedName << '(';
        docs << "| `" << route << "` | `AutoEvents." << event.managedName << "` | ";
        for (std::size_t index = 0; index < event.fields.size(); ++index) {
            const auto& field = event.fields[index];
            if (index > 0) {
                managed << ", ";
                docs << ", ";
            }
            managed << field.managedType << ' ' << field.name;
            docs << '`' << field.managedType << ' ' << field.name << '`';
            header << "            " << field.pack << ",\n";
        }
        docs << " | ";
        for (const auto& omitted : event.omitted) {
            docs << '`' << omitted << "` ";
        }
        docs << " |\n";
        managed << ")\n    {\n        /// <summary>Event name for Bridge.On.</summary>\n"
                << "        public const string Name = \"" << route << "\";\n    }\n\n";
        header << "        };\n    }\n};\n";
    }
    managed << "}\n\npublic static partial class Bridge\n{\n"
               "    private static void PublishCommonLibEvent(string name, object? sender, System.ReadOnlySpan<ulong> fields)\n"
               "    {\n        switch (name)\n        {\n";
    header << "}\n";
    native << "\nnamespace f4forge {\nvoid RegisterCommonLibEventSinks()\n{\n";
    for (const auto& [route, event] : events_) {
        const auto payload = "AutoEvents." + event.managedName;
        managed << "            case " << payload << ".Name when fields.Length == " << event.fields.size() << ":\n"
                << "                Publish(name, sender, new " << payload << '(';
        for (std::size_t index = 0; index < event.fields.size(); ++index) {
            if (index > 0) {
                managed << ", ";
            }
            managed << event.fields[index].unpack;
        }
        managed << "));\n                break;\n";
        native << "    if (auto* source = " << event.getter << "()) {\n"
               << "        source->RegisterSink(&CommonLibEventSink<" << event.nativeType << ">::Instance());\n    }\n";
    }
    native << "}\n}\n";
    managed << "            default:\n                throw new System.ArgumentException($\"Unknown CommonLib event or invalid field count: '{name}'.\");\n"
               "        }\n    }\n}\n";
}
