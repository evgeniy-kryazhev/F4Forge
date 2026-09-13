namespace F4Forge.DotNet.Sdk;

public sealed class TESForm
{
    internal TESForm(uint formId, FormType formType, string name, bool isValid)
    {
        FormId = formId;
        FormType = formType;
        Name = name;
        IsValid = isValid;
    }

    public uint FormId { get; }
    public FormType FormType { get; }
    public string Name { get; }
    public bool IsValid { get; }
}

public readonly record struct FormQueryResult(F4ForgeResult Result, TESForm? Form);
