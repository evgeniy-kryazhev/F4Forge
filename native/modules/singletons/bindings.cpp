#include "bindings.h"

namespace f4forge::singletons {

Bindings::Bindings(Channel& channel, std::function<bool()> isGameThread) :
    channel_(channel), isGameThread_(std::move(isGameThread))
{
    if (!isGameThread_) {
        throw std::invalid_argument("A singleton game-thread predicate is required.");
    }
}

Bindings::~Bindings()
{
    for (const auto registration : registrations_) {
        channel_.Remove(registration);
    }
}

void Bindings::Add(std::string name, Channel::Handler handler)
{
    if (!handler) {
        throw std::invalid_argument("A singleton handler is required.");
    }
    const auto registration = channel_.Add(std::move(name),
        [isGameThread = isGameThread_, handler = std::move(handler)](const DataValue& arguments) {
            if (!isGameThread()) {
                throw std::runtime_error("Singleton operations require the game thread. Use GameThread.RunAsync.");
            }
            return handler(arguments);
        });
    try {
        registrations_.push_back(registration);
    } catch (...) {
        channel_.Remove(registration);
        throw;
    }
}

}
