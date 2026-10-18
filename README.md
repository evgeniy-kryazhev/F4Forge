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
xmake run F4Forge.Events.Tests
xmake run F4Forge.Components.Tests
xmake run F4Forge.Hosting.Tests
dotnet test tests/dotnet/F4Forge.Net.Tests/F4Forge.Net.Tests.csproj -c Release
```

Две DLL для установки находятся в `build/bin/`. PDB, LIB и EXP нужны только для разработки.
Положите `F4Forge.Core.dll` в `Data/F4SE/Plugins/`, а `F4Forge.Net.dll` — в
`Data/F4SE/Plugins/F4Forge/`. В корне `Plugins` должны быть только нативные плагины:
управляемая DLL SDK прерывает сканирование F4SE. При обновлении удалите старую
`Data/F4SE/Plugins/F4Forge.Net.dll`; в MO2 устанавливайте архив с полной заменой мода.
На машине игрока нужны F4SE и .NET Runtime 10 x64. Дополнительный `nethost.dll` не нужен:
его код включён в Core. Конфигурация запуска CLR создаётся во временном файле и удаляется.

## .NET-моды

DLL модов размещаются в `Data/F4ForgeNet/`; поддерживаются подпапки любой глубины.
Загрузчик рекурсивно ищет DLL без манифестов. Библиотеки без реализации `IMod`,
копии SDK и нативные DLL пропускаются. DLL мода ссылается на `F4Forge.Net.dll`
и содержит ровно один публичный класс `IMod` с конструктором без параметров:

```text
Data/
  F4SE/Plugins/
    F4Forge.Core.dll
    F4Forge/
      F4Forge.Net.dll
  F4ForgeNet/
    MyMod.dll
    OtherMod/
      OtherMod.dll
      Dependency.dll
```

Зависимости кладите рядом с DLL мода. `context.Directory` указывает на папку этой DLL.
Загрузка идёт в порядке путей; порядок не заменяет механизм зависимостей между модами.

```csharp
public sealed class MyMod : IMod
{
    public void Start(ModContext context)
    {
        context.Messages.Register<object?, string>("mymod.title", _ => "My mod");
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
Ответ: `{"success":true,"value":"My mod"}` или
`{"success":false,"error":{"code":"method_not_found","message":"..."}}`.

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

## События

Канал событий доступен через `channel.Events()` в C++ и `context.Events` в C#:

```cpp
channel.Events().Publish("game.loaded", {{"saveName", saveName}});
```

```csharp
var subscription = context.Events.Subscribe<GameLoaded>(
    "game.loaded",
    notification => Console.WriteLine(notification.SaveName));

context.Events.Publish("mymod.ready", new { name = "My mod" });
subscription.Dispose();

public sealed record GameLoaded(string SaveName);
```

Это пример публикации из игрового модуля: сам канал не устанавливает игровые перехватчики.
Подписки принадлежат моду и снимаются при остановке или ошибке запуска. Подписчики
вызываются последовательно, в порядке регистрации, в потоке публикации. Истории и
повторной доставки нет. Ошибка подписчика записывается в лог и не прерывает остальных.
Новые подписки не получают уже начавшуюся публикацию; снятые до своей очереди пропускаются.

Сообщения и события используют общую последовательную область исполнения, допускающую
вложенные вызовы в том же потоке. Блокировка реестра во время обработчиков не удерживается.
Отписка запрещает дальнейшие вызовы и ждёт активный обработчик; отписка из самого
обработчика возвращается сразу. Сам обработчик после этого завершает текущий вызов.
Остановку всего SDK выполняет хост вне обработчиков; перезагрузка из обработчика не поддерживается.

## Единая сериализация

`IDataSerializer` в C++ и C# используется для запросов, аргументов, результатов, ошибок
и событий. Оба канала на каждой стороне используют один экземпляр сериализатора.
В C# он также доступен через `context.Serializer`; обработчики методов типизированы:

```csharp
context.Messages.Register<RenameRequest, bool>("mymod.rename", request =>
{
    return Rename(request.Name);
});
```

Транспорт передаёт байты с длиной, включая нулевые байты. UTF-8 JSON — текущая реализация
по умолчанию, а не требование транспорта. Конвертация JSON находится в `json_serializer.cpp`
и `JsonDataSerializer.cs`. Нативные обработчики используют `DataValue` — дерево значений
на основе nlohmann; альтернативное кодирование должно поддерживать те же значения.
Названия методов/событий и идентификатор формата остаются UTF-8 строками.

Новый формат добавляется двумя реализациями `IDataSerializer`: C++ с `SerializerRegistration`
и C# с публичным конструктором без аргументов в сборке SDK. Они должны одинаково кодировать
поля протокола `method`, `arguments`, `event`, `data`, `success`, `value`, `error`, `code`,
`message`. Выбор — переменная окружения `F4FORGE_DATA_FORMAT` перед запуском игры
(по умолчанию `json`). SDK автоматически находит соответствующую реализацию;
неизвестный или несовпадающий формат отклоняется до загрузки модов.

Сериализаторы должны быть потокобезопасными, не удерживать входные буферы и сигнализировать
ошибку разбора исключением. Формат фиксируется при создании хоста.
