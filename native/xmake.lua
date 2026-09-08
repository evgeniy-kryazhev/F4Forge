includes("lib/commonlibf4")

set_project("F4Forge")
set_version("0.1.0")
set_license("GPL-3.0")
set_languages("c++23")
set_warnings("allextra")

add_rules("mode.debug", "mode.releasedbg")
add_rules("plugin.vsxmake.autoupdate")

target("F4Forge")
    add_rules("commonlibf4.plugin", {
        name = "F4Forge",
        author = "F4Forge contributors",
        description = "Generic runtime host for Fallout 4 plugins"
    })
    add_files("src/**.cpp", "core/**.cpp")
    add_headerfiles("src/**.h")
    add_includedirs("src", "abi", "core")
    add_cxxflags("/WX", "/permissive-", "/EHsc", "/utf-8", { public = false })
    set_pcxxheader("src/pch.h")

target("F4ForgeAbiTests")
    set_kind("binary")
    add_files("tests/abi/AbiTests.cpp")
    add_includedirs("abi")
    add_cxxflags("/WX", "/permissive-", "/EHsc", "/utf-8", { public = false })

target("F4ForgeAbiCCompile")
    set_kind("binary")
    add_files("tests/abi/AbiCCompile.c")
    add_includedirs("abi")
    add_cflags("/WX", "/TC", { public = false })

target("F4ForgeRegistryTests")
    set_kind("binary")
    add_files("core/registry/endpoint_registry.cpp", "tests/registry/EndpointRegistryTests.cpp")
    add_includedirs("abi", "core")
    add_cxxflags("/WX", "/permissive-", "/EHsc", "/utf-8", { public = false })

target("F4ForgeModuleTests")
    set_kind("binary")
    add_files("core/registry/endpoint_registry.cpp", "core/modules/module_manager.cpp", "tests/modules/ModuleManagerTests.cpp")
    add_includedirs("abi", "core")
    add_cxxflags("/WX", "/permissive-", "/EHsc", "/utf-8", { public = false })

target("F4ForgeHostTests")
    set_kind("binary")
    add_files("core/registry/*.cpp", "core/modules/module_manager.cpp", "core/f4forge_host.cpp", "tests/host/F4ForgeHostTests.cpp")
    add_includedirs("abi", "core")
    add_cxxflags("/WX", "/permissive-", "/EHsc", "/utf-8", { public = false })

target("F4ForgeEventTests")
    set_kind("binary")
    add_files("core/registry/*.cpp", "tests/registry/EventInterceptorRegistryTests.cpp")
    add_includedirs("abi", "core")
    add_cxxflags("/WX", "/permissive-", "/EHsc", "/utf-8", { public = false })

target("F4ForgeRuntimeTests")
    set_kind("binary")
    add_files("core/runtime/runtime_manager.cpp", "tests/runtime/RuntimeManagerTests.cpp")
    add_includedirs("abi", "core")
    add_cxxflags("/WX", "/permissive-", "/EHsc", "/utf-8", { public = false })

target("F4ForgeConfigTests")
    set_kind("binary")
    add_files("core/config/config.cpp", "tests/config/ConfigTests.cpp")
    add_includedirs("core")
    add_cxxflags("/WX", "/permissive-", "/EHsc", "/utf-8", { public = false })
