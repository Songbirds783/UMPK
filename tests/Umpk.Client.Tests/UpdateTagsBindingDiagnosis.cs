using Umpk.Client.Internal;
using Umpk.Data.Java;
using Umpk.Game.Registries;
using Umpk.Protocol.Java;
using Umpk.Protocol.Java.Packets;
using Xunit;

namespace Umpk.Client.Tests;

/// <summary>Diagnoses the update_tags binding on the real 777 descriptor: the capture from purrland.cn proves the server sends it at configuration wire 14, yet the client never dispatched it to the applier. The classic silent-drop cause is a marker slot — this test makes the slot state visible instead of guessed.</summary>
public sealed class UpdateTagsBindingDiagnosis
{
    [Fact]
    public void The777ConfigSlot_ShowsWhatWire14IsBoundTo()
    {
        Assert.True(JavaVersions.TryGetByProtocol(777, out JavaVersion? version), "unknown protocol 777");
        Assert.True(
            version!.Protocol.TryGetRegistry(ProtocolPhase.Configuration, PacketFlow.Clientbound, out PhaseRegistry registry),
            "no configuration/clientbound registry at 777");

        var id = Identifier.Minecraft("update_tags");
        bool found = false;
        foreach ((int wireId, PacketType type) in registry.Packets)
        {
            if (type.Id != id)
                continue;

            found = true;
            bool bound = registry.TryGetInbound(wireId, out BoundPacketCodec? boundCodec);
            Assert.True(bound, $"update_tags wire {wireId}: no inbound codec registered");
            Assert.True(boundCodec!.IsImplemented,
                $"update_tags wire {wireId} IS A MARKER at 777 (type {type.GetType().Name}) — the live session silently drops the frame, exactly matching the missing-dispatch symptom");
        }

        Assert.True(found, "update_tags is not registered in the 777 configuration/clientbound registry at all");
    }

    /// <summary>The enchantment arm installs with members resolved from numeric ids via the synced registry; other registries' tags and unresolvable members drop.</summary>
    [Fact]
    public void FromPacket_InstallsTheEnchantmentArm()
    {
        var builder = new RegistryBuilder<EnchantmentDefinition>(RegistryIds.Enchantment, 2);
        builder.Add(0, Identifier.Minecraft("sharpness"), new EnchantmentDefinition(5));
        builder.Add(1, Identifier.Minecraft("mending"), new EnchantmentDefinition(1));
        Registry<EnchantmentDefinition> enchantments = builder.Build();

        var packet = new ClientboundConfigUpdateTagsPacket(
        [
            new TagRegistry(RegistryIds.Enchantment,
            [
                new TagEntry(Identifier.Minecraft("exclusive_set/damage"), [0]),
                new TagEntry(Identifier.Minecraft("exclusive_set/bow"), [1, 99]),
            ]),
            new TagRegistry(Identifier.Minecraft("block"),
            [
                new TagEntry(Identifier.Minecraft("mineable/pickaxe"), [0]),
            ]),
        ]);

        var tags = EnchantmentTags.FromPacket(packet, enchantments);

        Assert.NotNull(tags);
        Assert.Equal(2, tags.Count);
        Assert.Equal([Identifier.Minecraft("sharpness")], tags[Identifier.Minecraft("exclusive_set/damage")]);
        Assert.Equal([Identifier.Minecraft("mending")], tags[Identifier.Minecraft("exclusive_set/bow")]);
    }

    /// <summary>No enchantment arm → null, leaving any previous install alone.</summary>
    [Fact]
    public void FromPacket_WithoutTheEnchantmentArm_ReturnsNull()
    {
        var builder = new RegistryBuilder<EnchantmentDefinition>(RegistryIds.Enchantment, 1);
        builder.Add(0, Identifier.Minecraft("sharpness"), new EnchantmentDefinition(5));
        Registry<EnchantmentDefinition> enchantments = builder.Build();

        var packet = new ClientboundConfigUpdateTagsPacket(
        [
            new TagRegistry(Identifier.Minecraft("block"),
            [
                new TagEntry(Identifier.Minecraft("mineable/pickaxe"), [0]),
            ]),
        ]);

        Assert.Null(EnchantmentTags.FromPacket(packet, enchantments));
    }
}
