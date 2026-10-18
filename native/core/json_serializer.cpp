#include "serialization.h"

namespace f4forge {
namespace {

class JsonDataSerializer final : public IDataSerializer {
public:
    std::string_view Format() const noexcept override
    {
        return "json";
    }

    std::string Serialize(const DataValue& value) const override
    {
        return value.dump(-1, ' ', false, DataValue::error_handler_t::replace);
    }

    DataValue Deserialize(std::string_view bytes) const override
    {
        return DataValue::parse(bytes);
    }
};

const SerializerRegistration registration("json", [] {
    return std::make_shared<JsonDataSerializer>();
});

}
}
