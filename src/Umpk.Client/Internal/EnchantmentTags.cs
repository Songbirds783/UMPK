using Umpk.Game.Registries;
using Umpk.Protocol.Java.Packets;

namespace Umpk.Client.Internal;

/// <summary>Turns the configuration/play-phase <c>update_tags</c> packet's <c>minecraft:enchantment</c> payload into a name-keyed tag map.</summary>
/// <remarks>
/// <para>The wire carries tag members as NUMERIC registry ids; the name they resolve to comes from the enchantment registry the registry-data packets installed moments earlier in the same configuration phase (vanilla sends registry_data before update_tags). An id that does not resolve against that registry is a gap, not a guess: dropping just that member keeps the rest of the tag truthful, while inventing a name would corrupt every consumer.</para>
/// <para>Only the enchantment registry's tags are kept — the packet also carries block, item, fluid and entity tags, and every one of those has no consumer here. Installing them would only move the drop one layer up. A payload without an enchantment arm, or one that resolves no tag at all, returns null so the install site leaves the previous state alone.</para>
/// </remarks>
internal static class EnchantmentTags
{
    /// <summary>Extracts the enchantment tag map, or null when the packet carried no usable enchantment payload. Tags whose members all failed to resolve come back as well — an empty tag is still a real answer ("nothing matches this tag").</summary>
    public static IReadOnlyDictionary<Identifier, IReadOnlyList<Identifier>>? FromPacket(
        ClientboundConfigUpdateTagsPacket packet, Registry<EnchantmentDefinition> enchantments)
    {
        ArgumentNullException.ThrowIfNull(packet);
        ArgumentNullException.ThrowIfNull(enchantments);

        TagRegistry? arm = null;
        foreach (TagRegistry registry in packet.Registries)
        {
            if (registry.Registry == RegistryIds.Enchantment)
            {
                arm = registry;
                break;
            }
        }
        if (arm is null)
            return null;

        var tags = new Dictionary<Identifier, IReadOnlyList<Identifier>>();
        foreach (TagEntry entry in arm.Tags)
        {
            List<Identifier> members = [];
            foreach (int id in entry.Ids)
            {
                if (enchantments.TryGetKey(id, out Identifier member))
                    members.Add(member);
            }
            tags[entry.TagId] = members;
        }
        return tags.Count == 0 ? null : tags;
    }
}
