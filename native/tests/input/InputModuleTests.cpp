#include "modules/input_module.h"
#include "test_harness.h"

int main()
{
    f4forge::test::Context test;
    F4FORGE_CHECK(test, f4forge::core::InputModule::NormalizeKey(0x41) == F4FORGE_KEY_A);
    F4FORGE_CHECK(test, f4forge::core::InputModule::NormalizeKey(0xDE) == F4FORGE_KEY_APOSTROPHE);
    F4FORGE_CHECK(test, f4forge::core::InputModule::NormalizeKey(0x10000) == F4FORGE_KEY_UNKNOWN);
    return test.Failures() == 0 ? 0 : 1;
}
