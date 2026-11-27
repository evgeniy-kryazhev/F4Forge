#pragma once

#include <clang-c/Index.h>
#include <filesystem>
#include <map>
#include <ostream>
#include <string>
#include <vector>

struct EventField {
    std::string name;
    std::string managedType;
    std::string pack;
    std::string unpack;
};

struct EventDeclaration {
    std::string nativeType;
    std::string managedName;
    std::string sourceMethod;
    bool isInputCallback;
    std::vector<EventField> fields;
    std::vector<std::string> omitted;
};

class EventCatalog {
public:
    void ReadScope(CXCursor scope, const std::filesystem::path& includeRoot);
    void Emit(std::ostream& header, std::ostream& native, std::ostream& managed, std::ostream& docs) const;
    std::size_t Size() const { return events_.size(); }

private:
    void ReadSource(CXCursor getter);
    void ReadInputCallback(CXCursor callback);
    void AddEvent(CXType payload, std::string sourceMethod, bool isInputCallback);
    std::map<std::string, EventDeclaration> events_;
    std::map<std::string, std::string> skippedInputCallbacks_;
};
