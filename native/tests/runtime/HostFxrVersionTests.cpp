#include "HostFxrVersion.h"

#include <cassert>

int main()
{
    const auto oldVersion = f4forge::dotnet::ParseHostFxrVersion(L"9.0.10");
    const auto newVersion = f4forge::dotnet::ParseHostFxrVersion(L"10.0.3");
    const auto preview = f4forge::dotnet::ParseHostFxrVersion(L"10.0.0-preview");
    const auto stable = f4forge::dotnet::ParseHostFxrVersion(L"10.0.0");
    assert(oldVersion && newVersion && preview && stable);
    assert(newVersion->major > oldVersion->major);
    assert(stable->stable && !preview->stable);
    assert(!f4forge::dotnet::ParseHostFxrVersion(L"invalid"));
    return 0;
}
