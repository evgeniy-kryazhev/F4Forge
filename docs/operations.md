# Типизированные запросы и команды

SDK разделяет чтение данных и изменение состояния:

- `IQuery<TResult>` выполняется через `context.Messages.Query(query)`.
- `ICommand<TResult>` выполняется через `context.Messages.Execute(command)`.
- `IFormQuery<TForm>` расширяет запрос `FormSnapshot?`; `context.Objects.Query(query)` дополнительно создаёт типизированное представление объекта.

Общий `IOperation` задаёт `MethodName`, `Arguments` и необязательный метод `Validate(IMessageChannel)`. Исполнитель проверяет аргументы и область мода, затем вызывает существующий канал. Сериализуются только `Arguments`. Тип результата выводится компилятором из контракта; нет рефлексии, таблицы методов или новых interop-экспортов.

```csharp
using F4Forge.Net.Actors.Commands;
using F4Forge.Net.Actors.Queries;
using F4Forge.Net.Objects;
using F4Forge.Net.Objects.Queries;
using F4Forge.Net.Operations;
using F4Forge.Net.Player.Queries;

var player = context.Objects.Query(new GetPlayer());
var health = context.Objects.Query(new FormByEditorId<ActorValue>("Health"));
if (player is not null && health is not null)
{
    float current = context.Messages.Query(new GetActorValue(player, health));
    ReferenceSnapshot snapshot = context.Messages.Query(new GetReferenceSnapshot(player));
    bool applied = context.Messages.Execute(new SetActorBaseValue(player, health, 150));
}
```

`Execute` возвращает ответ нативного обработчика. Значение `false` не теряется и не превращается автоматически в исключение. Для операции с другим ответом укажите соответствующий `TResult`, например собственную запись с результатом изменения. Исключения проверки аргументов возникают до входа в транспорт. Ошибки нативного ответа передаются через `MessageException`; локальные ошибки сериализатора сохраняют свой исходный тип. Общий исполнитель не перехватывает и не заменяет эти исключения.

## Добавление функции

Для произвольных данных достаточно записи в модуле функции:

```csharp
using F4Forge.Net.Operations;

public sealed record GetInventoryCount(uint OwnerFormId) : IQuery<int>
{
    public string MethodName => "mymod.inventory.count";
    public object Arguments => new { OwnerFormId };
}

public sealed record SetInventoryLimit(int Limit) : ICommand<bool>
{
    public string MethodName => "mymod.inventory.set_limit";
    public object Arguments => new { Limit };
}
```

Вызовы: `context.Messages.Query(new GetInventoryCount(ownerId))` и `context.Messages.Execute(new SetInventoryLimit(100))`. Обработчики этих методов нужно отдельно реализовать и зарегистрировать в соответствующем модуле. Контракты SDK описывают вызов, но не создают игровую функцию.

Операции с существующим объектом наследуют `Objects.Operations.FormOperation`: он проверяет принадлежность объекта каналу мода. Операции со значениями актёра используют `Actors.Operations.ActorValueOperation`, который также проверяет общую сессию актёра и определения значения. Нативный обработчик повторно разрешает ID, проверяет поколение сессии, фактический тип и готовность мира. Не сериализуйте `Form`, канал или весь объект операции: передавайте только необходимые ID, сессию и параметры через `Arguments`.

Существующие операции размещены по назначению:

| Модуль | Запросы | Команды |
| --- | --- | --- |
| `Player/Queries` | `GetPlayer` | — |
| `Objects/Queries` | `FormById`, `FormByPlugin`, `FormByEditorId`, `DescribeForm`, `GetReferenceSnapshot` | — |
| `Actors` | `GetActorValue` | `SetActorBaseValue` |
| `Diagnostics/Queries` | `GetDiagnosticOperations`, `GetHookStatuses` | — |
| `Logging/Commands` | — | `WriteLog` |

Удобные методы `context.Diagnostics.GetOperations`, `GetHooks` и `LogInfo`/`LogWarning`/`LogError` используют эти же контракты. Низкоуровневый `Messages.Call<TResult>(name, arguments)` остаётся доступен для динамических методов и служебных протоколов регистрации, диспетчеризации и диагностики самого транспорта.

## Игровой поток и время жизни

Оба вызова синхронные и выполняются в потоке вызывающего кода. Они не ставят работу в очередь, не ожидают другие потоки и не добавляют асинхронные консольные команды. После `await` используйте существующий диспетчер:

```csharp
bool applied = await context.GameThread.RunAsync(() =>
{
    var player = context.Objects.Query(new GetPlayer());
    var health = context.Objects.Query(new FormByEditorId<ActorValue>("Health"));
    if (player is null || health is null)
    {
        return false;
    }

    return context.Messages.Execute(new SetActorBaseValue(player, health, 150));
}, cancellationToken);
```

Отмена до начала блока предотвращает его выполнение; начатая синхронная операция принудительно не прерывается. Канал мода запрещает вызовы после остановки. Контракт может хранить безопасное управляемое представление объекта, но не нативный указатель. После загрузки сохранения разрешайте объекты заново.
