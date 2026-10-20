using System.Buffers;
using System.IO.Pipelines;
using Umpk.Data.Java;
using Umpk.Game.Registries;
using Umpk.Hosting;
using Umpk.Nbt;
using Umpk.Protocol.Java;
using Umpk.Protocol.Java.Codecs;
using Umpk.Protocol.Java.Packets;
using Umpk.Protocol.Java.Transport;
using Umpk.TestKit.Server;
using Xunit;

namespace Umpk.Client.Tests;

/// <summary>
/// The configuration driver must SURFACE <c>minecraft:update_tags</c> to the applier chain instead of consuming it.
/// The purrland session proved every half of the chain except this hop: the frame arrived, the codec decoded it,
/// and <see cref="Umpk.Client.Appliers.ConnectionApplier"/>'s case never fired, because JavaClientLogin's
/// configuration loop surfaced registry_data and left update_tags on the consumed-and-ignored pile. This test
/// drives the real driver over a real connection, so the hop itself is what is under test.
/// </summary>
public sealed class UpdateTagsSurfaceTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(30);

    /// <summary>26.3, the era whose servers deliver tag sync during CONFIGURATION only.</summary>
    private const int Protocol = 777;

    [Fact]
    public async Task AnUpdateTagsDuringConfiguration_InstallsTheEnchantmentTags()
    {
        using var cts = new CancellationTokenSource(Budget);
        CancellationToken ct = cts.Token;

        Assert.True(JavaVersions.TryGetByProtocol(Protocol, out JavaVersion? version), "unknown protocol 777");
        await using FakeJavaServer server = FakeJavaServer.Create();
        await using UmpkClient client = LiveClient(server, version!);

        Task connect = client.ConnectAsync(new ServerEndpoint("test", 25565), ct);

        await server.NextFrameAsync(ct); // handshake
        server.ServerConnection.SetPhase(ProtocolPhase.Login);
        await server.NextFrameAsync(ct); // hello
        await SendAsync(server, version!.Protocol, ProtocolPhase.Login,
            new ClientboundLoginFinishedPacket(Guid.NewGuid(), "Tester", [], null), ct);
        await server.NextFrameAsync(ct); // login_acknowledged
        server.ServerConnection.SetPhase(ProtocolPhase.Configuration);
        await server.NextFrameAsync(ct); // client_information

        // The server's own enchantment table first: tag members arrive as numeric ids and resolve against what registry_data installed.
        await SendAsync(server, version.Protocol, ProtocolPhase.Configuration, RegistryData(
            Entry("minecraft:depth_strider", maxLevel: 3)), ct);

        // The frame the driver used to consume and ignore.
        await SendAsync(server, version.Protocol, ProtocolPhase.Configuration,
            new ClientboundConfigUpdateTagsPacket(
            [
                new TagRegistry(RegistryIds.Enchantment,
                [
                    new TagEntry(Identifier.Minecraft("exclusive_set/armor"), [0]),
                ]),
            ]), ct);

        await SendAsync(server, version.Protocol, ProtocolPhase.Configuration,
            new ClientboundFinishConfigurationPacket(), ct);
        await server.NextFrameAsync(ct); // finish_configuration
        server.ServerConnection.SetPhase(ProtocolPhase.Play);
        await Umpk.Client.Tests.Support.ScriptedServer.SendPlayReadinessFrameAsync(server, version.Protocol, ct);
        await connect.WaitAsync(Budget, ct);

        IReadOnlyDictionary<Identifier, IReadOnlyList<Identifier>>? tags =
            await client.InvokeAsync(c => c.State.EnchantmentTags, ct);

        Assert.NotNull(tags);
        Assert.Equal([Identifier.Minecraft("depth_strider")], tags![Identifier.Minecraft("exclusive_set/armor")]);
    }

    // Support

    private static UmpkClient LiveClient(FakeJavaServer server, JavaVersion version) =>
        new UmpkClientBuilder()
            .UseVersion(version)
            .UseProfile(new GameProfile(Guid.NewGuid(), "Tester"))
            .UseConnectionFactory(new PipeConnectionFactory(server.ClientPipe))
            .UseStaticRegistries(JavaGameData.Registries(version.Version.Protocol))
            .ConfigureFeatures(f =>
            {
                f.Physics = false;
                f.Pathfinding = false;
            })
            .Build();

    private static ClientboundConfigRegistryDataPacket RegistryData(params PackedRegistryEntry[] entries) =>
        new(RegistryIds.Enchantment, entries);

    private static PackedRegistryEntry Entry(string id, int maxLevel) =>
        new(Identifier.Parse(id), new NbtCompound { ["max_level"] = new NbtInt(maxLevel) });

    private static async Task SendAsync<TPacket>(
        FakeJavaServer server, ProtocolDescriptor descriptor, ProtocolPhase phase, TPacket packet, CancellationToken ct)
        where TPacket : class, IPacket
    {
        Assert.True(descriptor.TryGetRegistry(phase, PacketFlow.Clientbound, out PhaseRegistry registry));
        foreach ((int wireId, PacketType type) in registry.Packets)
        {
            if (type.Id != packet.Type.Id)
                continue;

            Assert.True(registry.TryGetInbound(wireId, out BoundPacketCodec codec));
            Assert.True(codec.IsImplemented, $"{packet.Type.Id} is a marker in {phase}.");
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new PacketWriter(buffer);
            codec.Encode(ref writer, packet, PacketCodecContext.Registryless);
            await server.SendFrameAsync(wireId, buffer.WrittenSpan.ToArray(), ct);
            return;
        }

        throw new Xunit.Sdk.XunitException($"{packet.Type.Id} is not registered clientbound in {phase}.");
    }

    private sealed class PipeConnectionFactory(IDuplexPipe pipe) : IConnectionFactory
    {
        public ValueTask<IDuplexPipe> ConnectAsync(ServerEndpoint endpoint, CancellationToken ct)
            => ValueTask.FromResult(pipe);
    }
}
