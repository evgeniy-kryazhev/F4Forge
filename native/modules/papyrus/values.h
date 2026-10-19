#pragma once

#include "channel.h"
#include "serialization.h"
#include <RE/Fallout.h>

namespace f4forge::papyrus
{

class Objects final
{
  public:
    explicit Objects(Channel& channel) : channel_(channel)
    {
    }
    void Open(const std::string& scope);
    void Release(std::string_view scope);
    void Reset();
    std::string Store(const std::string& scope, RE::BSTSmartPointer<RE::BSScript::Object> object);
    RE::BSTSmartPointer<RE::BSScript::Object> Get(const std::string& scope, const std::string& token);
    void ValidateForm(const DataValue& reference);
    std::uint64_t Session() const noexcept
    {
        return session_.load();
    }

  private:
    using Entries = std::map<std::string, RE::BSTSmartPointer<RE::BSScript::Object>>;
    std::mutex gate_;
    Channel& channel_;
    std::atomic<std::uint64_t> session_ = 1;
    std::map<std::string, Entries, std::less<>> scopes_;
    std::uint64_t next_ = 0;
};

class Values final
{
  public:
    Values(RE::BSScript::IVirtualMachine& vm, std::shared_ptr<Objects> objects, std::string scope,
           std::uint64_t session);
    RE::BSTSmartPointer<RE::BSScript::Object> Resolve(const DataValue& reference,
                                                      const RE::BSScript::TypeInfo* expected = nullptr);
    RE::BSScript::Variable Decode(const DataValue& value, const RE::BSScript::TypeInfo& expected, unsigned depth = 0);
    DataValue Encode(const RE::BSScript::Variable& value, unsigned depth = 0);

  private:
    RE::BSScript::TypeInfo Infer(const DataValue& value);
    RE::BSScript::IVirtualMachine& vm_;
    std::shared_ptr<Objects> objects_;
    std::string scope_;
    std::uint64_t session_;
};

} // namespace f4forge::papyrus
