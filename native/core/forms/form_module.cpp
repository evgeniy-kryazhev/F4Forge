#include "form_module.h"

#include "RE/T/TESForm.h"
#include "RE/T/TESFullName.h"

#include <algorithm>
#include <cstring>

namespace f4forge::core {

namespace {

forms::FormType StableType(RE::ENUM_FORM_ID type) noexcept
{
    using E = RE::ENUM_FORM_ID;
    switch (type) {
    case E::kARMO: case E::kBOOK: case E::kINGR: case E::kMISC: case E::kWEAP:
    case E::kAMMO: case E::kKEYM: case E::kALCH: case E::kNOTE:
        return forms::FormType::Item;
    case E::kNPC_: case E::kACHR:
        return forms::FormType::Actor;
    case E::kREFR:
        return forms::FormType::Reference;
    case E::kCELL:
        return forms::FormType::Cell;
    case E::kWRLD:
        return forms::FormType::Worldspace;
    case E::kQUST:
        return forms::FormType::Quest;
    case E::kNONE:
        return forms::FormType::Unknown;
    default:
        return forms::FormType::Other;
    }
}

}

class FormModule::GameFormLookup final : public FormLookup {
public:
    bool GetInfo(uint32_t formId, FormRecord& result) override
    {
        const auto* form = RE::TESForm::GetFormByID(formId);
        if (form == nullptr) return false;
        result = { form->GetFormID(), StableType(form->GetFormType()) };
        return true;
    }

    bool GetName(uint32_t formId, std::string& result) override
    {
        const auto* form = RE::TESForm::GetFormByID(formId);
        if (form == nullptr) return false;
        result.assign(RE::TESFullName::GetFullName(*form));
        return true;
    }
};

FormModule::FormModule(EndpointRegistry& endpoints) noexcept :
    _endpoints(endpoints), _ownedLookup(std::make_unique<GameFormLookup>()), _lookup(_ownedLookup.get())
{}

FormModule::FormModule(EndpointRegistry& endpoints, FormLookup& lookup) noexcept :
    _endpoints(endpoints), _lookup(&lookup)
{}

FormModule::~FormModule()
{
    Stop();
    _endpoints.InvalidateOwner(&_owner);
}

bool FormModule::Start() noexcept
{
    if (_started.load(std::memory_order_acquire)) return true;
    if (!RegisterEndpoints()) return false;
    _started.store(true, std::memory_order_release);
    return true;
}

void FormModule::Stop() noexcept
{
    _ready.store(false, std::memory_order_release);
    _started.store(false, std::memory_order_release);
}

void FormModule::OnGameDataReady() noexcept
{
    if (Start()) _ready.store(true, std::memory_order_release);
}

bool FormModule::RegisterEndpoints() noexcept
{
    if (_getInfoEndpoint != F4FORGE_INVALID_HANDLE && _getNameEndpoint != F4FORGE_INVALID_HANDLE) return true;
    const F4ForgeEndpointDefinition info{
        sizeof(F4ForgeEndpointDefinition), F4FORGE_ENDPOINT_METHOD, forms::EndpointVersion, F4FORGE_ENDPOINT_NONE,
        F4FORGE_THREAD_GAME_ONLY, sizeof(forms::FormRequest), sizeof(forms::FormInfoResponse), 0,
        { forms::GetInfoEndpoint, sizeof(forms::GetInfoEndpoint) - 1 }, &GetInfoThunk, this
    };
    if (_endpoints.Register(info, &_owner, &_getInfoEndpoint) != F4FORGE_RESULT_SUCCESS) return false;
    const F4ForgeEndpointDefinition name{
        sizeof(F4ForgeEndpointDefinition), F4FORGE_ENDPOINT_METHOD, forms::EndpointVersion, F4FORGE_ENDPOINT_NONE,
        F4FORGE_THREAD_GAME_ONLY, sizeof(forms::FormRequest), sizeof(forms::FormNameResponse), 0,
        { forms::GetNameEndpoint, sizeof(forms::GetNameEndpoint) - 1 }, &GetNameThunk, this
    };
    if (_endpoints.Register(name, &_owner, &_getNameEndpoint) == F4FORGE_RESULT_SUCCESS) return true;
    _endpoints.InvalidateOwner(&_owner);
    _getInfoEndpoint = F4FORGE_INVALID_HANDLE;
    _getNameEndpoint = F4FORGE_INVALID_HANDLE;
    return false;
}

F4ForgeResult F4FORGE_CALL FormModule::GetInfoThunk(
    void* context, const void* request, uint32_t requestSize, void* response,
    uint32_t responseCapacity, uint32_t* responseSize) noexcept
{
    if (context == nullptr || request == nullptr || response == nullptr || responseSize == nullptr ||
        requestSize != sizeof(forms::FormRequest) || responseCapacity < sizeof(forms::FormInfoResponse))
        return F4FORGE_RESULT_INVALID_ARGUMENT;
    *responseSize = sizeof(forms::FormInfoResponse);
    return static_cast<FormModule*>(context)->GetInfo(
        *static_cast<const forms::FormRequest*>(request), *static_cast<forms::FormInfoResponse*>(response));
}

F4ForgeResult F4FORGE_CALL FormModule::GetNameThunk(
    void* context, const void* request, uint32_t requestSize, void* response,
    uint32_t responseCapacity, uint32_t* responseSize) noexcept
{
    if (context == nullptr || request == nullptr || response == nullptr || responseSize == nullptr ||
        requestSize != sizeof(forms::FormRequest) || responseCapacity < sizeof(forms::FormNameResponse))
        return F4FORGE_RESULT_INVALID_ARGUMENT;
    *responseSize = sizeof(forms::FormNameResponse);
    return static_cast<FormModule*>(context)->GetName(
        *static_cast<const forms::FormRequest*>(request), *static_cast<forms::FormNameResponse*>(response));
}

F4ForgeResult FormModule::GetInfo(const forms::FormRequest& request, forms::FormInfoResponse& response) noexcept
{
    response = {};
    if (!_ready.load(std::memory_order_acquire)) return F4FORGE_RESULT_NOT_READY;
    try {
        FormRecord record;
        if (!_lookup->GetInfo(request.formId, record)) return F4FORGE_RESULT_NOT_FOUND;
        response = { record.formId, static_cast<uint32_t>(record.formType), 1 };
        return F4FORGE_RESULT_SUCCESS;
    } catch (...) {
        return F4FORGE_RESULT_INTERNAL_ERROR;
    }
}

F4ForgeResult FormModule::GetName(const forms::FormRequest& request, forms::FormNameResponse& response) noexcept
{
    response = {};
    if (!_ready.load(std::memory_order_acquire)) return F4FORGE_RESULT_NOT_READY;
    try {
        std::string name;
        if (!_lookup->GetName(request.formId, name)) return F4FORGE_RESULT_NOT_FOUND;
        if (name.size() > forms::NameCapacity) return F4FORGE_RESULT_BUFFER_TOO_SMALL;
        response.length = static_cast<uint32_t>(name.size());
        std::memcpy(response.utf8, name.data(), name.size());
        return F4FORGE_RESULT_SUCCESS;
    } catch (...) {
        return F4FORGE_RESULT_INTERNAL_ERROR;
    }
}

}
