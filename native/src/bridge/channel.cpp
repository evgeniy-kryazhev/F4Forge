#include "channel.h"

#include "method_registry.h"

using f4forge::F4SEValue;
using f4forge::InvokeError;
using f4forge::ValueType;

extern "C" __declspec(dllexport) void __cdecl InvokeFFI(
    const char* name, const F4SEValue* arguments, std::int32_t count, F4SEValue* result) noexcept
{
    if (result == nullptr) {
        return;
    }
    if (name == nullptr || count < 0 || (count > 0 && arguments == nullptr)) {
        *result = {ValueType::Error, {.error = InvokeError::InvalidArguments}};
        return;
    }

    try {
        const auto handler = f4forge::GetMethodRegistry().Find(name);
        if (handler == nullptr) {
            *result = {ValueType::Error, {.error = InvokeError::UnknownOperation}};
            return;
        }
        *result = handler({arguments, static_cast<std::size_t>(count)});
    } catch (...) {
        // A registration failure must not unwind into a managed caller.
        *result = {ValueType::Error, {.error = InvokeError::RegistryUnavailable}};
    }
}
