using Umpk.Game.Registries;
using Umpk.Nbt;
using Umpk.Protocol.Java.Packets;

namespace Umpk.Client.Internal;

/// <summary>Turns the server's own <c>minecraft:enchantment</c> data into a <see cref="Registry{T}"/> of <see cref="EnchantmentDefinition"/>.</summary>
/// <remarks>
/// <para>One wire shape only: the 1.20.5+ configuration-phase <c>registry_data</c> packet, entries in network-id order. The registry became synchronized data in 1.21, while 1.20.6 does not carry it. So there is no 764/765 NBT-blob arm and no join-game arm to write: on those protocols the server never sends this registry, and <c>JavaGameData</c>'s generated identity table serves 477-766 instead.</para>
/// <para>What is read is the identity plus the fields an automation client can act on without implementing enchantment effects: <c>max_level</c>, <c>anvil_cost</c>, the two enchanting-table cost curves, the raw <c>exclusive_set</c> holder-set references, <c>weight</c> and the display-name string extracted from <c>description</c> (its translate key or literal text). Supported items, slots and the effect tree stay unread — no consumer here, and modelling them would only move the drop one layer up.</para>
/// <para>From 1.20.5 a server that agreed a known-pack set may send an entry with NO element (<see cref="PackedRegistryEntry.Data"/> null), meaning "you already have this one from a pack you told me you know". Such an entry KEEPS its slot: the id is positional, so dropping one would shift every id after it and silently rename every enchantment past the gap. It falls back to <see cref="EnchantmentDefinition"/>'s declared defaults rather than to invented values.</para>
/// </remarks>
internal static class Enchantments
{
    /// <summary>The top-level element key carrying the maximum enchantment level.</summary>
    private const string MaxLevelKey = "max_level";

    /// <summary>The top-level element key carrying the anvil merge cost.</summary>
    private const string AnvilCostKey = "anvil_cost";

    /// <summary>The top-level element key carrying the enchanting table's minimum cost curve.</summary>
    private const string MinCostKey = "min_cost";

    /// <summary>The top-level element key carrying the enchanting table's maximum cost curve.</summary>
    private const string MaxCostKey = "max_cost";

    /// <summary>The top-level element key carrying the conflicting-enchantment holder set.</summary>
    private const string ExclusiveSetKey = "exclusive_set";

    /// <summary>The top-level element key carrying the weighted-random draw weight.</summary>
    private const string WeightKey = "weight";

    /// <summary>The top-level element key carrying the display-name text component.</summary>
    private const string DescriptionKey = "description";

    /// <summary>The cost-curve member naming the level-I cost.</summary>
    private const string CostBaseKey = "base";

    /// <summary>The cost-curve member naming the per-level increment above level I.</summary>
    private const string CostPerLevelKey = "per_level_above_first";

    /// <summary>Builds an enchantment registry from one configuration-phase <c>registry_data</c> packet's entries. Network ids are the entry order, which is how vanilla assigns them. Returns null when nothing usable was present, so an empty packet leaves the existing table alone.</summary>
    public static Registry<EnchantmentDefinition>? FromPackedEntries(IReadOnlyList<PackedRegistryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var builder = new RegistryBuilder<EnchantmentDefinition>(RegistryIds.Enchantment, entries.Count);
        for (int id = 0; id < entries.Count; id++)
        {
            PackedRegistryEntry entry = entries[id];
            builder.Add(id, entry.Id, ReadElement(entry.Data));
        }

        return builder.Count == 0 ? null : builder.Build();
    }

    /// <summary>Reads one enchantment definition compound, falling back to <see cref="EnchantmentDefinition"/>'s defaults for any member the element omits.</summary>
    private static EnchantmentDefinition ReadElement(NbtTag? tag)
    {
        if (tag is not NbtCompound element)
            return new EnchantmentDefinition();

        return new EnchantmentDefinition(
            MaxLevel: element.TryGet(MaxLevelKey, out NbtNumeric? maxLevel) ? Math.Max(1, maxLevel.AsInt) : 1,
            AnvilCost: element.TryGet(AnvilCostKey, out NbtNumeric? anvilCost) ? Math.Max(0, anvilCost.AsInt) : 0,
            MinCost: ReadCost(element, MinCostKey),
            MaxCost: ReadCost(element, MaxCostKey),
            ExclusiveSet: ReadHolderSet(element, ExclusiveSetKey),
            Weight: element.TryGet(WeightKey, out NbtNumeric? weight) ? Math.Clamp(weight.AsInt, 1, 1024) : 1,
            Description: ReadDescription(element));
    }

    /// <summary>Reads one cost curve compound, or null when the element omitted it or shaped it unrecognizably.</summary>
    private static EnchantmentCost? ReadCost(NbtCompound element, string key)
    {
        if (!element.TryGet(key, out NbtTag? tag) || tag is not NbtCompound cost)
            return null;

        int baseCost = cost.TryGet(CostBaseKey, out NbtNumeric? baseTag) ? Math.Max(0, baseTag.AsInt) : 0;
        int perLevel = cost.TryGet(CostPerLevelKey, out NbtNumeric? perLevelTag) ? Math.Max(0, perLevelTag.AsInt) : 0;
        return new EnchantmentCost(baseCost, perLevel);
    }

    /// <summary>Reads one holder set — a single <c>#tag</c> reference string or a list of plain ids — verbatim, or null when the element omitted it. Tags are returned with their <c>#</c> prefix intact; expanding them is the tags update's job, not this parser's.</summary>
    private static IReadOnlyList<string>? ReadHolderSet(NbtCompound element, string key)
    {
        if (!element.TryGet(key, out NbtTag? tag))
            return null;

        return tag switch
        {
            NbtString reference => new[] { reference.Value },
            NbtList ids => ids.OfType<NbtString>().Select(id => id.Value).ToArray(),
            _ => null,
        };
    }

    /// <summary>Reads the display-name text component down to the one string an automation client can use: the <c>translate</c> key when the component is one (every vanilla enchantment), the literal <c>text</c> when it is plain, null otherwise. Styles, siblings and the rest of the component tree have no consumer here.</summary>
    private static string? ReadDescription(NbtCompound element)
    {
        if (!element.TryGet(DescriptionKey, out NbtTag? tag) || tag is not NbtCompound description)
            return null;

        if (description.TryGet("translate", out NbtTag? translate) && translate is NbtString key)
            return key.Value;
        if (description.TryGet("text", out NbtTag? text) && text is NbtString literal)
            return literal.Value;
        return null;
    }
}
