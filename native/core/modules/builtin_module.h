#pragma once

namespace f4forge::core {

class BuiltinModule {
public:
    virtual ~BuiltinModule() = default;
    virtual bool Start() noexcept = 0;
    virtual void Stop() noexcept = 0;
};

}
