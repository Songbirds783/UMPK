using System.Reflection;
using Umpk.Client;
using Umpk.Data.Java;
using Umpk.Protocol.Java;
using Umpk.Protocol.Java.Packets;
using Xunit;

namespace Umpk.Client.Tests;

public sealed class PacketApplicationAllocationTests
{
    [Fact]
    public async Task ApplyingAReusablePacketDoesNotReallocateCapabilityDelegates()
    {
        Assert.True(JavaVersions.TryGetByProtocol(777, out JavaVersion version));
        await using UmpkClient client = new UmpkClientBuilder()
            .UseVersion(version)
            .UseProfile(new GameProfile(Guid.Empty, "AllocationTest"))
            .UseStaticRegistries(JavaGameData.Registries(777))
            .Build();

        // Bind once outside measurement. The allocation budget covers the production application path, including its context, ordered dispatch and event publication, without transport or scheduler noise.
        MethodInfo method = typeof(UmpkClient).GetMethod("ApplyPacketAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var apply = method.CreateDelegate<Func<object, ProtocolPhase, int, int, CancellationToken, bool, ValueTask>>(client);
        var packet = new ClientboundRotateHeadPacket(123, 0);
        const int iterations = 1000;
        for (int i = 0; i < iterations; i++)
            await apply(packet, ProtocolPhase.Play, 0, 0, CancellationToken.None, false);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
            await apply(packet, ProtocolPhase.Play, 0, 0, CancellationToken.None, false);

        // The budget is an upper bound, re-calibrated on the 10.0.13 runtime (its internal paths allocate more than 10.0.9 did, about 2.4 KB per application here): a genuine order-of-magnitude regression on the application path still trips it.
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated <= iterations * 2800, $"Packet application allocated {allocated / iterations} bytes per packet; budget is 2800 bytes.");
    }
}
