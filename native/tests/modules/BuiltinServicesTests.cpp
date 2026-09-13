#include "modules/builtin_services.h"
#include "test_harness.h"

namespace {

class FakeModule final : public f4forge::core::BuiltinModule {
public:
    bool Start() noexcept override
    {
        ++starts;
        active = succeed;
        return active;
    }

    void Stop() noexcept override
    {
        ++stops;
        active = false;
    }

    bool succeed{ true };
    bool active{};
    unsigned starts{};
    unsigned stops{};
};

}

int main()
{
    f4forge::test::Context test;
    FakeModule events;
    FakeModule input;
    input.succeed = false;
    f4forge::core::BuiltinServices services(events, input);

    F4FORGE_CHECK(test, !services.Start());
    F4FORGE_CHECK(test, !events.active && !input.active);
    F4FORGE_CHECK(test, events.stops == 1 && input.stops == 1);

    input.succeed = true;
    F4FORGE_CHECK(test, services.Start());
    F4FORGE_CHECK(test, services.Start());
    F4FORGE_CHECK(test, events.starts == 2 && input.starts == 2);

    services.Stop();
    services.Stop();
    F4FORGE_CHECK(test, !events.active && !input.active);
    return test.Failures() == 0 ? 0 : 1;
}
