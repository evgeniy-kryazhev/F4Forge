#include "forms/form_module.h"
#include "test_harness.h"

#include <cstring>
#include <stdexcept>
#include <unordered_map>

namespace {

bool onGameThread = true;
int32_t F4FORGE_CALL CheckGameThread() noexcept { return onGameThread ? 1 : 0; }

class FakeLookup final : public f4forge::core::FormLookup {
public:
    bool GetInfo(uint32_t formId, f4forge::core::FormRecord& result) override
    {
        if (throws) throw std::runtime_error("lookup");
        if (formId != 0x1234) return false;
        result = { formId, f4forge::core::forms::FormType::Item };
        return true;
    }

    bool GetName(uint32_t formId, std::string& result) override
    {
        if (throws) throw std::runtime_error("lookup");
        if (formId != 0x1234) return false;
        result = name;
        return true;
    }

    bool throws{};
    std::string name{ "Stimpak" };
};

}

int main()
{
    using namespace f4forge::core;
    using namespace f4forge::core::forms;
    f4forge::test::Context test;
    EndpointRegistry endpoints;
    endpoints.SetGameThreadCheck(&CheckGameThread);
    FakeLookup lookup;
    FormModule module(endpoints, lookup);
    F4FORGE_CHECK(test, module.Start());

    FormRequest request{ 0x1234 };
    FormInfoResponse info{};
    uint32_t size{};
    F4FORGE_CHECK(test, endpoints.Invoke(module.GetInfoEndpoint(), &request, sizeof(request), &info, sizeof(info), &size) == F4FORGE_RESULT_NOT_READY);
    module.OnGameDataReady();
    F4FORGE_CHECK(test, endpoints.Invoke(module.GetInfoEndpoint(), &request, sizeof(request), &info, sizeof(info), &size) == F4FORGE_RESULT_SUCCESS);
    F4FORGE_CHECK(test, size == sizeof(info) && info.formId == request.formId && info.isValid == 1);
    F4FORGE_CHECK(test, info.formType == static_cast<uint32_t>(FormType::Item));

    request.formId = 9;
    F4FORGE_CHECK(test, endpoints.Invoke(module.GetInfoEndpoint(), &request, sizeof(request), &info, sizeof(info), &size) == F4FORGE_RESULT_NOT_FOUND);
    F4FORGE_CHECK(test, info.isValid == 0);
    F4FORGE_CHECK(test, endpoints.Invoke(module.GetInfoEndpoint(), &request, sizeof(request) - 1, &info, sizeof(info), &size) == F4FORGE_RESULT_INVALID_REQUEST_SIZE);
    F4FORGE_CHECK(test, endpoints.Invoke(module.GetInfoEndpoint(), &request, sizeof(request), &info, sizeof(info) - 1, &size) == F4FORGE_RESULT_INVALID_RESPONSE_CAPACITY);

    onGameThread = false;
    F4FORGE_CHECK(test, endpoints.Invoke(module.GetInfoEndpoint(), &request, sizeof(request), &info, sizeof(info), &size) == F4FORGE_RESULT_WRONG_THREAD);
    onGameThread = true;

    request.formId = 0x1234;
    FormNameResponse name{};
    F4FORGE_CHECK(test, endpoints.Invoke(module.GetNameEndpoint(), &request, sizeof(request), &name, sizeof(name), &size) == F4FORGE_RESULT_SUCCESS);
    F4FORGE_CHECK(test, name.length == 7 && std::memcmp(name.utf8, "Stimpak", 7) == 0);
    lookup.name.assign(NameCapacity, 'x');
    F4FORGE_CHECK(test, endpoints.Invoke(module.GetNameEndpoint(), &request, sizeof(request), &name, sizeof(name), &size) == F4FORGE_RESULT_SUCCESS);
    F4FORGE_CHECK(test, name.length == NameCapacity);
    lookup.name.push_back('x');
    F4FORGE_CHECK(test, endpoints.Invoke(module.GetNameEndpoint(), &request, sizeof(request), &name, sizeof(name), &size) == F4FORGE_RESULT_BUFFER_TOO_SMALL);
    lookup.throws = true;
    F4FORGE_CHECK(test, endpoints.Invoke(module.GetInfoEndpoint(), &request, sizeof(request), &info, sizeof(info), &size) == F4FORGE_RESULT_INTERNAL_ERROR);
    return test.Failures() == 0 ? 0 : 1;
}
