using Umpk.Game.Blocks;
using Umpk.Game.Entities;
using Umpk.Game.Registries;
using Umpk.Geometry;
using Umpk.Physics;

namespace Umpk.Client.Navigation;

/// <summary>Adapts the tracked <see cref="Umpk.Game.World.World"/> to <see cref="IPhysicsWorldView"/> for the local-player physics engine. Collision shapes come from an <see cref="IBlockShapeSource"/>: the version's generated shape tables by default, or whatever the host injected. Entities come from the <see cref="EntityStore"/> through the <c>entities</c> accessor: null (or a null answer, i.e. entity tracking disabled) restores the empty defaults, which is how <see cref="ClientFeatures.EntityCollision"/> turns both entity hard colliders and entity push off without a second view type.</summary>
internal sealed class WorldPhysicsView(
    Umpk.Game.World.World world,
    IBlockShapeSource shapes,
    Func<EntityStore?>? entities = null) : IPhysicsWorldView
{
    private readonly Umpk.Game.World.World _world = world;
    private readonly IBlockShapeSource _shapes = shapes;
    private readonly Func<EntityStore?>? _entities = entities;

    public BlockState GetBlock(BlockPos pos) => _world.GetBlock(pos);

    public ReadOnlySpan<Aabb> GetCollisionShapes(BlockState state) => _shapes.GetCollisionShapes(state);

    public bool IsChunkLoaded(BlockPos pos) => _world.GetColumn(pos) is not null;

    /// <summary>Vanilla <c>canBeCollidedWith</c> entities — boats, shulkers, happy ghasts — whose standing box overlaps the sweep region become hard colliders. Sizes come from <see cref="EntityColliderCatalog"/> (vanilla 26.3 hand data), not the registry, which only knows names, and are multiplied by the entity's tracked <c>minecraft:scale</c> attribute clamped through the type's vanilla <c>sanitizeScale</c> bound, so command-resized entities collide at their real size.</summary>
    public void CollectEntityColliders(in Aabb region, ICollection<Aabb> into)
    {
        if (_entities?.Invoke() is not { } store)
            return;

        foreach (Entity entity in store.All)
        {
            EntityColliderInfo info = EntityColliderCatalog.Lookup(entity.Type.Id.Path);
            if (!info.HardCollider)
                continue;

            double scale = EffectiveScale(entity, info);
            Aabb box = Aabb.OfSize(entity.Position.X, entity.Position.Y, entity.Position.Z, info.Width * scale, info.Height * scale);
            if (box.Intersects(region))
                into.Add(box);
        }
    }

    /// <summary>Vanilla client semantics, mirrored from <c>ClientLevel.getPushableEntities</c>: only the LOCAL player is ever a push target, and the pushers are exactly the entities whose own client tick runs <c>LivingEntity.aiStep</c> — living entities. Non-living types (items, projectiles, boats) never initiate a push; hard colliders don't need to because their boxes can't overlap a body that collision keeps out. Pusher reach uses the scaled box for the same reason.</summary>
    public void CollectEntityPushers(in Aabb playerBox, ICollection<EntityPusher> into)
    {
        if (_entities?.Invoke() is not { } store)
            return;

        foreach (Entity entity in store.All)
        {
            EntityColliderInfo info = EntityColliderCatalog.Lookup(entity.Type.Id.Path);
            if (!info.Living)
                continue;

            double scale = EffectiveScale(entity, info);
            Aabb box = Aabb.OfSize(entity.Position.X, entity.Position.Y, entity.Position.Z, info.Width * scale, info.Height * scale);
            if (box.Intersects(playerBox))
                into.Add(new EntityPusher(entity.Position.X, entity.Position.Z));
        }
    }

    /// <summary>Vanilla 26.3 <c>LivingEntity.getScale()</c>: the server-synced <c>minecraft:scale</c> attribute value clamped through the type's <c>sanitizeScale</c> override. The attribute arrives via <c>update_attributes</c> on 1.20.5+ servers; an entity that was never sent one keeps vanilla's default of 1.0. <c>getAgeScale</c> (baby 0.5) is not applied — the baby bit lives in AgeableMob metadata this client does not decode, and the catalog documents the adult-box approximation.</summary>
    private static double EffectiveScale(Entity entity, EntityColliderInfo info)
    {
        return entity.Attributes.TryGet(AttributeIds.Scale, out AttributeInstance instance)
            ? Math.Min(instance.Value, info.MaxScale)
            : Math.Min(1.0, info.MaxScale);
    }
}
