#pragma once

#include <atomic>
#include <iostream>
#include <mutex>

namespace f4forge::test {

class Context final {
public:
    void Check(bool condition, const char* expression, const char* file, int line) noexcept
    {
        if (condition) return;
        {
            std::lock_guard lock(_outputMutex);
            std::cerr << file << ':' << line << ": check failed: " << expression << '\n';
        }
        _failures.fetch_add(1, std::memory_order_relaxed);
    }

    int Failures() const noexcept
    {
        return _failures.load(std::memory_order_relaxed);
    }

private:
    std::atomic<int> _failures{};
    std::mutex _outputMutex;
};

}

#define F4FORGE_CHECK(context, expression) \
    (context).Check(static_cast<bool>(expression), #expression, __FILE__, __LINE__)
