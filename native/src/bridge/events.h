#pragma once

#include "channel.h"

namespace f4forge {

// All pointers are borrowed for this synchronous call. Strings are null-terminated UTF-8.
bool PublishEvent(const char* name, const void* sender, const F4SEValue& args) noexcept;

}
