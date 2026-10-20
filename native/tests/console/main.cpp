#include "../../modules/console/router.h"
#include <iostream>

namespace {

using f4forge::DataValue;
using f4forge::console::Router;

void Require(bool condition, const char* message)
{
    if (!condition) {
        throw std::runtime_error(message);
    }
}

class TaggedSerializer final : public f4forge::IDataSerializer {
public:
    std::string_view Format() const noexcept override { return "console.test"; }

    std::string Serialize(const DataValue& value) const override
    {
        ++writes;
        return std::string("\0\xff", 2) + json_->Serialize(value);
    }

    DataValue Deserialize(std::string_view bytes) const override
    {
        ++reads;
        Require(bytes.starts_with(std::string_view("\0\xff", 2)), "Serializer was bypassed");
        return json_->Deserialize(bytes.substr(2));
    }

    mutable int reads = 0;
    mutable int writes = 0;

private:
    std::shared_ptr<const f4forge::IDataSerializer> json_ = f4forge::CreateDataSerializer();
};

void Parsing()
{
    using f4forge::console::ParseCommand;
    const auto command = ParseCommand("  HeLLo  \"World Ω 🌍\" \"\" plain C:\\mods \"quoted \\\"word\\\"\" \"C:\\mods\"  ");
    Require(command.alias == "hello", "Alias was not normalized");
    Require(command.arguments == std::vector<std::string>{"World Ω 🌍", "", "plain", "C:\\mods", "quoted \"word\"", "C:\\mods"}, "Quoted arguments were parsed incorrectly");
    Require(command.rawCommand.starts_with("  HeLLo  "), "Raw command was changed");
    Require(command.rawArguments.starts_with("\"World Ω 🌍\""), "Raw arguments were changed");
    Require(ParseCommand("hello").arguments.empty(), "No-argument command gained arguments");
    Require(ParseCommand("hello a\tb\r\nc").arguments.size() == 3, "Whitespace parsing failed");
    Require(!ParseCommand("hello \"broken").parseError.empty(), "Unclosed quotes were accepted");
    for (const auto* text : {"", " ", "1hello", "a;b", "hello.world()"}) {
        Require(ParseCommand(text).alias.empty(), "Invalid alias was accepted");
    }
    Require(f4forge::console::NormalizeAlias("_Mod.Name-42") == "_mod.name-42", "Valid alias was rejected");
    Require(ParseCommand(std::string(65, 'a')).alias.empty(), "Oversized alias was accepted");
}

void Routing(const std::shared_ptr<const f4forge::IDataSerializer>& serializer)
{
    f4forge::Channel channel(serializer);
    std::vector<std::string> output;
    const auto call = [&](std::string method, const DataValue& arguments) {
        return serializer->Deserialize(channel.Call(serializer->Serialize({{"method", method}, {"arguments", arguments}})));
    };
    {
        Router router(channel, [&](std::string_view line) { output.emplace_back(line); }, [](std::string_view alias) {
            return alias == "help" || alias == "tgm";
        });
        Require(call("console.validate_alias", {{"alias", "Hello"}}).at("value") == true, "Alias validation failed");
        Require(call("console.validate_alias", {{"alias", "HELP"}}).at("success") == false, "Reserved alias was allowed");
        Require(call("console.validate_alias", {{"alias", "a;b"}}).at("success") == false, "Invalid alias was allowed");
        Require(!router.Execute("vanilla \"broken", 0), "Unknown malformed command was swallowed");
        Require(!router.Execute("player.additem f 1", 0), "Vanilla command was swallowed");
        int calls = 0;
        const auto registration = channel.Add("console.command.hello", [&](const DataValue& arguments) {
            ++calls;
            Require(arguments.at("alias") == "hello", "Wrong alias in payload");
            Require(arguments.at("selectedReferenceFormId") == 0x14, "Selected reference was lost");
            if (!arguments.at("parseError").get<std::string>().empty()) {
                throw std::runtime_error(arguments.at("parseError").get<std::string>());
            }
            Require(arguments.at("arguments") == DataValue::array({"World Ω", ""}), "Arguments lost in transit");
            return DataValue{{"lines", DataValue::array({"Hello Ω {name} %s!", "second"})}};
        });
        Require(router.Execute("HELLO \"World Ω\" \"\"", 0x14), "Known command was not consumed");
        Require(calls == 1 && output == std::vector<std::string>{"Hello Ω {name} %s!", "second"}, "Output changed or callback repeated");
        Require(router.Execute("hello \"broken", 0x14), "Known malformed command was forwarded to engine");
        Require(output.back().find("Unclosed") != std::string::npos, "Parse error was not displayed");
        channel.Remove(registration);
        Require(!router.Execute("hello", 0), "Removed registration still handled input");
        channel.Add("console.command.help", [](const DataValue&) { return DataValue{{"lines", {"bad"}}}; });
        Require(!router.Execute("help", 0), "Built-in command was shadowed through raw channel");
        channel.Add("console.command.broken", [](const DataValue&) -> DataValue {
            throw std::runtime_error("Expected failure");
        });
        Require(router.Execute("broken", 0), "Handler failure was forwarded to engine");
        Require(output.back() == "F4Forge: Expected failure", "Handler failure was not displayed");
        channel.Add("console.command.nullref", [](const DataValue& arguments) {
            Require(arguments.at("selectedReferenceFormId") == 0, "Null reference must use zero");
            return DataValue{{"lines", DataValue::array()}};
        });
        Require(router.Execute("nullref", 0), "No-output handler failed");
        channel.Add("console.command.badresult", [](const DataValue&) { return DataValue{{"lines", 42}}; });
        Require(router.Execute("badresult", 0), "Malformed SDK result escaped the router");
        Require(output.back().starts_with("F4Forge console result:"), "Malformed SDK result was not diagnosed");
    }
    Require(call("console.validate_alias", {{"alias", "hello"}}).at("error").at("code") == "method_not_found", "Router stop leaked registration");
    Router restarted(channel, [](std::string_view) {}, [](std::string_view) { return false; });
    Require(call("console.validate_alias", {{"alias", "hello"}}).at("value") == true, "Router could not restart");
}

}

int main()
{
    try {
        Parsing();
        Routing(f4forge::CreateDataSerializer());
        const auto serializer = std::make_shared<TaggedSerializer>();
        Routing(serializer);
        Require(serializer->reads > 0 && serializer->writes > 0, "Alternate serializer was not exercised");
        std::cout << "Console tests passed (JSON and binary serializer).\n";
        return 0;
    } catch (const std::exception& exception) {
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
