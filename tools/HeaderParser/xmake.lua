option("llvm_dir")
    set_showmenu(true)
    set_description("LLVM x64 SDK containing clang-c/Index.h and libclang")
option_end()

target("F4Forge.HeaderParser")
    set_kind("binary")
    set_default(false)
    add_files("main.cpp", "event_catalog.cpp")
    on_load(function(target)
        local root = get_config("llvm_dir") or os.getenv("LLVM_ROOT")
        if not root then
            root = path.join(os.projectdir(), "build/toolchains/llvm")
            if not os.isdir(root) then
                root = path.join(os.getenv("ProgramFiles"), "LLVM")
            end
        end
        root = path.absolute(root)
        assert(os.isfile(path.join(root, "include/clang-c/Index.h")), "Install LLVM x64 or set xmake f --llvm_dir=<LLVM SDK>")
        target:add("sysincludedirs", path.join(root, "include"))
        target:add("linkdirs", path.join(root, "lib"))
        target:add("links", "libclang")
        target:data_set("llvm.root", root)
    end)

target("F4Forge.Bindings")
    set_kind("phony")
    set_default(false)
    set_policy("build.fence", true)
    add_deps("F4Forge.HeaderParser")
    on_build(function(target)
        import("core.project.depend")
        import("core.project.project")
        local parser = target:dep("F4Forge.HeaderParser")
        local llvm = parser:data("llvm.root")
        local common = path.join(os.projectdir(), "native/lib/commonlibf4")
        local headers = path.join(common, "include")
        local output = path.join(os.projectdir(), "build/generated/bindings")
        local args = {path.join(headers, "RE/Fallout.h"), path.join(headers, "RE"), output,
            "-x", "c++", "-std=c++23", "--target=x86_64-pc-windows-msvc", "-fms-extensions",
            "-D_ALLOW_COMPILER_AND_STL_VERSION_MISMATCH", "-DNOMINMAX", "-D_MT", "-Wno-everything",
            is_mode("debug") and "-D_DEBUG" or "-DNDEBUG",
            "-I" .. headers, "-I" .. path.join(common, "lib/commonlib-shared/include")}
        local shared = project.target("commonlib-shared")
        for _, key in ipairs({"sysincludedirs", "includedirs", "defines"}) do
            local groups = shared:get_from(key, "*")
            for _, group in ipairs(groups or {}) do
                for _, value in ipairs(table.wrap(group)) do
                    table.insert(args, (key == "defines" and "-D" or "-I") .. value)
                end
            end
        end
        local files = os.files(path.join(headers, "**.h"))
        table.join2(files, os.files(path.join(common, "lib/commonlib-shared/include/**.h")))
        table.insert(files, parser:targetfile())
        table.insert(files, path.join(llvm, "bin/libclang.dll"))
        table.sort(files)
        local complete = os.isfile(path.join(output, "AutoRegistry.cpp")) and os.isfile(path.join(output, "AutoBindings.g.cs"))
            and os.isfile(path.join(output, "AutoEventSinks.h")) and os.isfile(path.join(output, "API.md"))
        depend.on_changed(function()
            local env = parser:toolchain("msvc"):runenvs()
            os.vrunv(parser:targetfile(), args, {envs = env, addenvs = {PATH = path.join(llvm, "bin")}})
        end, {files = files, values = args, changed = not complete, dependfile = path.join(output, "generation.d")})
    end)
