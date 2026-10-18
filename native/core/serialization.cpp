#include "serialization.h"
#include <map>
#include <stdexcept>

namespace f4forge {
namespace {

auto& Factories()
{
    static std::map<std::string, SerializerRegistration::Factory, std::less<>> factories;
    return factories;
}

}

SerializerRegistration::SerializerRegistration(std::string format, Factory factory)
{
    if (!factory || !Factories().emplace(std::move(format), std::move(factory)).second) {
        throw std::logic_error("Invalid or duplicate serializer registration.");
    }
}

std::shared_ptr<const IDataSerializer> CreateDataSerializer(std::string_view format)
{
    const auto found = Factories().find(format);
    if (found == Factories().end()) {
        throw std::invalid_argument("Unsupported data format: " + std::string(format));
    }
    auto serializer = found->second();
    if (!serializer || serializer->Format() != format) {
        throw std::logic_error("Serializer factory returned an incompatible format.");
    }
    return serializer;
}

}
