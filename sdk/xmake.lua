local sdk_output = "sdk/src/bin/Release/net10.0"
local resource_file = "build/generated/dotnet/managed_sdk.rc"

option("dotnet_host_dir")
    set_showmenu(true)
    set_description("Optional path to the .NET 10 win-x64 native host pack")
option_end()

rule("f4forge.sdk.dotnet")
    on_load(function(target)
        target:add("deps", "F4Forge.Net")
        target:add("files", path.join(os.projectdir(), resource_file), {always_added = true})
        import("core.base.semver")
        local hostdir = get_config("dotnet_host_dir")
        if not hostdir then
            local dotnetroot = os.getenv("DOTNET_ROOT") or path.join(os.getenv("ProgramFiles"), "dotnet")
            local versions = os.dirs(path.join(dotnetroot, "packs/Microsoft.NETCore.App.Host.win-x64/10.*"))
            table.sort(versions, function(left, right)
                return semver.compare(path.filename(left), path.filename(right)) > 0
            end)
            assert(#versions > 0, "Install the .NET 10 x64 SDK or set --dotnet_host_dir")
            hostdir = path.join(versions[1], "runtimes/win-x64/native")
        end
        assert(os.isfile(path.join(hostdir, "nethost.h")), "Invalid .NET host directory")
        target:add("includedirs", hostdir)
        target:add("includedirs", path.join(os.projectdir(), "native/src/hosting"))
        target:add("linkdirs", hostdir)
        target:add("links", "libnethost")
        target:add("syslinks", "advapi32")
    end)
rule_end()

target("F4Forge.Net")
    set_kind("phony")
    add_deps("F4Forge.Bindings")
    set_policy("build.fence", true)
    on_build(function()
        os.vrunv("dotnet", {"build", "sdk/src/F4Forge.Net.csproj", "-c", "Release", "-p:F4ForgeBindingsReady=true"})

        local assembly = sdk_output .. "/F4Forge.Net.dll"
        local config = sdk_output .. "/F4Forge.Net.runtimeconfig.json"
        -- RC does not track binary inputs. Make the script change with either payload.
        local script = string.format("// %s %s\nF4FORGE_SDK RCDATA \"%s\"\nF4FORGE_CONFIG RCDATA \"%s\"\n",
            hash.sha256(assembly), hash.sha256(config), assembly, config)
        if not os.isfile(resource_file) or io.readfile(resource_file) ~= script then
            os.mkdir(path.directory(resource_file))
            io.writefile(resource_file, script)
        end
    end)

target("F4Forge.Net.Fixture")
    set_kind("phony")
    add_deps("F4Forge.Net")
    on_build(function()
        os.vrunv("dotnet", {"build", "tests/dotnet/Fixture/Fixture.csproj", "-c", "Release", "-p:F4ForgeBindingsReady=true"})
        os.vrunv("dotnet", {"build", "tests/dotnet/BridgeProbe/BridgeProbe.csproj", "-c", "Release", "-p:F4ForgeBindingsReady=true"})
    end)
