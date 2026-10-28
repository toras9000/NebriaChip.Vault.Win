# NebriaChip.Vault.Win

[![NugetShield]][NugetPackage]

[NugetPackage]: https://www.nuget.org/packages/NebriaChip.Vault.Win
[NugetShield]: https://img.shields.io/nuget/v/NebriaChip.Vault.Win

このライブラリは、Windows 資格情報ストアにアクセスする Windows API の単純なラッパーです。  

詳しくは [Wincred API](https://learn.microsoft.com/en-us/windows/win32/api/wincred/) を参照してください。  

## Examples

Generic タイプでテキストを保存・読み取り。  

```csharp
using NebriaChip.Vault.Win;

var name = "NebriaChip_TestText";
var text = "SECRET-TEXT";

var adapter = new PersistVaultAdapter();
adapter.WriteGenericText(name, text);
var restore = adapter.ReadGenericText(name);
```

Generic タイプでオブジェクトの保存・読み取り。  

```csharp
using NebriaChip.Vault.Win;

var name = "NebriaChip_TestObject";
var data = new MyData("text", 100);

var adapter = new PersistVaultAdapter();
adapter.WriteGenericObject(name, data);
var restore = adapter.ReadGenericObject<MyData>(name);

record MyData(string Name, int Value);
```

より低レイヤの追加の情報を利用したアクセス。  

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
