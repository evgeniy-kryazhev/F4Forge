#include "store.h"
#include <Windows.h>
#include <limits>
#include <stdexcept>

namespace f4forge::diagnostics {

bool MatchPattern(std::span<const std::uint8_t> bytes, std::span<const std::optional<std::uint8_t>> pattern)
{
    if (bytes.size() < pattern.size() || pattern.empty()) {
        return false;
    }
    for (std::size_t index = 0; index < pattern.size(); ++index) {
        if (pattern[index] && bytes[index] != *pattern[index]) {
            return false;
        }
    }
    return true;
}

void CheckReadable(std::uintptr_t address, std::size_t size)
{
    if (address == 0 || size == 0 || size > (std::numeric_limits<std::uintptr_t>::max)() - address) {
        throw std::runtime_error("Invalid memory range for a game hook.");
    }
    const auto end = address + size;
    while (address < end) {
        MEMORY_BASIC_INFORMATION memory{};
        if (VirtualQuery(reinterpret_cast<const void*>(address), &memory, sizeof(memory)) != sizeof(memory)
            || memory.State != MEM_COMMIT || (memory.Protect & (PAGE_GUARD | PAGE_NOACCESS)) != 0) {
            throw std::runtime_error("Game hook memory is not committed and accessible.");
        }
        const auto protection = memory.Protect & 0xff;
        if (protection != PAGE_READONLY && protection != PAGE_READWRITE && protection != PAGE_WRITECOPY
            && protection != PAGE_EXECUTE_READ && protection != PAGE_EXECUTE_READWRITE && protection != PAGE_EXECUTE_WRITECOPY) {
            throw std::runtime_error("Game hook memory is not readable.");
        }
        const auto regionEnd = reinterpret_cast<std::uintptr_t>(memory.BaseAddress) + memory.RegionSize;
        if (regionEnd <= address) {
            throw std::runtime_error("Invalid game hook memory region.");
        }
        address = regionEnd;
    }
}

void CheckCode(std::uintptr_t address)
{
    CheckReadable(address, 1);
    MEMORY_BASIC_INFORMATION memory{};
    if (VirtualQuery(reinterpret_cast<const void*>(address), &memory, sizeof(memory)) != sizeof(memory)) {
        throw std::runtime_error("Cannot inspect game hook target.");
    }
    const auto protection = memory.Protect & 0xff;
    if (protection != PAGE_EXECUTE_READ && protection != PAGE_EXECUTE_READWRITE && protection != PAGE_EXECUTE_WRITECOPY) {
        throw std::runtime_error("Game hook target is not executable.");
    }
}

std::uintptr_t CheckVtableSlot(std::uintptr_t table, std::size_t index)
{
    if (table % alignof(std::uintptr_t) != 0 || index > 4096) {
        throw std::runtime_error("Invalid game hook vtable slot.");
    }
    const auto offset = index * sizeof(std::uintptr_t);
    if (offset > (std::numeric_limits<std::uintptr_t>::max)() - table) {
        throw std::runtime_error("Game hook vtable address overflow.");
    }
    CheckReadable(table + offset, sizeof(std::uintptr_t));
    const auto target = reinterpret_cast<const std::uintptr_t*>(table)[index];
    CheckCode(target);
    return target;
}

}
