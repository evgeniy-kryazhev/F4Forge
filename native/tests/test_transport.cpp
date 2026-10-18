#include "channel.h"
#include <memory>

namespace {
std::unique_ptr<f4forge::Channel> channel;
}

extern "C" __declspec(dllexport) f4forge::ChannelBinding TestCreate()
{
    channel = std::make_unique<f4forge::Channel>();
    channel->Add("native.echo", [](const auto& arguments) {
        return arguments;
    });
    return channel->Binding();
}

extern "C" __declspec(dllexport) void TestDestroy()
{
    channel.reset();
}
