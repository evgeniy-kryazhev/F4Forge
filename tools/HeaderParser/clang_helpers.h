#pragma once

#include <clang-c/Index.h>
#include <string>
#include <vector>

inline std::string Text(CXString value)
{
    const auto* text = clang_getCString(value);
    std::string result = text ? text : "";
    clang_disposeString(value);
    return result;
}

inline std::vector<CXCursor> Children(CXCursor parent)
{
    std::vector<CXCursor> children;
    clang_visitChildren(parent, [](CXCursor child, CXCursor, CXClientData data) {
        static_cast<std::vector<CXCursor>*>(data)->push_back(child);
        return CXChildVisit_Continue;
    }, &children);
    return children;
}
