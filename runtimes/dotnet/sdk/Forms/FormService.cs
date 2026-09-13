using System.Buffers.Binary;
using System.Text;

namespace F4Forge.DotNet.Sdk;

public sealed class FormService
{
    private const string GetInfoEndpointName = "game.forms.get_info";
    private const string GetNameEndpointName = "game.forms.get_name";
    private const int InfoResponseSize = 12;
    private const int NameCapacity = 256;
    private const int NameResponseSize = 4 + NameCapacity;
    private readonly EndpointService _endpoints;
    private readonly GameThreadDispatcher _gameThread;
    private EndpointHandle _getInfo;
    private EndpointHandle _getName;

    internal FormService(EndpointService endpoints, GameThreadDispatcher gameThread)
    {
        _endpoints = endpoints;
        _gameThread = gameThread;
    }

    public F4ForgeResult Get(uint formId, out TESForm? form)
    {
        form = null;
        ResolveEndpoints();
        if (!_getInfo.IsValid || !_getName.IsValid) return F4ForgeResult.CapabilityUnavailable;

        Span<byte> request = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(request, formId);
        Span<byte> info = stackalloc byte[InfoResponseSize];
        var result = _endpoints.Invoke(_getInfo, request, info, out var responseSize);
        if (result != F4ForgeResult.Success) return result;
        if (responseSize != InfoResponseSize) return F4ForgeResult.InternalError;

        Span<byte> name = stackalloc byte[NameResponseSize];
        result = _endpoints.Invoke(_getName, request, name, out responseSize);
        if (result != F4ForgeResult.Success) return result;
        if (responseSize != NameResponseSize) return F4ForgeResult.InternalError;
        var nameLength = BinaryPrimitives.ReadUInt32LittleEndian(name);
        if (nameLength > NameCapacity) return F4ForgeResult.InternalError;

        form = new TESForm(
            BinaryPrimitives.ReadUInt32LittleEndian(info),
            (FormType)BinaryPrimitives.ReadUInt32LittleEndian(info[4..]),
            Encoding.UTF8.GetString(name.Slice(sizeof(uint), checked((int)nameLength))),
            BinaryPrimitives.ReadUInt32LittleEndian(info[8..]) != 0);
        return F4ForgeResult.Success;
    }

    public Task<FormQueryResult> GetAsync(uint formId, CancellationToken cancellationToken = default) =>
        _gameThread.InvokeAsync(() =>
        {
            var result = Get(formId, out var form);
            return new FormQueryResult(result, form);
        }, cancellationToken);

    private void ResolveEndpoints()
    {
        if (!_getInfo.IsValid) _getInfo = _endpoints.Resolve(GetInfoEndpointName);
        if (!_getName.IsValid) _getName = _endpoints.Resolve(GetNameEndpointName);
    }
}
