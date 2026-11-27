#pragma once

#include <cctype>
#include <string>
#include <string_view>

// Preserve CommonLib acronyms while converting word and scope separators to casing.
inline std::string ManagedIdentifier(std::string_view native, bool capitalizeFirst = true)
{
    std::string name;
    bool capitalize = capitalizeFirst;
    for (char letter : native) {
        if (letter == '_' || letter == ':') {
            capitalize = true;
            continue;
        }
        if (capitalize) {
            letter = static_cast<char>(std::toupper(static_cast<unsigned char>(letter)));
        }
        name.push_back(letter);
        capitalize = false;
    }
    return name;
}
