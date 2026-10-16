using Umpk.Client.Internal;
using Umpk.Client.Navigation;
using Umpk.Data.Java;
using Umpk.Game.Entities;
using Umpk.Game.Registries;
using Umpk.Geometry;
using Umpk.Physics;
using Xunit;

namespace Umpk.Client.Tests;

/// <summary>
/// The physics view sizes every entity box by the server-synced <c>minecraft:scale</c> attribute, clamped through the type's vanilla <c>sanitizeScale</c> bound (F26.3 <c>LivingEntity.getScale</c>: ender dragon forces 1.0, happy ghast clamps to 1.0, shulker clamps to 3.0).
/// <para>On 1.20.5+ servers the attribute arrives via <c>update_attributes</c>, so <c>/attribute @e[...]</c> resize commands are collided with at their real size. An entity the server never sent an attribute frame for keeps vanilla's default 1.0, and on pre-1.20.5 servers no attribute is ever resolvable, which is exactly vanilla's hard-coded behaviour.</para>
/// </summary>
public sealed class EntityScaleCollisionTests
{
    private const int Protocol = 772;

    [Fact]
    public void ScaledBoat_IsCollidedAtEnlargedSize()
    {
        (WorldPhysicsView view, _, _) = BuildViewWith("oak_boat", scale: 2.0);

        // Unscaled the 1.375-wide hull ends at x=0.6875, short of a player at [0.9, 1.5]; scaled x2 it reaches 1.375.
        var player = Aabb.OfSize(1.2, 0.0, 0.0, 0.6, 1.8);
        var collected = new List<Aabb>();
        view.CollectEntityColliders(player, collected);

        Assert.Single(collected);
        Assert.Equal(2.75, collected[0].XSize, 6);
    }

    [Fact]
    public void UnscaledBoat_KeepsCatalogSize()
    {
        (WorldPhysicsView view, _, _) = BuildViewWith("oak_boat", scale: null);

        var reachable = Aabb.OfSize(0.5, 0.0, 0.0, 0.6, 1.8);
        var collected = new List<Aabb>();
        view.CollectEntityColliders(reachable, collected);

        Assert.Single(collected);
        Assert.Equal(1.375, collected[0].XSize, 6);
    }

    [Fact]
    public void ScaledZombie_PushesFromFarther()
    {
        (WorldPhysicsView view, _, _) = BuildViewWith("zombie", scale: 2.0);

        // Unscaled the 0.6-wide zombie's reach ends at x=0.3; scaled x2 it ends at 0.6 and reaches a player at [0.45, 1.05].
        var player = Aabb.OfSize(0.75, 0.0, 0.0, 0.6, 1.8);
        var pushers = new List<EntityPusher>();
        view.CollectEntityPushers(player, pushers);

        Assert.Single(pushers);
        Assert.Equal(0.0, pushers[0].X, 6);
    }

    [Fact]
    public void SanitizeClamps_FollowVanillaOverrides()
    {
        // Happy ghast clamps to 1.0: a scale-16 command still leaves the 4.0 box.
        (WorldPhysicsView ghastView, _, _) = BuildViewWith("happy_ghast", scale: 16.0);
        var ghastCollected = new List<Aabb>();
        ghastView.CollectEntityColliders(Aabb.OfSize(0.0, 0.0, 0.0, 0.6, 1.8), ghastCollected);
        Assert.Equal(4.0, ghastCollected[0].XSize, 6);

        // Shulker clamps to 3.0: 1.0 x 3.
        (WorldPhysicsView shulkerView, _, _) = BuildViewWith("shulker", scale: 16.0);
        var shulkerCollected = new List<Aabb>();
        shulkerView.CollectEntityColliders(Aabb.OfSize(0.0, 0.0, 0.0, 0.6, 1.8), shulkerCollected);
        Assert.Equal(3.0, shulkerCollected[0].XSize, 6);

        // Ender dragon forces exactly 1.0 regardless of the attribute. It is not a hard collider in vanilla,
        // so the clamp shows on its push reach: forced to the 16-wide box it stops pushing at x=8, while an
        // unclamped scale-8 dragon (16x8=128 wide) would still push a player standing at x=10.
        (WorldPhysicsView dragonView, _, _) = BuildViewWith("ender_dragon", scale: 8.0);
        var clamped = new List<EntityPusher>();
        dragonView.CollectEntityPushers(Aabb.OfSize(10.0, 0.0, 0.0, 0.6, 1.8), clamped);
        Assert.Empty(clamped);

        var inReach = new List<EntityPusher>();
        dragonView.CollectEntityPushers(Aabb.OfSize(5.0, 0.0, 0.0, 0.6, 1.8), inReach);
        Assert.Single(inReach);
    }

    [Fact]
    public void BabyZombie_UsesExplicitBabyBox()
    {
        // Baby zombies ride the 0.49x0.98 BABY_DIMENSIONS, so their push reach ends at x=0.245: a player
        // at [0.27, 0.87] is out of reach, while the adult 0.6-wide box (reach 0.3) still touches it.
        (WorldPhysicsView babyView, _, _) = BuildViewWith("zombie", scale: null, baby: true);
        var babyPushers = new List<EntityPusher>();
        babyView.CollectEntityPushers(Aabb.OfSize(0.57, 0.0, 0.0, 0.6, 1.8), babyPushers);
        Assert.Empty(babyPushers);

        (WorldPhysicsView adultView, _, _) = BuildViewWith("zombie", scale: null);
        var adultPushers = new List<EntityPusher>();
        adultView.CollectEntityPushers(Aabb.OfSize(0.57, 0.0, 0.0, 0.6, 1.8), adultPushers);
        Assert.Single(adultPushers);
    }

    [Fact]
    public void BabyBoxes_ExplicitOverridesAndHalfAdultFallback()
    {
        // Explicit: baby zombie is 0.49x0.98, not the 0.3x0.975 half-box the old uniform age scale gave.
        var zombie = EntityColliderCatalog.Lookup("zombie");
        Assert.Equal((0.49, 0.98), zombie.SizeFor(baby: true, scale: 1.0));

        // Fallback: a donkey declares no BABY_DIMENSIONS, so its baby halves the adult box like vanilla's
        // default getAgeScale does.
        var donkey = EntityColliderCatalog.Lookup("donkey");
        Assert.Equal((1.3964844 * 0.5, 1.5 * 0.5), donkey.SizeFor(baby: true, scale: 1.0));
    }

    [Fact]
    public void BabyHappyGhast_CollidesAtBabySize()
    {
        // Happy ghast is the one hard collider with a tracked baby: the 4.0 box drops to the explicit
        // 0.95x0.95 one (4.0 x 0.2375), and the scale-attribute clamp stays separate from the age term.
        (WorldPhysicsView view, _, _) = BuildViewWith("happy_ghast", scale: null, baby: true);
        var collected = new List<Aabb>();
        view.CollectEntityColliders(Aabb.OfSize(0.3, 0.0, 0.0, 0.6, 1.8), collected);
        Assert.Single(collected);
        Assert.Equal(0.95, collected[0].XSize, 6);
    }

    [Fact]
    public void BabyAndScale_ComposeMultiplicatively()
    {
        // Vanilla composes getAgeScale x getScale: a scale-2 baby horse pushes with half its adult
        // reach — box radius 0.9775, versus 0.4888 for a scale-1 baby and 1.3965 for a scale-2 adult.
        (WorldPhysicsView view, _, _) = BuildViewWith("horse", scale: 2.0, baby: true);
        var inBabyReach = new List<EntityPusher>();
        view.CollectEntityPushers(Aabb.OfSize(1.1, 0.0, 0.0, 0.6, 1.8), inBabyReach);
        Assert.Single(inBabyReach);

        var beyondBabyReach = new List<EntityPusher>();
        view.CollectEntityPushers(Aabb.OfSize(1.6, 0.0, 0.0, 0.6, 1.8), beyondBabyReach);
        Assert.Empty(beyondBabyReach);
    }

    [Fact]
    public void CubeSize_ScalesSlimePushReach()
    {
        // A size-4 slime (metadata 3, one below the reported size) is 2.08 wide: its push reach ends at
        // x=1.04, while the size-1 default (0.52) ends at x=0.26. Boxes are centered on the position.
        (WorldPhysicsView bigView, _, _) = BuildViewWith("slime", scale: null, cubeSize: 3);
        var bigPushers = new List<EntityPusher>();
        bigView.CollectEntityPushers(Aabb.OfSize(0.5, 0.0, 0.0, 0.6, 1.8), bigPushers);
        Assert.Single(bigPushers);

        var bigBeyond = new List<EntityPusher>();
        bigView.CollectEntityPushers(Aabb.OfSize(1.5, 0.0, 0.0, 0.6, 1.8), bigBeyond);
        Assert.Empty(bigBeyond);

        (WorldPhysicsView smallView, _, _) = BuildViewWith("slime", scale: null);
        var smallPushers = new List<EntityPusher>();
        smallView.CollectEntityPushers(Aabb.OfSize(0.1, 0.0, 0.0, 0.6, 1.8), smallPushers);
        Assert.Single(smallPushers);

        var smallBeyond = new List<EntityPusher>();
        smallView.CollectEntityPushers(Aabb.OfSize(0.7, 0.0, 0.0, 0.6, 1.8), smallBeyond);
        Assert.Empty(smallBeyond);
    }

    [Fact]
    public void CubeSize_FollowsVanillaSizeTerm()
    {
        // All three cube mobs share AbstractCubeMob's typeDimensions x getSize() override: a size-3 magma
        // cube is 1.56 and a size-2 sulfur cube 0.98. SizeFor is pure arithmetic — the guard that keeps
        // non-cube types at size 1 lives in WorldPhysicsView, which only feeds the key for sizeScaled types.
        var magma = EntityColliderCatalog.Lookup("magma_cube");
        Assert.Equal((1.56, 1.56), magma.SizeFor(baby: false, scale: 1.0, size: 3));

        var sulfur = EntityColliderCatalog.Lookup("sulfur_cube");
        Assert.Equal((0.98, 0.98), sulfur.SizeFor(baby: false, scale: 1.0, size: 2));

        Assert.True(magma.SizeScaled && sulfur.SizeScaled);
        Assert.False(EntityColliderCatalog.Lookup("cow").SizeScaled);
    }

    private static (WorldPhysicsView View, EntityStore Store, Entity Entity) BuildViewWith(
        string typeId, double? scale, bool baby = false, int? cubeSize = null)
    {
        Registry<BlockDefinition> blocks = JavaGameData.Registries(Protocol).Blocks;
        var data = new RegistryBlockDataSource(blocks, isLegacy: false);
        IBlockShapeSource shapes = JavaGameData.BlockShapes(Protocol);
        var world = new Umpk.Game.World.World(
            WorldFactory.CreateDimension(new CommonWorldSetup("minecraft:overworld", 0), registries: null, Protocol),
            data,
            WorldFactory.EmptyBiomes());

        var store = new EntityStore();
        var view = new WorldPhysicsView(world, shapes, () => store);

        RegistryEntry<EntityTypeDefinition> type = Registry.Direct(
            1,
            Identifier.Minecraft(typeId),
            new EntityTypeDefinition(0.6f, 1.8f));
        var entity = new Entity(42, Guid.NewGuid(), type, JavaGameData.EntityMetadataKeys(Protocol));
        store.Add(entity);

        // Index 14 carries both semantics on the wire — is_baby for the AgeableMob chains, cube_size for
        // AbstractCubeMob — but a single entity type only ever sends one, so these two arms never compose.
        if (baby)
            entity.Metadata.Set(14, MetadataValue.Boolean(true));

        if (cubeSize is not null)
            entity.Metadata.Set(14, MetadataValue.VarInt(cubeSize.Value));

        if (scale is not null)
        {
            RegistryEntry<AttributeDefinition> scaleAttribute = Registry.Direct(
                1,
                new Identifier("minecraft", "scale"),
                new AttributeDefinition(1.0, 0.0625, 16.0));
            AttributeInstance instance = entity.Attributes.GetOrCreate(AttributeIds.Scale, scaleAttribute);
            instance.BaseValue = scale.Value;
        }

        return (view, store, entity);
    }
}
