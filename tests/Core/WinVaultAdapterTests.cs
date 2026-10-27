using AwesomeAssertions;
using NebriaChip.Vault.Win.Tests;

namespace NebriaChip.Vault.Win.Core.Tests;

[TestClass]
public sealed class WinVaultAdapterTests
{
    [TestMethod]
    public void WriteRead()
    {
        using var disposer = new CredentialDisposer();
        var name = disposer.Track(CredType.Generic, "NebriaChip");

        var adapter = new WinVaultAdapter();
        adapter.Write(CredType.Generic, name, [0x01, 0x02]).Should().BeTrue();
        adapter.Read(CredType.Generic, name).Should().Equal([0x01, 0x02]);
    }

    [TestMethod]
    public void WriteRead_Extra()
    {
        using var disposer = new CredentialDisposer();
        var name = disposer.Track(CredType.Generic, "NebriaChip");

        var adapter = new WinVaultAdapter();

        var options = new WriteOptions(
            UserName: "usr-nm",
            Comment: "cmnt",
            TargetAlias: "alias",
            Attributes: new Dictionary<string, byte[]>() { ["a"] = [0x34, 0x56], ["b"] = [0x78, 0x9A], }
        );
        adapter.Write(CredType.Generic, name, [0x01, 0x02], options).Should().BeTrue();

        var readback = adapter.Read(CredType.Generic, name, out var extra);
        readback.Should().Equal([0x01, 0x02]);
        extra.Should().NotBeNull();
        extra.UserName.Should().Be(options.UserName);
        extra.Comment.Should().Be(options.Comment);
        extra.TargetAlias.Should().Be(options.TargetAlias);
        extra.Attributes.Should().BeEquivalentTo(options.Attributes);
    }

    [TestMethod]
    public void WriteRead_Update()
    {
        using var disposer = new CredentialDisposer();
        var name = disposer.Track(CredType.Generic, "NebriaChip");

        var adapter = new WinVaultAdapter();

        var options1 = new WriteOptions(
            UserName: "usr-nm",
            Comment: "cmnt",
            TargetAlias: "alias",
            Attributes: new Dictionary<string, byte[]>() { ["a"] = [0x34, 0x56], ["b"] = [0x78, 0x9A], }
        );
        adapter.Write(CredType.Generic, name, [0x01, 0x02], options1).Should().BeTrue();

        var options2 = new WriteOptions(
            UserName: "usr-nm2",
            Comment: "cmnt2",
            TargetAlias: "alias2",
            Attributes: new Dictionary<string, byte[]>() { ["a"] = [0x11, 0x22], ["c"] = [0x78, 0x9A], }
        );
        adapter.Write(CredType.Generic, name, options2).Should().BeTrue();

        var readback = adapter.Read(CredType.Generic, name, out var extra);
        readback.Should().Equal([0x01, 0x02]);
        extra.Should().NotBeNull();
        extra.UserName.Should().Be(options2.UserName);
        extra.Comment.Should().Be(options2.Comment);
        extra.TargetAlias.Should().Be(options2.TargetAlias);
        extra.Attributes.Should().BeEquivalentTo(options2.Attributes);
    }

    [TestMethod]
    public void Delete()
    {
        using var disposer = new CredentialDisposer();
        var name = disposer.Track(CredType.Generic, "NebriaChip");

        var adapter = new WinVaultAdapter();

        adapter.Write(CredType.Generic, name, [0x01, 0x02]).Should().BeTrue();

        adapter.Read(CredType.Generic, name).Should().Equal([0x01, 0x02]);

        adapter.Delete(CredType.Generic, name).Should().BeTrue();

        adapter.Read(CredType.Generic, name).Should().BeNull();
    }

    [TestMethod]
    public void Enumerate()
    {
        using var disposer = new CredentialDisposer();
        var name = disposer.Track(CredType.Generic, "NebriaChip");

        var adapter = new WinVaultAdapter();
        adapter.Write(CredType.Generic, name, [0x01, 0x02]).Should().BeTrue();

        adapter.Enumerate("NebriaChip*").Should().Contain(i => i.Type == CredType.Generic && i.Name == name);
    }
}
