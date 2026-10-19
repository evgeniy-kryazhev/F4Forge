#include "../../modules/papyrus/service.h"
#include <future>
#include <iostream>

namespace
{

using f4forge::DataValue;
using namespace f4forge::papyrus;

void Require(bool condition, const char* message)
{
    if (!condition)
    {
        throw std::runtime_error(message);
    }
}

class BinarySerializer final : public f4forge::IDataSerializer
{
  public:
    std::string_view Format() const noexcept override
    {
        return "test.binary";
    }
    std::string Serialize(const DataValue& value) const override
    {
        return std::string("\0\xff", 2) + json_->Serialize(value);
    }
    DataValue Deserialize(std::string_view bytes) const override
    {
        if (bytes.size() < 2 || bytes[0] != '\0' || static_cast<unsigned char>(bytes[1]) != 255)
        {
            throw std::invalid_argument("Missing binary prefix.");
        }
        return json_->Deserialize(bytes.substr(2));
    }

  private:
    std::shared_ptr<const f4forge::IDataSerializer> json_ = f4forge::CreateDataSerializer();
};

DataValue Call(f4forge::Channel& channel, std::string_view method, const DataValue& arguments)
{
    return channel.Serializer().Deserialize(
        channel.Call(channel.Serializer().Serialize({{"method", method}, {"arguments", arguments}})));
}

DataValue Request(std::string scope, std::string id)
{
    return {{"scope", std::move(scope)},
            {"requestId", std::move(id)},
            {"operation", "static"},
            {"scriptName", "PrismaUI"},
            {"name", "Show"},
            {"arguments", DataValue::array()},
            {"target", nullptr}};
}

void Check(bool binary)
{
    f4forge::Channel channel(binary ? std::make_shared<BinarySerializer>() : f4forge::CreateDataSerializer());
    std::vector<Completion> complete;
    std::vector<IsPending> pending;
    std::vector<DataValue> published;
    std::vector<std::string> released;
    int resets = 0;
    Backend backend{[&](const DataValue& request, Completion completion, IsPending active) {
                        if (request.at("name") == "throw")
                        {
                            throw std::runtime_error("Queue unavailable Ω");
                        }
                        complete.push_back(std::move(completion));
                        pending.push_back(std::move(active));
                    },
                    [&](std::string_view scope) { released.emplace_back(scope); }, [&] { ++resets; }};
    {
        Service service(channel, backend, [&](const DataValue& value) {
            published.push_back(value);
            // Reentrant removal verifies that callbacks run outside registry locks.
            Require(Call(channel, "papyrus.forget", {{"scope", "A"}, {"requestId", "missing"}}).at("success"),
                    "Reentry failed");
        });
        Require(Call(channel, "papyrus.begin", Request("A", "one")).at("success"), "Begin failed");
        Require(!Call(channel, "papyrus.begin", Request("A", "one")).at("success"), "Duplicate accepted");
        Require(pending[0](), "Request not active");
        auto worker =
            std::async(std::launch::async, [&] { complete[0]({{"kind", "string"}, {"text", "Результат 🌍"}}, ""); });
        worker.get();
        Require(published.size() == 1 && published[0].at("value").at("text") == "Результат 🌍", "Unicode result lost");
        complete[0](nullptr, "duplicate");
        Require(published.size() == 1 && !pending[0](), "Duplicate completion delivered");

        Require(Call(channel, "papyrus.begin", Request("A", "two")).at("success"), "Second begin failed");
        Call(channel, "papyrus.forget", {{"scope", "A"}, {"requestId", "two"}});
        Require(!pending[1](), "Forgotten request remains active");
        complete[1](nullptr, "late");
        Require(published.size() == 1, "Forgotten completion delivered");

        Call(channel, "papyrus.begin", Request("A", "reuse"));
        service.Reset();
        Require(!pending[2]() && resets == 1, "Reset did not cancel");
        Require(published.size() == 2 && !published.back().at("success").get<bool>(), "Reset did not report failure");
        Call(channel, "papyrus.begin", Request("A", "reuse"));
        complete[2](nullptr, "previous session");
        Require(pending[3](), "Late old completion consumed new request");
        complete[3]({{"kind", "int"}, {"integer", 7}}, "");
        Require(published.back().at("value").at("integer") == 7, "New session response lost");

        Call(channel, "papyrus.begin", Request("B", "one"));
        Call(channel, "papyrus.release", {{"scope", "B"}});
        Require(!pending[4]() && released == std::vector<std::string>{"B"}, "Scope release failed");
        Require(!Call(channel, "papyrus.begin", Request("B", "late")).at("success"), "Retired scope reopened");

        auto broken = Request("C", "throw");
        broken["name"] = "throw";
        Require(!Call(channel, "papyrus.begin", broken).at("success"), "Queue exception escaped");
        broken["name"] = "Show";
        Require(Call(channel, "papyrus.begin", broken).at("success"), "Queue failure was not rolled back");
        broken["operation"] = "unknown";
        Require(!Call(channel, "papyrus.begin", broken).at("success"), "Unknown operation accepted");
        broken = Request("C", "bad");
        broken["arguments"] = "not an array";
        Require(!Call(channel, "papyrus.begin", broken).at("success"), "Malformed arguments accepted");
        service.Stop();
        service.Stop();
        service.Reset();
        Require(resets == 2, "Repeated stop or reset released backend twice");
    }
    Require(!pending.back()(), "Stopped service still accepts completion");
    const auto before = published.size();
    complete.back()(nullptr, "after stop");
    Require(published.size() == before && resets == 2, "Stopped service published or failed to release state");
    Require(!Call(channel, "papyrus.begin", Request("A", "after stop")).at("success"), "Stopped registration retained");
}

} // namespace

int main()
{
    try
    {
        Check(false);
        Check(true);
        std::cout << "Papyrus queue, completion, cancellation, session reset, shutdown and serialization passed.\n";
        return 0;
    }
    catch (const std::exception& exception)
    {
        std::cerr << exception.what() << '\n';
        return 1;
    }
}
