#include "event_catalog.h"
#include "clang_helpers.h"
#include "managed_name.h"

#include <algorithm>
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

std::optional<EventField> ReadField(CXCursor field, std::size_t index, bool inputSnapshot)
{
    auto name = Text(clang_getCursorSpelling(field));
    if (name.empty() || clang_getCXXAccessSpecifier(field) != CX_CXXPublic) {
        return std::nullopt;
    }
    auto value = "event." + name;
    const auto slot = "fields[" + std::to_string(index) + "]";
    name = ManagedIdentifier(name);
    auto type = clang_getCanonicalType(clang_getCursorType(field));
    if (inputSnapshot && type.kind == CXType_Record && QualifiedName(clang_getTypeDeclaration(type)) == "REX::TEnumSet") {
        type = clang_getCanonicalType(clang_Type_getTemplateArgumentAsType(type, 0));
        value += ".get()";
    }
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
        if (inputSnapshot) {
            return std::nullopt;
        }
        managed = "nint";
        break;
    case CXType_Record:
        if (inputSnapshot || QualifiedName(clang_getTypeDeclaration(type)) != "RE::NiPointer") {
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

void ReadFields(CXCursor definition, EventDeclaration& event)
{
    for (const auto member : Children(definition)) {
        const auto kind = clang_getCursorKind(member);
        if (kind == CXCursor_CXXBaseSpecifier) {
            const auto baseType = clang_getCanonicalType(clang_getCursorType(member));
            if (event.isInputCallback && clang_getCXXAccessSpecifier(member) == CX_CXXPublic) {
                ReadFields(clang_getCursorDefinition(clang_getTypeDeclaration(baseType)), event);
            } else {
                event.omitted.push_back("base " + Text(clang_getTypeSpelling(baseType)));
            }
        } else if (kind == CXCursor_FieldDecl) {
            if (auto field = ReadField(member, event.fields.size(), event.isInputCallback)) {
                event.fields.push_back(std::move(*field));
            } else {
                event.omitted.push_back(Text(clang_getCursorSpelling(member)));
            }
        }
    }
}

void EmitInputHandler(std::ostream& header, std::ostream& native, const std::vector<const EventDeclaration*>& inputs)
{
    native << "\nvoid RegisterCommonLibInputHandler()\n{\n";
    if (inputs.empty()) {
        native << "}\n";
        return;
    }
    header << R"(
class CommonLibInputHandler final : public RE::BSInputEventUser {
public:
    bool ShouldHandleEvent(const RE::InputEvent* event) override
    {
        if (!event) {
            return false;
        }
        switch (event->eventType.get()) {
)";
    for (const auto* input : inputs) {
        header << "        case " << input->nativeType << "::TYPE:\n";
    }
    header << "            return true;\n        default:\n            return false;\n        }\n    }\n";
    for (const auto* input : inputs) {
        header << "\n    void " << input->sourceMethod << "(const " << input->nativeType << "* event) override\n    {\n"
               << "        if (event) {\n            PublishCommonLibEvent(*event, nullptr);\n        }\n    }\n";
    }
    header << "};\n";
    native << R"(    auto* controls = RE::MenuControls::GetSingleton();
    if (!controls) {
        return;
    }
    static CommonLibInputHandler handler;
    for (const auto* existing : controls->handlers) {
        if (existing == &handler) {
            return;
        }
    }
    controls->RegisterHandler(&handler);
}
)";
}

}

void EventCatalog::ReadScope(CXCursor scope, const std::filesystem::path& includeRoot)
{
    const bool inputUser = Text(clang_getCursorSpelling(scope)) == "BSInputEventUser"
        && QualifiedName(scope) == "RE::BSInputEventUser";
    for (const auto child : Children(scope)) {
        const auto kind = clang_getCursorKind(child);
        const auto access = clang_getCXXAccessSpecifier(child);
        if (access == CX_CXXPrivate || access == CX_CXXProtected) {
            continue;
        }
        if (inputUser && kind == CXCursor_CXXMethod && clang_CXXMethod_isVirtual(child)) {
            ReadInputCallback(child);
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
    AddEvent(clang_Type_getTemplateArgumentAsType(sourceType, 0), QualifiedName(getter), false);
}

void EventCatalog::ReadInputCallback(CXCursor callback)
{
    const auto name = Text(clang_getCursorSpelling(callback));
    if (!name.starts_with("On") || !name.ends_with("Event") || clang_Cursor_getNumArguments(callback) != 1
        || clang_getCursorResultType(callback).kind != CXType_Void) {
        return;
    }
    const auto argument = clang_getCanonicalType(clang_getCursorType(clang_Cursor_getArgument(callback, 0)));
    if (argument.kind != CXType_Pointer) {
        return;
    }
    const auto payload = clang_getPointeeType(argument);
    if (!clang_isConstQualifiedType(payload)) {
        return;
    }
    const auto definition = clang_getCursorDefinition(clang_getTypeDeclaration(payload));
    if (clang_Cursor_isNull(definition)) {
        skippedInputCallbacks_[name] = "Forward-declared input payload";
        return;
    }
    const auto members = Children(definition);
    const bool hasTypeTag = std::ranges::any_of(members, [](CXCursor member) {
        return clang_getCursorKind(member) == CXCursor_VarDecl && clang_getCXXAccessSpecifier(member) == CX_CXXPublic
            && Text(clang_getCursorSpelling(member)) == "TYPE";
    });
    if (!hasTypeTag) {
        skippedInputCallbacks_[name] = "Missing public input event TYPE";
        return;
    }
    AddEvent(payload, name, true);
}

void EventCatalog::AddEvent(CXType payloadType, std::string sourceMethod, bool isInputCallback)
{
    const auto payload = clang_getCanonicalType(payloadType);
    const auto definition = clang_getCursorDefinition(clang_getTypeDeclaration(payload));
    const auto nativeType = QualifiedName(definition);
    if (!nativeType.starts_with("RE::")) {
        return;
    }
    const auto route = nativeType.substr(4);
    const auto managedName = ManagedIdentifier(route);
    EventDeclaration event{nativeType, managedName, std::move(sourceMethod), isInputCallback, {}, {}};
    ReadFields(definition, event);
    const auto [found, inserted] = events_.emplace(route, event);
    if (!inserted && found->second.sourceMethod != event.sourceMethod) {
        throw std::runtime_error("Multiple event sources for " + nativeType);
    }
}

void EventCatalog::Emit(std::ostream& header, std::ostream& native, std::ostream& managed, std::ostream& docs) const
{
    header << "\nnamespace f4forge {\n";
    managed << "\n/// <summary>Generated CommonLibF4 event payloads. Native addresses are borrowed.</summary>\n"
               "public static class Events\n{\n";
    docs << "\n## CommonLibF4 events\n\n" << events_.size()
         << " event sources. Subscribe with `Bridge.On<Events.T>(Events.T.Name, handler)`. "
            "Handlers run on the notifying thread. Sender and nint fields are borrowed engine addresses.\n\n"
            "| Event name | Payload type | Copied fields | Omitted members |\n|---|---|---|---|\n";
    for (const auto& [route, event] : events_) {
        header << "\ntemplate <>\nstruct EventBinding<" << event.nativeType << "> {\n"
               << "    static constexpr auto Name = \"" << route << "\";\n"
               << "    static std::array<std::uint64_t, " << event.fields.size() << "> Snapshot("
               << "[[maybe_unused]] const " << event.nativeType << "& event)\n    {\n        return {\n";
        managed << "    /// <summary>Snapshot of " << event.nativeType << ".</summary>\n"
                << "    public readonly record struct " << event.managedName << '(';
        docs << "| `" << route << "` | `Events." << event.managedName << "` | ";
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
    std::vector<const EventDeclaration*> inputs;
    native << "\nnamespace f4forge {\nvoid RegisterCommonLibEventSinks()\n{\n";
    for (const auto& [route, event] : events_) {
        const auto payload = "Events." + event.managedName;
        managed << "            case " << payload << ".Name when fields.Length == " << event.fields.size() << ":\n"
                << "                Publish(name, sender, new " << payload << '(';
        for (std::size_t index = 0; index < event.fields.size(); ++index) {
            if (index > 0) {
                managed << ", ";
            }
            managed << event.fields[index].unpack;
        }
        managed << "));\n                break;\n";
        if (event.isInputCallback) {
            inputs.push_back(&event);
            continue;
        }
        native << "    if (auto* source = " << event.sourceMethod << "()) {\n"
               << "        source->RegisterSink(&CommonLibEventSink<" << event.nativeType << ">::Instance());\n    }\n";
    }
    native << "}\n";
    EmitInputHandler(header, native, inputs);
    header << "}\n";
    native << "}\n";
    if (!skippedInputCallbacks_.empty()) {
        docs << "\n### Skipped input callbacks\n\n";
        for (const auto& [name, reason] : skippedInputCallbacks_) {
            docs << "- `" << name << "` — " << reason << "\n";
        }
    }
    managed << "            default:\n                throw new System.ArgumentException($\"Unknown CommonLib event or invalid field count: '{name}'.\");\n"
               "        }\n    }\n}\n";
}
