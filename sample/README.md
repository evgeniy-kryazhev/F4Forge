# F4Forge Samples

## HelloWorld

Build the first C# plugin from the repository root:

```powershell
dotnet build sample/HelloWorld/HelloWorld.csproj -c Release
```

Copy `sample/HelloWorld/bin/Release/net10.0/F4Forge.Sample.HelloWorld.dll` to the
configured F4Forge plugin directory. When the .NET runtime provider loads the plugin,
it writes `Hello world from F4Forge!` to the F4SE log during `OnLoad`.
