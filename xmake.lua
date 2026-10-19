set_xmakever("3.1.0")
set_runtimes("MT")
includes("native/lib/commonlibf4")
set_project("F4Forge")
set_version("0.3.0")
set_languages("c++23")
set_arch("x64")
add_rules("mode.debug", "mode.releasedbg")
add_requires("nlohmann_json 3.12.0")
add_requires("minhook v1.3.4", {configs = {shared = false}})

rule("f4forge.native")
    on_load(function(target)
        target:add("includedirs", "native/core")
        target:add("packages", "nlohmann_json")
        target:add("cxxflags", "/W4", "/WX", "/permissive-", "/EHsc", "/utf-8")
    end)
rule_end()

-- Each SDK supplies its own build description; the core list stays unchanged.
includes("sdk/*/xmake.lua")

target("F4Forge.Core")
    set_kind("shared")
    set_targetdir("build/bin")
    add_rules("f4forge.native")
    add_packages("minhook")
    for _, directory in ipairs(os.dirs("sdk/*")) do
        add_rules("f4forge.sdk." .. path.filename(directory))
    end
    add_rules("commonlibf4.plugin", {
        name = "F4Forge.Core",
        author = "F4Forge contributors",
        description = "Modular SDK host"
    })
    add_files("native/src/**.cpp", "native/core/**.cpp", "sdk/*/native/**.cpp")
    add_files("native/modules/**.rc")
    for _, source in ipairs(os.files("native/modules/**.cpp")) do
        add_files(source)
    end
    set_pcxxheader("native/lib/commonlibf4/include/F4SE/Impl/PCH.h")

target("F4Forge.Channel.Tests")
    set_kind("binary")
    set_targetdir("build/tests")
    add_rules("f4forge.native")
    add_files("native/core/**.cpp", "native/tests/channel_tests.cpp")

target("F4Forge.TestTransport")
    set_kind("shared")
    set_targetdir("build/tests")
    add_rules("f4forge.native")
    add_files("native/core/**.cpp", "native/tests/test_transport.cpp")
    add_files("native/modules/interception/registry.cpp")

target("F4Forge.Events.Tests")
    set_kind("binary")
    set_targetdir("build/tests")
    add_rules("f4forge.native")
    add_files("native/core/**.cpp", "native/tests/event_tests.cpp")

target("F4Forge.Components.Tests")
    set_kind("binary")
    set_targetdir("build/tests")
    add_rules("f4forge.native")
    add_files("native/core/**.cpp", "native/tests/component_tests.cpp")

target("F4Forge.Hosting.Tests")
    set_kind("binary")
    set_targetdir("build/tests")
    set_rundir(".")
    add_rules("f4forge.native")
    for _, directory in ipairs(os.dirs("sdk/*")) do
        add_rules("f4forge.sdk." .. path.filename(directory))
    end
    add_deps("F4Forge.Net", "F4Forge.Net.Fixture")
    add_files("native/core/**.cpp", "sdk/*/native/**.cpp", "native/tests/hosting_tests.cpp")
