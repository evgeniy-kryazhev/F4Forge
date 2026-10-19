#include "channel.h"
#include "components.h"
#include "../game_events/runtime.h"
#include <F4SE/F4SE.h>
#include <RE/M/Main.h>

namespace f4forge::threading {
namespace {

std::vector<std::uint64_t> registrations;
Channel* activeChannel = nullptr;
std::atomic<std::uint64_t> generation = 0;

const ComponentRegistration component({"threading",
    [](Channel& channel, const std::filesystem::path&) {
        activeChannel = &channel;
        ++generation;
        registrations.reserve(2);
        registrations.push_back(channel.Add("threading.is_game_thread", [](const DataValue&) {
            const auto* main = RE::Main::GetSingleton();
            return main != nullptr && main->threadID != 0 && main->threadID == REX::W32::GetCurrentThreadId();
        }));
        registrations.push_back(channel.Add("threading.enqueue", [](const DataValue& arguments) {
            const auto id = arguments.at("taskId").get<std::string>();
            if (id.empty() || id.size() > 128 || id.find('\0') != std::string::npos) {
                throw std::invalid_argument("A valid game-thread task identifier is required.");
            }
            const auto* tasks = F4SE::GetTaskInterface();
            if (tasks == nullptr) {
                throw std::runtime_error("F4SE task interface is unavailable.");
            }
            const auto queuedGeneration = generation.load();
            tasks->AddTask([id, queuedGeneration] {
                game_events::Host().Invoke([&] {
                    if (generation.load() == queuedGeneration) {
                        game_events::Host().Publish("threading.execute", {{"taskId", id}});
                    }
                });
            });
            return true;
        }));
    },
    [] {
        ++generation;
        if (activeChannel != nullptr) {
            for (const auto registration : registrations) {
                activeChannel->Remove(registration);
            }
        }
        registrations.clear();
        activeChannel = nullptr;
    }});

}
}
