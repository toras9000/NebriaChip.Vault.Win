using AwesomeAssertions;
using Lestaly;
using NebriaChip.Vault.Win.Core;

namespace NebriaChip.Vault.Win.Tests;

[TestClass]
public class PersistVaultAdapterTests
{
    [TestMethod]
    public void WriteReadGenericData()
    {
        using var disposer = new CredentialDisposer();
        var name = disposer.Track(CredType.Generic, "NebriaChip");

        var adapter = new PersistVaultAdapter();
        adapter.WriteGenericData(name, [0x12, 0x34, 0x45]).Should().BeTrue();
        adapter.ReadGenericData(name).Should().Equal([0x12, 0x34, 0x45]);
    }

    [TestMethod]
    public void WriteReadGenericText()
    {
        using var disposer = new CredentialDisposer();
        var name = disposer.Track(CredType.Generic, "NebriaChip");

        var adapter = new PersistVaultAdapter();
        adapter.WriteGenericText(name, "あいうえお").Should().BeTrue();
        adapter.ReadGenericText(name).Should().Be("あいうえお");
    }

    [TestMethod]
    public void WriteReadGenericObject()
    {
        using var disposer = new CredentialDisposer();
        var name = disposer.Track(CredType.Generic, "NebriaChip");

        var data = new TestData(
            Text: "あいうえお",
            Number: 123,
            Directory: new DirectoryInfo(".")
        );

        var adapter = new PersistVaultAdapter();
        adapter.WriteGenericObject(name, data, RoughJson.Options).Should().BeTrue();
        adapter.ReadGenericObject<TestData>(name, RoughJson.Options).Should().BeEquivalentTo(data, options => options.Excluding(d => d.Directory.Parent).Excluding(d => d.Directory.Root));
    }

    record TestData(string Text, int Number, DirectoryInfo Directory);
}
