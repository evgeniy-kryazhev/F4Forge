#include "config/config.h"

#include <cassert>
#include <filesystem>
#include <fstream>

int main()
{
    const auto base = std::filesystem::path("C:/F4Forge");
    const auto defaults = f4forge::core::ConfigLoader::Defaults(base);
    assert(defaults.pluginDirectory == base / "Plugins");
    assert(defaults.runtimeDirectory == base);

    const auto missing = f4forge::core::ConfigLoader::Load(base / "missing.toml", base);
    assert(missing.pluginDirectory == base / "Plugins");
    assert(missing.logLevel == "info");

    const auto file = std::filesystem::temp_directory_path() / "f4forge-config-test.toml";
    {
        std::ofstream output(file);
        output << "[framework]\nlog_level = \"debug\"\n"
                  "[plugins]\ndirectory = \"Managed\"\n"
                  "[runtimes]\ndirectory = \"Runtime\"\n";
    }
    const auto loaded = f4forge::core::ConfigLoader::Load(file, base);
    assert(loaded.logLevel == "debug");
    assert(loaded.pluginDirectory == base / "Managed");
    assert(loaded.runtimeDirectory == base / "Runtime");
    std::filesystem::remove(file);
    return 0;
}
