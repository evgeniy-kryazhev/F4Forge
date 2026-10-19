# Динамический Papyrus-мост

`context.Papyrus` вызывает функции и свойства по именам. Их типы берутся из метаданных VM: отдельный класс или ABI-привязка для каждого метода чужого мода не нужны. Используется существующий канал и его `IDataSerializer`; interop-экспорты и центральная таблица методов не меняются.

## Глобальные функции и UI

```csharp
var result = await context.Papyrus.CallStaticAsync(
    "PrismaUI", "CreateView", ["my-overlay", "my-mod/index.html"], cancellationToken);
if (result.Kind == "bool" && result.Boolean)
{
    await context.Papyrus.CallStaticAsync(
        "PrismaUI", "Show", ["my-overlay"], cancellationToken);
}
```

Это опубликованные функции Papyrus API PrismaUI; сам мод должен быть установлен и зарегистрировать свои native functions. F4Forge не включает PrismaUI и его рендерер.

Тот же механизм вызывает функции UI, зарегистрированные F4SE:

```csharp
var isOpen = await context.Papyrus.CallStaticAsync(
    "UI", "IsMenuOpen", ["HUDMenu"], cancellationToken);

var result = await context.Papyrus.CallStaticAsync(
    "UI", "Invoke", ["HUDMenu", "root.myFunction", new object?[] { 42, "text" }], cancellationToken);
```

`root.myFunction` — пример пути: реальная функция должна существовать в открытом меню. Это путь C# → Papyrus VM → native function F4SE → Scaleform. Мост не угадывает внутренние функции FallUI и не делает чистый C++ API произвольной DLL доступным по именам.

## Методы и свойства

```csharp
using F4Forge.Net.Objects;
using F4Forge.Net.Objects.Queries;

var quest = context.Objects.Query(new FormByPlugin<Form>("Example.esp", 0x123));
if (quest is not null)
{
    var script = context.Papyrus.Object(quest, "ExampleQuestScript");
    var count = await context.Papyrus.CallAsync(script, "GetCount", cancellationToken: cancellationToken);
    var property = await context.Papyrus.GetPropertyAsync(script, "Counter", cancellationToken);
    await context.Papyrus.SetPropertyAsync(script, "Counter", 10, cancellationToken);
}
```

Получение формы через `Objects.Query` требует игрового потока; после `await` используйте `GameThread.RunAsync` для этого шага. Сам Papyrus-клиент ставит отправку и преобразование результатов в очередь F4SE и может вызываться из любого управляемого потока. Пользовательские скрипты должны уже быть привязаны к форме; мост не прикрепляет их автоматически. Нативное представление формы, например `Actor` или `Quest`, может быть создано стандартным механизмом VM.

Свойства читаются и изменяются через VM с сохранением getter/setter. Прямая запись внутренних переменных и наблюдение чужих custom events в эту поставку не входят. Для America Rising 2 имена файлов, форм, скриптов и функций нужно взять из ресурсов конкретной установленной версии; специальных имён AR2 в ядре нет.

## Значения

Аргументы принимают `null`, `bool`, `int`, конечные `float`/`double`, `string`, формы текущего мода, `PapyrusObject`, массивы и `PapyrusStruct`. Вложенные значения ограничены глубиной 32. Типы массивов выводятся из сигнатуры функции; внутри `Var` новый массив без явной сигнатуры передаётся как `Var[]`. Возвращённый `PapyrusValue` сохраняет тип элементов массива в `TypeName`, включая пустые массивы.

```csharp
using F4Forge.Net.Papyrus;

var data = new PapyrusStruct("UI#MenuData", new Dictionary<string, object?>
{
    ["depth"] = 6
});
```

Используйте VM-имя структуры с `#`, поля проверяются по метаданным. Пропущенные поля остаются со значениями, созданными VM. Пропущенные аргументы функции передаются механизму вызова VM; отсутствие обязательного аргумента должно привести к отказу VM, а не к автоматической подстановке F4Forge.

Результат — `PapyrusValue`: поле `Kind` определяет заполненное поле `Boolean`, `Integer`, `Number`, `Text`, `Reference`, `Items` или `Fields`. `none` также представляет результат функции без возвращаемого значения. Возвращённый `Reference` можно передать в последующие вызовы клиента; это непрозрачный токен, а не адрес памяти. Токены принадлежат одному клиенту и сессии, освобождаются при остановке мода или смене сохранения. В одном клиенте хранится не более 4096 возвращённых ссылок.

## Время жизни и ошибки

Ожидание каждого вызова ограничено 30 секундами. `CancellationToken` отменяет ожидание и ещё не запущенную работу. Уже исполняющийся Papyrus-стек не прерывается; его поздний результат отбрасывается. Клиент не блокирует игровой поток, его продолжения выполняются асинхронно.

Вызовы ожидают готовых игровых данных и завершённой загрузки сохранения. При неизвестном скрипте, функции, свойстве, несовместимом аргументе, отказе VM или устаревшей ссылке задача завершается с ошибкой. Некоторые ошибки исполнения скрипта VM сообщает только в своём журнале и не вызывает callback: тогда действует тайм-аут. Ошибки VM-моста передаются как `MessageException` с кодом `papyrus_failed`; отклонение запроса самим каналом сохраняет код канала.

Остановка мода синхронно закрывает его нативную область. Управляемые задачи остаются под наблюдением существующего времени жизни до завершения. У нативной очереди и callback VM нет указателей на управляемый мод. Буферы копируются до возврата callback; используются тот же сериализатор и общий предел транспортного сообщения.

## Необязательная C#-обёртка

Обёртка — обычная библиотека мода, вызывающая этот же API. Например, метод `ShowAsync(name)` может делегировать `CallStaticAsync("PrismaUI", "Show", [name])`. Новых методов, типов запросов или зависимостей в F4Forge для этого не требуется. Низкоуровневый путь остаётся доступен без обёртки.

## Проверки

```powershell
xmake -P native/tests/papyrus -j 2
xmake run -P native/tests/papyrus F4Forge.Papyrus.Tests
dotnet test tests/dotnet/F4Forge.Net.Tests/F4Forge.Net.Tests.csproj -c Release
```

Изолированные тесты проверяют транспорт, корреляцию, ошибки, отмену, закрытие, смену сессии, повторные callback и альтернативную сериализацию. Модуль компилируется с реальными интерфейсами CommonLib, но изолированные тесты не запускают VM Fallout 4.

**Интеграция с работающей VM пока не проверена в игре.** Для ручной проверки скомпилируйте `tests/papyrus/F4ForgeBridgeProbe.psc` штатным компилятором Creation Kit в отдельный тестовый мод. Статические функции доступны без привязки к объекту. Для проверки свойств и методов привяжите скрипт к тестовому квесту. Проверьте `Add`, `Echo`, массивы, структуры, latent-вызов `WaitAndReturn`, свойства и отказ после загрузки сохранения. Скрипт не зависит от API F4Forge и не требует изменения чужих модов.
