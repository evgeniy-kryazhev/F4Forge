# F4Forge

Минимальный модульный хост SDK для Fallout 4 / F4SE, Windows x64.

- `F4Forge.Core.dll` — нативное ядро, канал сообщений, F4SE и нативные загрузчики SDK.
- `F4Forge.Net.dll` — C# API, загрузка .NET-модов и управляемая сторона канала.
- `sdk/<language>/` — независимый SDK; он сам определяет правила загрузки своих модов.

Игровых функций пока нет. Версионная совместимость старого API не поддерживается.

## Сборка и проверка

Нужны MSVC с C++23, .NET SDK 10 x64, xmake 3.1+ и зависимости подмодуля:

```powershell
git submodule update --init --recursive
xmake f -m releasedbg -y
xmake -j 2
xmake run F4Forge.Channel.Tests
xmake run F4Forge.Components.Tests
xmake run F4Forge.Hosting.Tests
dotnet test tests/dotnet/F4Forge.Net.Tests/F4Forge.Net.Tests.csproj -c Release
```

Две DLL для установки находятся в `build/bin/`. PDB, LIB и EXP нужны только для разработки.
Положите `F4Forge.Core.dll` и `F4Forge.Net.dll` рядом в `Data/F4SE/Plugins/`.
На машине игрока нужны F4SE и .NET Runtime 10 x64. Дополнительный `nethost.dll` не нужен:
его код включён в Core. Конфигурация запуска CLR создаётся во временном файле и удаляется.

## .NET-моды

Каждый мод размещается в `Data/F4SE/Plugins/F4Forge/mods/dotnet/<Name>/` и содержит
свою DLL, зависимости и `mod.json`: `{"assembly":"MyMod.dll"}`.
Мод ссылается на `F4Forge.Net.dll` и содержит ровно один публичный класс `IMod`:

```csharp
public sealed class MyMod : IMod
{
    public void Start(ModContext context)
    {
        context.Messages.Register("mymod.title", _ => "My mod");
    }

    public void Dispose()
    {
        // Stop background work and release resources owned by the mod.
    }
}
```

SDK автоматически снимает регистрации при остановке или ошибке запуска мода.
Моды изолированы через `AssemblyLoadContext`; ошибки отдельных модов записываются в
`F4Forge/Net.log`. Автоматическая перезагрузка модов пока не реализована.

## Общий канал и расширение

Запрос: `{"method":"mymod.title","arguments":null}`.
Ответ: `{"value":"My mod"}` или `{"error":{"code":"method_not_found","message":"..."}}`.

C#: `context.Messages.Call<string>("mymod.title", null)`.
C++: зарегистрируйте обработчик через `Channel::Add`, вызовите через `Channel::Call`.
Канал синхронный, сериализует обработчики и допускает вложенные вызовы в том же потоке.
Не ждите внутри обработчика другой поток, который обращается к каналу. Передаваемые JSON
буферы копируются во время вызова; указатели и игровые объекты через канал не передаются.
Лимит сообщения — 1 MiB. Канал не переносит вызовы на игровой поток автоматически.

Нативные модули запускаются раньше компонентов с фазой `ComponentPhase::Sdk`.
Новая функция добавляет файл в `native/modules/` с `ComponentRegistration` и отдельный
C# extension-класс в SDK. Центральный маршрутизатор и таблица interop не меняются.
Новый SDK добавляет `sdk/<language>/xmake.lua` с правилом `f4forge.sdk.<language>` и
`native/*.cpp` с регистрацией компонента. Повторите `xmake f` после добавления файлов.
Все нативные адаптеры попадут в Core; новые языковые DLL получают собственное имя `F4Forge.<Language>`.

## Инструменты разработки

Навыки: `.agents/skills/cpp-pro` из `Jeffallan/claude-skills` и
`.agents/skills/dotnet-pinvoke` из официального `dotnet/skills`.
Проектные MCP в `.codex/config.toml`: Microsoft Learn (MSVC/C++/.NET) и Context7
(документация библиотек). После установки перезапустите MCP-соединения; навыки доступны
со следующего хода. MCP нужны только помощнику разработчика, не сборке и не игре.
