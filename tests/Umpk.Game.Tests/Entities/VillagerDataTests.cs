using Umpk.Game.Entities;
using Xunit;

namespace Umpk.Game.Tests.Entities;

public sealed class VillagerDataTests
{
    [Fact]
    public void ProfessionNames_MatchTheVanillaRegistrationOrder()
    {
        Assert.Equal("minecraft:none", VillagerDataNames.ProfessionName(0));
        Assert.Equal("minecraft:armorer", VillagerDataNames.ProfessionName(1));
        Assert.Equal("minecraft:farmer", VillagerDataNames.ProfessionName(5));
        Assert.Equal("minecraft:librarian", VillagerDataNames.ProfessionName(9));
        Assert.Equal("minecraft:nitwit", VillagerDataNames.ProfessionName(11));
        Assert.Equal("minecraft:toolsmith", VillagerDataNames.ProfessionName(13));
        Assert.Equal("minecraft:weaponsmith", VillagerDataNames.ProfessionName(14));
        Assert.Null(VillagerDataNames.ProfessionName(15));
        Assert.Null(VillagerDataNames.ProfessionName(-1));
    }

    [Fact]
    public void TypeNames_MatchTheVanillaRegistrationOrder()
    {
        Assert.Equal("minecraft:desert", VillagerDataNames.TypeName(0));
        Assert.Equal("minecraft:plains", VillagerDataNames.TypeName(2));
        Assert.Equal("minecraft:taiga", VillagerDataNames.TypeName(6));
        Assert.Null(VillagerDataNames.TypeName(7));
    }

    [Fact]
    public void Metadata_VillagerDataIsFoundByKind_RegardlessOfIndex()
    {
        var metadata = new EntityMetadata(EntityTestFixtures.Zombie);
        metadata.Set(3, MetadataValue.Boolean(true)); // a decoy entry at some index
        metadata.Set(17, MetadataValue.VillagerData(new VillagerData(Type: 2, Profession: 5, Level: 3)));

        Assert.True(metadata.TryGetVillagerData(out VillagerData data));
        Assert.Equal(2, data.Type);
        Assert.Equal(5, data.Profession);
        Assert.Equal(3, data.Level);
    }

    [Fact]
    public void Metadata_WithoutAVillagerEntry_AnswersFalse()
    {
        var metadata = new EntityMetadata(EntityTestFixtures.Zombie);
        metadata.Set(0, MetadataValue.Byte(1));

        Assert.False(metadata.TryGetVillagerData(out VillagerData data));
        Assert.Equal(default, data);
    }
}
