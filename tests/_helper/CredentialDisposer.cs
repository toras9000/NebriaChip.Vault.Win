using System.Runtime.CompilerServices;
using NebriaChip.Vault.Win.Core;

namespace NebriaChip.Vault.Win.Tests;

internal class CredentialDisposer : IDisposable
{
    public CredentialDisposer()
    {
        this.adapter = new WinVaultAdapter();
        this.resources = new();
    }

    public string Track(CredType type, ReadOnlySpan<char> prefix, [CallerMemberName] string context = "")
    {
        var name = string.IsNullOrWhiteSpace(context) ? $"{prefix}-{DateTime.Now.Ticks}" : $"{prefix}-{context}-{DateTime.Now.Ticks}";
        this.resources.Add(new(type, name));
        return name;
    }

    public void Dispose()
    {
        foreach (var res in this.resources)
        {
            try
            {
                switch (res.Type)
                {
                case CredType.Generic: this.adapter.Delete(CredType.Generic, res.Name); break;
                default: break;
                }
            }
            catch { }
        }
        this.resources.Clear();
    }

    private record CredRes(CredType Type, string Name);

    private readonly WinVaultAdapter adapter;
    private readonly List<CredRes> resources;
}
