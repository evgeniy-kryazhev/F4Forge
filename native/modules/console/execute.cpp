#include "../extensions/transport.h"
#include "components.h"

namespace f4forge::console_execution
{
namespace
{
Channel* channel = nullptr;
std::uint64_t route = 0;

const ComponentRegistration component({"console.execution",
                                       [](Channel& owner, const std::filesystem::path&) {
                                           channel = &owner;
                                           route =
                                               owner.Add("console.execute", [](const DataValue& request) -> DataValue {
                                                   extensions::RequireGameThread();
                                                   const auto command = request.at("command").get<std::string>();
                                                   if (command.empty() || command.find('\0') != std::string::npos)
                                                   {
                                                       throw std::invalid_argument(
                                                           "A nonempty console command without NUL is required.");
                                                   }
                                                   RE::Console::ExecuteCommand(command.c_str());
                                                   return true;
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
} // namespace f4forge::console_execution
