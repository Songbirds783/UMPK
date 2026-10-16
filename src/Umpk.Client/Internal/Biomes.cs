using Umpk.Game.Registries;
using Umpk.Protocol.Java.Packets;

namespace Umpk.Client.Internal;

/// <summary>Turns the server's own <c>minecraft:worldgen/biome</c> data into a <see cref="Registry{T}"/> of <see cref="BiomeDefinition"/>.</summary>
/// <remarks>
/// <para>One wire shape only: the 1.20.2+ configuration-phase <c>registry_data</c> packet, entries in network-id order. The registry has been synchronized data since datapack dimensions arrived (1.16.2); the 764/765 combined NBT blob carries it too, but that arm stays unwritten — the <see cref="Enchantments"/> precedent applies, where the blob eras never see this install.</para>
/// <para>What is read is deliberately only the identity. A chunk's biome palette carries bare network ids — meaningless without the name-to-id mapping and complete with it, which is the whole reason this registry has to be installed: <see cref="Umpk.Game.World.World.GetBiome"/> resolves those ids into named entries. The remaining definition fields (temperature, downfall, effects, spawners, carvers) have no consumer here and modelling them would only move the drop one layer up.</para>
/// <para>A server that agreed a known-pack set may send an entry with NO element (<see cref="PackedRegistryEntry.Data"/> null), meaning "you already have this one from a pack you told me you know" — on a vanilla-plus-datapack server this is most biome entries. Such an entry KEEPS its slot: the id is positional, so dropping one would shift every id after it and silently rename every biome past the gap.</para>
/// </remarks>
internal static class Biomes
{
    /// <summary>Builds a biome registry from one configuration-phase <c>registry_data</c> packet's entries. Network ids are the entry order, which is how vanilla assigns them. Returns null when nothing usable was present, so an empty packet leaves the existing table alone.</summary>
    public static Registry<BiomeDefinition>? FromPackedEntries(IReadOnlyList<PackedRegistryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var builder = new RegistryBuilder<BiomeDefinition>(RegistryIds.Biome, entries.Count);
        for (int id = 0; id < entries.Count; id++)
            builder.Add(id, entries[id].Id, new BiomeDefinition());

        return builder.Count == 0 ? null : builder.Build();
    }
}
