#pragma once

#include "form_contracts.h"
#include "../modules/builtin_module.h"
#include "../registry/endpoint_registry.h"

#include <atomic>
#include <string>

namespace f4forge::core {

struct FormRecord final {
    uint32_t formId{};
    forms::FormType formType{ forms::FormType::Unknown };
};

class FormLookup {
public:
    virtual ~FormLookup() = default;
    virtual bool GetInfo(uint32_t formId, FormRecord& result) = 0;
    virtual bool GetName(uint32_t formId, std::string& result) = 0;
};

class FormModule final : public BuiltinModule {
public:
    explicit FormModule(EndpointRegistry& endpoints) noexcept;
    FormModule(EndpointRegistry& endpoints, FormLookup& lookup) noexcept;
    ~FormModule() override;
    bool Start() noexcept override;
    void Stop() noexcept override;
    void OnGameDataReady() noexcept;

    F4ForgeEndpointHandle GetInfoEndpoint() const noexcept { return _getInfoEndpoint; }
    F4ForgeEndpointHandle GetNameEndpoint() const noexcept { return _getNameEndpoint; }

private:
    class GameFormLookup;
    static F4ForgeResult F4FORGE_CALL GetInfoThunk(
        void*, const void*, uint32_t, void*, uint32_t, uint32_t*) F4FORGE_NOEXCEPT;
    static F4ForgeResult F4FORGE_CALL GetNameThunk(
        void*, const void*, uint32_t, void*, uint32_t, uint32_t*) F4FORGE_NOEXCEPT;
    F4ForgeResult GetInfo(const forms::FormRequest&, forms::FormInfoResponse&) noexcept;
    F4ForgeResult GetName(const forms::FormRequest&, forms::FormNameResponse&) noexcept;
    bool RegisterEndpoints() noexcept;

    EndpointRegistry& _endpoints;
    std::unique_ptr<FormLookup> _ownedLookup;
    FormLookup* _lookup;
    EndpointOwner _owner;
    F4ForgeEndpointHandle _getInfoEndpoint{ F4FORGE_INVALID_HANDLE };
    F4ForgeEndpointHandle _getNameEndpoint{ F4FORGE_INVALID_HANDLE };
    std::atomic<bool> _started{};
    std::atomic<bool> _ready{};
};

}
