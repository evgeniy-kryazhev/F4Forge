#pragma once

namespace RE {
namespace detail {

template <class Character, bool CaseSensitive>
class BSFixedString {
public:
    BSFixedString(const Character* text) : text_(text) {}
    const Character* c_str() const { return text_; }

private:
    const Character* text_;
};

}

using BSFixedString = detail::BSFixedString<char, false>;

enum class FixtureMode : unsigned int {
    First = 1,
    Last = 0xFFFFFFFF
};

class FixtureSingleton {
public:
    inline static FixtureSingleton* current = nullptr;
    virtual ~FixtureSingleton() = default;

    static FixtureSingleton* GetSingleton() { return current; }
    bool IsEnabled() const { return enabled; }
    void SetEnabled(bool value) { enabled = value; }
    int Overload(int value) const { return value + 1; }
    bool Overload(bool value) const { return !value; }
    int CharacterOverload(char) const { return 5; }
    int CharacterOverload(int) const { return 9; }
    FixtureMode EchoMode(FixtureMode value) const { return value; }
    BSFixedString Echo(const BSFixedString& value) const { return value; }
    int* BorrowPointer() { return &count; }
    void MutateReference(int& value) { ++value; }
    static bool StaticMethod() { return true; }
    virtual bool VirtualMethod() const { return true; }

    template <class T>
    T TemplateMethod(T value) { return value; }

    bool enabled = false;
    int count = 7;
    unsigned int flags : 3 = 5;

private:
    bool HiddenMethod() const { return true; }
};

class FixtureReferenceSingleton {
public:
    static FixtureReferenceSingleton& GetSingleton()
    {
        static FixtureReferenceSingleton instance;
        return instance;
    }

    int value = 42;
};

class OrdinaryClass {
public:
    bool enabled = false;
};

}
