#include "channel.h"
#include "../modules/interception/registry.h"
#include "../modules/console/interception.h"
#include <memory>

namespace {
std::unique_ptr<f4forge::Channel> channel;
std::unique_ptr<f4forge::interception::Registry> interceptors;
std::vector<std::string> errors;

// A deliberately non-JSON wire encoding used only to detect bypasses of IDataSerializer.
class TaggedSerializer final : public f4forge::IDataSerializer {
public:
    std::string_view Format() const noexcept override
    {
        return "test.binary";
    }

    std::string Serialize(const f4forge::DataValue& value) const override
    {
        return std::string("\0\xff", 2) + json_->Serialize(value);
    }

    f4forge::DataValue Deserialize(std::string_view bytes) const override
    {
        if (!bytes.starts_with(std::string_view("\0\xff", 2))) {
            throw std::invalid_argument("Missing binary prefix.");
        }
        return json_->Deserialize(bytes.substr(2));
    }

private:
    std::shared_ptr<const f4forge::IDataSerializer> json_ = f4forge::CreateDataSerializer();
};

f4forge::ChannelBinding Create(std::shared_ptr<const f4forge::IDataSerializer> serializer)
{
    interceptors.reset();
    channel = std::make_unique<f4forge::Channel>(std::move(serializer));
    errors.clear();
    channel->Events().SetErrorSink([](std::string_view message) {
        errors.emplace_back(message);
    });
    channel->Add("native.echo", [](const auto& arguments) {
        return arguments;
    });
    interceptors = std::make_unique<f4forge::interception::Registry>(*channel, [](std::string_view message) {
        errors.emplace_back(message);
    });
    channel->Add("test.intercept", [](const f4forge::DataValue& input) {
        return interceptors->Apply("console.command.executing", input, f4forge::console::ValidateConsoleDecision);
    });
    return channel->Binding();
}
}

extern "C" __declspec(dllexport) f4forge::ChannelBinding TestCreate()
{
    return Create(f4forge::CreateDataSerializer());
}

extern "C" __declspec(dllexport) f4forge::ChannelBinding TestCreateBinary()
{
    return Create(std::make_shared<TaggedSerializer>());
}

extern "C" __declspec(dllexport) int TestErrorCount()
{
    return static_cast<int>(errors.size());
}

extern "C" __declspec(dllexport) void TestPublish()
{
    channel->Events().Publish("native.event", {{"text", "Привет 🌍"}});
}

extern "C" __declspec(dllexport) void TestDestroy()
{
    interceptors.reset();
    channel.reset();
}
