# NebriaChip.Vault.Win

[![NugetShield]][NugetPackage]

[NugetPackage]: https://www.nuget.org/packages/NebriaChip.Vault.Win
[NugetShield]: https://img.shields.io/nuget/v/NebriaChip.Vault.Win

This library is a simple wrapper around the Windows APIs for accessing the Windows Credential Store.

For more details, see the [Wincred API](https://learn.microsoft.com/en-us/windows/win32/api/wincred/).

## Examples

Saving and reading text using the Generic type.  

```csharp
using NebriaChip.Vault.Win;

var name = "NebriaChip_TestText";
var text = "SECRET-TEXT";

var adapter = new PersistVaultAdapter();
adapter.WriteGenericText(name, text);
var restore = adapter.ReadGenericText(name);
```

Saving and reading an object using the Generic type.  

```csharp
using NebriaChip.Vault.Win;

var name = "NebriaChip_TestObject";
var data = new MyData("text", 100);

var adapter = new PersistVaultAdapter();
adapter.WriteGenericObject(name, data);
var restore = adapter.ReadGenericObject<MyData>(name);

record MyData(string Name, int Value);
```

Accessing with additional low-level information.  

```csharp
using NebriaChip.Vault.Win.Core;

var name = "NebriaChip_TestAttribute";
var secret = new byte[]{ 0x01, 0x02, };

var adapter = new WinVaultAdapter();
var options = new WriteOptions(
    Persist: CredPersist.LocalMachine,
    Attributes: new Dictionary<string, byte[]>()
    {
        ["a"] = [0x34, 0x56],
        ["b"] = [0x78, 0x9A],
    }
);
adapter.Write(CredType.Generic, name, secret, options);

var readSecret = adapter.Read(CredType.Generic, name, out var extra);
```
