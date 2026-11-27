set_xmakever("3.1.0")
set_runtimes("MT")
includes("native/lib/commonlibf4")
set_project("F4Forge")
set_version("0.3.1")
set_languages("c++23")
set_arch("x64")
add_rules("mode.debug", "mode.releasedbg")
add_cxxflags("/W4", "/WX", "/permissive-", "/EHsc", "/utf-8")

includes("sdk/*/xmake.lua")

target("F4Forge.Core")
    set_kind("shared")
    set_targetdir("build/bin")
    add_rules("f4forge.sdk.dotnet")
    add_rules("commonlibf4.plugin", {
        name = "F4Forge.Core",
        author = "F4Forge contributors",
        description = "Managed plugin loader"
    })
    add_files("native/src/**.cpp", "sdk/*/native/**.cpp")
    set_pcxxheader("native/lib/commonlibf4/include/F4SE/Impl/PCH.h")

target("F4Forge.Hosting.Tests")
    set_kind("binary")
    set_targetdir("build/tests")
    set_rundir(".")
    add_rules("f4forge.sdk.dotnet")
    add_deps("F4Forge.Net.Fixture")
    add_files("sdk/*/native/**.cpp", "native/tests/hosting_tests.cpp")
