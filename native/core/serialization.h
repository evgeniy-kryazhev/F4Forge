#pragma once

#include <functional>
#include <memory>
#include <string>
#include <string_view>
#include <nlohmann/json.hpp>

namespace f4forge {

// A value tree, independent of the encoding used on the wire.
using DataValue = nlohmann::json;
using Json = DataValue;

class IDataSerializer {
public:
    virtual ~IDataSerializer() = default;
    virtual std::string_view Format() const noexcept = 0;
    virtual std::string Serialize(const DataValue& value) const = 0;
    virtual DataValue Deserialize(std::string_view bytes) const = 0;
};

class SerializerRegistration final {
public:
    using Factory = std::function<std::shared_ptr<const IDataSerializer>()>;
    SerializerRegistration(std::string format, Factory factory);
};

std::shared_ptr<const IDataSerializer> CreateDataSerializer(std::string_view format = "json");

}
