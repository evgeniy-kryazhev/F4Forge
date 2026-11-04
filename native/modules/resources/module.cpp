#include "../extensions/transport.h"
#include "components.h"
#include <RE/B/BSResourceNiBinaryStream.h>
#include <array>

namespace f4forge::resources
{
namespace
{
Channel* channel = nullptr;
std::uint64_t route = 0;

const ComponentRegistration component(
    {"game.resources",
     [](Channel& owner, const std::filesystem::path&) {
         channel = &owner;
         route = owner.Add("resources.read", [](const DataValue& request) -> DataValue {
             extensions::RequireGameThread();
             const auto path = request.at("path").get<std::string>();
             if (path.empty() || path.front() == '/' || path.front() == '\\' || path.find("..") != std::string::npos ||
                 path.find(':') != std::string::npos || path.find('\0') != std::string::npos)
             {
                 throw std::invalid_argument("Expected a game-resource-relative path.");
             }
             RE::BSResourceNiBinaryStream stream(path.c_str());
             if (!stream)
             {
                 return nullptr;
             }
             auto result = DataValue::array();
             std::array<std::uint8_t, 4096> buffer{};
             for (;;)
             {
                 const auto count = stream.DoRead(buffer.data(), buffer.size());
                 if (count == 0)
                 {
                     break;
                 }
                 if (result.size() + count > 128 * 1024)
                 {
                     throw std::length_error("Resource exceeds the 128 KiB copied-read limit.");
                 }
                 for (std::size_t index = 0; index < count; ++index)
                 {
                     result.push_back(buffer[index]);
                 }
             }
             return result;
         });
     },
     [] {
         if (channel != nullptr)
         {
             channel->Remove(route);
             channel = nullptr;
         }
     }});
} // namespace
} // namespace f4forge::resources
