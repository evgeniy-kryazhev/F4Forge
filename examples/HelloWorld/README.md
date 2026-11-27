# HelloWorld

Плагин пишет `Hello world` при загрузке игры и код клавиши при каждом новом нажатии на клавиатуре.

1. Из корня репозитория собери ядро командой `xmake build F4Forge`, затем пример командой `dotnet build examples/HelloWorld/HelloWorld.csproj -c Release`.
2. Скопируй `build/dist/F4Forge.dll` в `Data/F4SE/Plugins/`, а `examples/HelloWorld/bin/Release/net10.0/HelloWorld.dll` в `Data/F4Forge/` или в подпапку `Data/F4Forge/HelloWorld/`. Обновляй обе DLL из одной сборки проекта: SDK с типом `Events.ButtonEvent` встроен в ядро. При использовании Mod Organizer 2 эти пути отсчитываются от корня мода без префикса `Data/`.
3. Загрузи сохранение и открой `Data/F4SE/Plugins/F4Forge/Net.log`. В журнале появится строка с меткой `[INFO] [HelloWorld] Hello world`.
4. Нажми клавишу в игре. В журнале появится запись вида `[INFO] [HelloWorld] Keyboard key pressed: 30`, где число — код нажатой клавиши.
