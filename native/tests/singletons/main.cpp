#include "bindings.h"
#include "generated.inc"
#include <iostream>

namespace {

void Require(bool condition, const char* message)
{
    if (!condition) {
        throw std::runtime_error(message);
    }
}

class TaggedSerializer final : public f4forge::IDataSerializer {
public:
    std::string_view Format() const noexcept override { return "test.binary"; }

    std::string Serialize(const f4forge::DataValue& value) const override
    {
        return std::string("\0\xFF", 2) + json_->Serialize(value);
    }

    f4forge::DataValue Deserialize(std::string_view bytes) const override
    {
        if (bytes.size() < 2 || bytes[0] != '\0' || static_cast<unsigned char>(bytes[1]) != 255) {
            throw std::invalid_argument("Missing binary prefix.");
        }
        return json_->Deserialize(bytes.substr(2));
    }

private:
    std::shared_ptr<const f4forge::IDataSerializer> json_ = f4forge::CreateDataSerializer();
};

f4forge::DataValue Call(f4forge::Channel& channel, std::string_view method,
    const f4forge::DataValue& arguments = nullptr)
{
    const auto& serializer = channel.Serializer();
    const auto request = serializer.Serialize({{"method", method}, {"arguments", arguments}});
    return serializer.Deserialize(channel.Call(request));
}

void ExerciseGeneratedBindings(bool binary)
{
    auto serializer = binary ? std::shared_ptr<const f4forge::IDataSerializer>(std::make_shared<TaggedSerializer>())
                             : f4forge::CreateDataSerializer();
    f4forge::Channel channel(std::move(serializer));
    bool gameThread = true;
    constexpr auto available = "singletons.RE::FixtureSingleton.available";
    constexpr auto count = "singletons.RE::FixtureSingleton.count.get";
    constexpr auto echo = "singletons.RE::FixtureSingleton.Echo.12a5562d6692";
    constexpr auto integerOverload = "singletons.RE::FixtureSingleton.Overload.5b8a52c7c03e";
    constexpr auto boolOverload = "singletons.RE::FixtureSingleton.Overload.e9b95bb7112b";
    {
        f4forge::singletons::Bindings bindings(channel, [&] { return gameThread; });
        f4forge::singletons::RegisterGenerated(bindings);
        Require(Call(channel, available).at("value") == false, "Missing singleton must report unavailable.");
        Require(Call(channel, count).at("success") == false, "Missing singleton must fail safely.");

        RE::FixtureSingleton first;
        RE::FixtureSingleton second;
        second.count = 99;
        RE::FixtureSingleton::current = &first;
        Require(Call(channel, available).at("value") == true, "Existing singleton must report available.");
        Require(Call(channel, count).at("value") == 7, "Field must be read through generated binding.");
        RE::FixtureSingleton::current = &second;
        Require(Call(channel, count).at("value") == 99, "Bindings must resolve the current singleton on every call.");
        Require(Call(channel, "singletons.RE::FixtureSingleton.flags.get").at("value") == 5, "Bitfields must be copied.");
        Require(Call(channel, "singletons.RE::FixtureReferenceSingleton.value.get").at("value") == 42,
            "Reference singletons must be supported.");
        Require(Call(channel, integerOverload, {{"value", 41}}).at("value") == 42, "Integer overload must dispatch.");
        Require(Call(channel, boolOverload, {{"value", false}}).at("value") == true, "Boolean overload must dispatch.");
        Require(Call(channel, "singletons.RE::FixtureSingleton.CharacterOverload.c1734087e4dc", {{"argument0", 1}}).at("value") == 5,
            "Character overload must use the exact native type rather than integer promotion.");
        constexpr auto echoMode = "singletons.RE::FixtureSingleton.EchoMode.3c5678e27367";
        Require(Call(channel, echoMode, {{"value", 0xFFFFFFFFU}}).at("value") == 0xFFFFFFFFU,
            "Unsigned enum values must preserve their full range.");
        Require(Call(channel, echoMode, {{"value", -1}}).at("success") == false,
            "Negative unsigned enum values must be rejected.");
        bindings.Add("singletons.nested", [&](const f4forge::DataValue&) {
            return Call(channel, count).at("value");
        });
        Require(Call(channel, "singletons.nested").at("value") == 99, "Nested calls must use the shared recursive lane.");
        Require(Call(channel, "singletons.RE::FixtureSingleton.SetEnabled.6bc8e0f1418d", {{"value", true}}).at("success"),
            "Void method must complete successfully.");
        Require(second.enabled, "Void method must mutate the current native singleton.");

        const auto unicode = std::string("Меню — 🎮");
        Require(Call(channel, echo, {{"value", unicode}}).at("value") == unicode, "Unicode must round-trip.");
        Require(Call(channel, echo, {{"value", std::string("a\0b", 3)}}).at("success") == false,
            "Embedded NUL must be rejected.");
        Require(Call(channel, integerOverload, {{"value", 0xFFFFFFFFFFFFFFFFULL}}).at("success") == false,
            "Integer overflow must be rejected.");
        Require(Call(channel, boolOverload, {{"value", 1}}).at("success") == false, "Boolean coercion must be rejected.");
        Require(Call(channel, integerOverload, {{"value", 1.25}}).at("success") == false, "Float truncation must be rejected.");
        gameThread = false;
        const auto failure = Call(channel, count);
        Require(failure.at("error").at("message").get<std::string>().find("game thread") != std::string::npos,
            "Off-thread access must fail before touching engine state.");
        gameThread = true;
        RE::FixtureSingleton::current = nullptr;
    }
    Require(Call(channel, available).at("error").at("code") == "method_not_found", "Stopping must remove registrations.");
    {
        f4forge::singletons::Bindings restarted(channel, [] { return true; });
        f4forge::singletons::RegisterGenerated(restarted);
        Require(Call(channel, available).at("value") == false, "Bindings must support restart.");
    }

    try {
        f4forge::singletons::Bindings partial(channel, [] { return true; });
        partial.Add("singletons.rollback", [](const f4forge::DataValue&) { return true; });
        partial.Add("singletons.rollback", [](const f4forge::DataValue&) { return false; });
        throw std::runtime_error("Duplicate registration should have failed.");
    } catch (const std::invalid_argument&) {
        Require(Call(channel, "singletons.rollback").at("error").at("code") == "method_not_found",
            "Startup failure must roll back prior registrations.");
    }
}

}

int main()
{
    try {
        ExerciseGeneratedBindings(false);
        ExerciseGeneratedBindings(true);
        std::cout << "Generated singleton binding tests passed.\n";
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
