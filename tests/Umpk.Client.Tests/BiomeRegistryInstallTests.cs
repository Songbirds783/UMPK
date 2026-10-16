using Umpk.Client.Internal;
using Umpk.Client.Tests.Support;
using Umpk.Data.Java;
using Umpk.Game.Blocks;
using Umpk.Game.Registries;
using Umpk.Game.World;
using Umpk.Nbt;
using Umpk.Protocol.Java;
using Umpk.Protocol.Java.Packets;
using Xunit;

namespace Umpk.Client.Tests;

/// <summary>The configuration phase must install the server's <c>minecraft:worldgen/biome</c> table.</summary>
/// <remarks>
/// <para>Every chunk section carries a biome palette of bare network ids, and <see cref="World.GetBiome"/> is the consumer that resolves them into named entries. Before the install the session always built its world over the single-entry plains placeholder, so every position — loaded or not, vanilla or datapack — read as unbound, and nothing anywhere could say which biome a coordinate was in.</para>
/// <para>The entry shape mirrors the enchantment install: ids are positional (entry order), and a known-pack handshake elides most vanilla entries' payload, which is fine here because the definition is identity-only by design.</para>
/// </remarks>
public sealed class BiomeRegistryInstallTests
{
    /// <summary>1.21, keeping the harness on the same band as the enchantment install tests; the packet shape is identical across 764+.</summary>
    private const int Protocol = 767;

    private static readonly Identifier Plains = Identifier.Minecraft("plains");
    private static readonly Identifier Desert = Identifier.Minecraft("desert");
    private static readonly Identifier Jungle = Identifier.Minecraft("jungle");

    // The install

    /// <summary>Packed-entry order defines network ids. The expected ids are independent literals, so mapping every entry to one id cannot pass.</summary>
    [Fact]
    public async Task ConfigurationRegistryData_InstallsTheBiomeTable()
    {
        ApplierHarness harness = Build();

        await harness.ApplyAsync(RegistryData(
            Entry("minecraft:plains"),
            Entry("minecraft:desert"),
            Entry("minecraft:jungle")));

        Registry<BiomeDefinition> biomes = harness.State.Registries!.Biomes;
        Assert.Equal(3, biomes.Count);
        Assert.True(biomes.TryGetNetworkId(Plains, out int plains));
        Assert.Equal(0, plains);
        Assert.True(biomes.TryGetNetworkId(Desert, out int desert));
        Assert.Equal(1, desert);
        Assert.True(biomes.TryGetNetworkId(Jungle, out int jungle));
        Assert.Equal(2, jungle);
    }

    /// <summary>A server that agreed a known-pack set answers with entries carrying NO element at all — on a vanilla-plus-datapack server this is most biome entries. The identifier and its id are still the server's, so an elided entry must keep its slot rather than be dropped — dropping it would shift every id after it and silently rename every biome past the gap.</summary>
    [Fact]
    public async Task AnElidedEntry_KeepsItsSlotSoTheIdsAfterItDoNotShift()
    {
        ApplierHarness harness = Build();

        await harness.ApplyAsync(RegistryData(
            Elided("minecraft:plains"),
            Elided("minecraft:desert"),
            Entry("minecraft:jungle")));

        Registry<BiomeDefinition> biomes = harness.State.Registries!.Biomes;
        Assert.Equal(3, biomes.Count);
        Assert.True(biomes.TryGetNetworkId(Desert, out int desert));
        Assert.Equal(1, desert);
        Assert.True(biomes.TryGetNetworkId(Jungle, out int jungle));
        Assert.Equal(2, jungle);
    }

    // The consumer

    /// <summary>The proof the install has a consumer: a world built over the installed table resolves a chunk section's bare palette id into the named biome. The id is written straight into the section the way the chunk codec would leave it, at 4x4x4 biome-cell granularity.</summary>
    [Fact]
    public async Task AWorldBuiltAfterTheInstall_ResolvesChunkPaletteIdsToNamedBiomes()
    {
        ApplierHarness harness = Build();
        await harness.ApplyAsync(RegistryData(
            Entry("minecraft:plains"),
            Entry("minecraft:desert"),
            Entry("minecraft:jungle")));
        Assert.True(harness.State.Registries!.Biomes.TryGetNetworkId(Desert, out int desertId));

        var world = new World(
            WorldFactory.CreateDimension(new CommonWorldSetup("minecraft:overworld", 0), registries: null, Protocol),
            new RegistryBlockDataSource(JavaGameData.Registries(Protocol).Blocks, isLegacy: false),
            harness.State.Registries.Biomes);

        var pos = new Umpk.Geometry.BlockPos(100, 100, 100);
        Assert.True(JavaGameData.Registries(Protocol).Blocks.TryGetValue(
            Identifier.Minecraft("stone"), out BlockDefinition? stone));
        world.SetBlockStateId(pos, stone!.DefaultStateId);
        world.GetColumn(pos)!.GetSectionForY(pos.Y)!.SetBiomeId(
            (pos.X & 15) >> 2, (pos.Y & 15) >> 2, (pos.Z & 15) >> 2, desertId);

        RegistryEntry<BiomeDefinition> biome = world.GetBiome(pos);
        Assert.False(biome.IsDefault);
        Assert.Equal(Desert, biome.Id);
    }

    /// <summary>The control: a world over the pre-install placeholder cannot lie about being unbound — palette 0 resolves into the placeholder's single plains entry, which is precisely the old client's failure mode, the table says a NAME but the name is the client's guess rather than the server's. Only a position outside loaded chunks reads as unbound, which is exactly the tier the <c>GetBiomeIdAsync</c> guidance already tells consumers to treat separately.</summary>
    [Fact]
    public void AWorldOverThePlaceholder_NamesTheWrongBiomeAndAnUnloadedPosition_ReadsAsUnbound()
    {
        var world = new World(
            WorldFactory.CreateDimension(new CommonWorldSetup("minecraft:overworld", 0), registries: null, Protocol),
            new RegistryBlockDataSource(JavaGameData.Registries(Protocol).Blocks, isLegacy: false),
            WorldFactory.EmptyBiomes());

        var pos = new Umpk.Geometry.BlockPos(0, 100, 0);
        Assert.True(JavaGameData.Registries(Protocol).Blocks.TryGetValue(
            Identifier.Minecraft("stone"), out BlockDefinition? stone));
        world.SetBlockStateId(pos, stone!.DefaultStateId);

        // Palette 0 against the placeholder table: a name, but the placeholder's, not the server's.
        RegistryEntry<BiomeDefinition> biome = world.GetBiome(pos);
        Assert.False(biome.IsDefault);
        Assert.Equal(Plains, biome.Id);

        Assert.True(world.GetBiome(new Umpk.Geometry.BlockPos(10_000, 100, 10_000)).IsDefault);
    }

    // Support

    private static ClientboundConfigRegistryDataPacket RegistryData(params PackedRegistryEntry[] entries) =>
        new(RegistryIds.Biome, entries);

    private static PackedRegistryEntry Entry(string id) =>
        new(Identifier.Parse(id), new NbtCompound());

    private static PackedRegistryEntry Elided(string id) => new(Identifier.Parse(id), null);

    private static ApplierHarness Build()
    {
        Assert.True(JavaVersions.TryGetByProtocol(Protocol, out JavaVersion? version));
        var harness = new ApplierHarness(version!, new ClientFeatures { Terrain = true });
        harness.State.Registries = JavaGameData.Registries(Protocol);
        return harness;
    }
}
