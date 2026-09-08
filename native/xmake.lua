includes("lib/commonlibf4")

set_project("F4Forge")
set_version("0.1.0")
set_license("GPL-3.0")
set_languages("c++23")
set_warnings("none")

add_rules("mode.debug", "mode.releasedbg")
add_rules("plugin.vsxmake.autoupdate")

target("F4Forge")
    add_rules("commonlibf4.plugin", {
        name = "F4Forge",
        author = "F4Forge contributors",
        description = "Generic runtime host for Fallout 4 plugins"
    })
    add_files("src/**.cpp")
    add_headerfiles("src/**.h")
    add_includedirs("src", "abi")
    add_cxxflags("/W4", "/WX", "/permissive-", "/EHsc", "/utf-8", { public = false })
    set_pcxxheader("src/pch.h")

target("F4ForgeAbiTests")
    set_kind("binary")
    add_files("tests/abi/AbiTests.cpp")
    add_includedirs("abi")
    add_cxxflags("/W4", "/WX", "/permissive-", "/EHsc", "/utf-8", { public = false })

target("F4ForgeAbiCCompile")
    set_kind("binary")
    add_files("tests/abi/AbiCCompile.c")
    add_includedirs("abi")
    add_cflags("/W4", "/WX", "/TC", { public = false })
