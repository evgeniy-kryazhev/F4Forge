# HelloWorld

Плагин пишет `Hello world` в журнал F4Forge при каждом событии `GameEvents.OnGameLoaded`.

1. Из корня репозитория выполни `dotnet build examples/HelloWorld/HelloWorld.csproj -c Release`.
2. Скопируй `examples/HelloWorld/bin/Release/net10.0/HelloWorld.dll` в `Data/F4Forge/` или в подпапку `Data/F4Forge/HelloWorld/`. Для примера нужна версия F4Forge с `GameEvents.OnGameLoaded`.
3. Загрузи сохранение и открой `Data/F4SE/Plugins/F4Forge/Net.log`. В журнале появится строка с меткой `[INFO] [HelloWorld] Hello world`.
