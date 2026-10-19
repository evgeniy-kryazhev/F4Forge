using System.Collections;
using F4Forge.Net.Messaging;
using F4Forge.Net.Objects;

namespace F4Forge.Net.Papyrus;

/// <summary>A script identity. Tokens are opaque, scoped to one mod and one game session.</summary>
/// <param name="FormId">Form-backed target, or zero for an opaque VM object.</param>
/// <param name="Session">Game session generation.</param>
/// <param name="ScriptName">Attached script type.</param>
/// <param name="Token">Native registry token for a returned VM object.</param>
public sealed record PapyrusObject(uint FormId, ulong Session, string ScriptName, string? Token = null);

/// <summary>A named Papyrus struct, with fields converted using VM metadata.</summary>
/// <param name="TypeName">VM-qualified struct name, such as UI#MenuData.</param>
/// <param name="Fields">Values keyed by script field name.</param>
public sealed record PapyrusStruct(string TypeName, IReadOnlyDictionary<string, object?> Fields);

/// <summary>A copied dynamic result. Only the fields belonging to Kind are populated.</summary>
public sealed record PapyrusValue
{
    /// <summary>none, bool, int, float, string, object, array or struct.</summary>
    public string Kind { get; init; } = "none";
    /// <summary>Boolean data.</summary>
    public bool Boolean { get; init; }
    /// <summary>Signed 32-bit integer data.</summary>
    public int Integer { get; init; }
    /// <summary>Finite 32-bit floating-point data.</summary>
    public float Number { get; init; }
    /// <summary>String data.</summary>
    public string? Text { get; init; }
    /// <summary>A scoped script identity.</summary>
    public PapyrusObject? Reference { get; init; }
    /// <summary>Copied array elements.</summary>
    public PapyrusValue[]? Items { get; init; }
    /// <summary>Qualified struct name or array element type.</summary>
    public string? TypeName { get; init; }
    /// <summary>Copied struct fields.</summary>
    public Dictionary<string, PapyrusValue>? Fields { get; init; }

    internal static PapyrusValue From(object? value, IMessageChannel messages, int depth = 0)
    {
        if (depth > 32)
        {
            throw new ArgumentException("Papyrus values exceed the nesting limit.", nameof(value));
        }
        switch (value)
        {
            case null:
                return new();
            case PapyrusValue copied:
                return copied;
            case bool boolean:
                return new() { Kind = "bool", Boolean = boolean };
            case int integer:
                return new() { Kind = "int", Integer = integer };
            case float number when float.IsFinite(number):
                return new() { Kind = "float", Number = number };
            case double number when double.IsFinite(number) && Math.Abs(number) <= float.MaxValue:
                return new() { Kind = "float", Number = (float)number };
            case string text:
                return new() { Kind = "string", Text = text };
            case Form form:
                if (!ReferenceEquals(form.Messages, messages))
                {
                    throw new ArgumentException("The form belongs to another mod scope.", nameof(value));
                }
                return new() { Kind = "object", Reference = new(form.FormId, form.Session, "") };
            case PapyrusObject reference:
                return new() { Kind = "object", Reference = reference };
            case PapyrusStruct structure:
                return new()
                {
                    Kind = "struct", TypeName = structure.TypeName,
                    Fields = structure.Fields.ToDictionary(field => field.Key,
                        field => From(field.Value, messages, depth + 1))
                };
            case IEnumerable sequence:
                var items = new List<PapyrusValue>();
                foreach (var item in sequence)
                {
                    if (items.Count >= 65536)
                    {
                        throw new ArgumentException("Papyrus array exceeds the element limit.", nameof(value));
                    }
                    items.Add(From(item, messages, depth + 1));
                }
                return new() { Kind = "array", Items = items.ToArray() };
            default:
                throw new ArgumentException($"Unsupported Papyrus argument type: {value.GetType().Name}.", nameof(value));
        }
    }
}
