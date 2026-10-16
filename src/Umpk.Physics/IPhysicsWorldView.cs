using Umpk.Game.Blocks;
using Umpk.Geometry;

namespace Umpk.Physics;

/// <summary>One entity that pushes the local player this tick, carried as the pusher's feet-centre XZ position. The push direction and magnitude derive from the offset between the two positions (vanilla <c>Entity.push</c>), so nothing else about the pusher is needed.</summary>
public readonly struct EntityPusher
{
    /// <summary>Creates the push record.</summary>
    public EntityPusher(double x, double z)
    {
        X = x;
        Z = z;
    }

    /// <summary>The pusher's feet-centre X.</summary>
    public double X { get; }

    /// <summary>The pusher's feet-centre Z.</summary>
    public double Z { get; }
}

/// <summary>The entire world dependency of the physics engine. The engine reads blocks, their collision shapes, and (optionally) nearby hard entity colliders through this seam and nothing else. <c>Umpk.Client</c> implements it over the live <c>World</c>; the pathfinder implements it over planning snapshots; tests implement it over hand-built voxel fixtures.</summary>
public interface IPhysicsWorldView
{
    /// <summary>The block state at an integer position. Out-of-world positions return an air-like state.</summary>
    BlockState GetBlock(BlockPos pos);

    /// <summary>The collision AABBs for a block state, in local (0..1) block coordinates.</summary>
    ReadOnlySpan<Aabb> GetCollisionShapes(BlockState state);

    /// <summary>Whether the chunk containing the given position is loaded (vanilla off-world fall behavior).</summary>
    bool IsChunkLoaded(BlockPos pos);

    /// <summary>Appends the hard hitboxes of nearby entities (boats, shulkers) that overlap <paramref name="region"/> into <paramref name="into"/>. The default implementation adds nothing; hosts with entity tracking override it. Colliders are in world coordinates.</summary>
    void CollectEntityColliders(in Aabb region, ICollection<Aabb> into)
    {
        // Default: no entity colliders. Hosts with an entity tracker override this.
    }

    /// <summary>Appends the living entities whose bounding box overlaps the player box <paramref name="playerBox"/> into <paramref name="into"/>. In vanilla these are the entities whose own client tick runs <c>pushEntities</c> (every living entity); the engine applies one vanilla <c>Entity.push</c> delta per entry. The default implementation adds nothing; hosts with entity tracking override it.</summary>
    void CollectEntityPushers(in Aabb playerBox, ICollection<EntityPusher> into)
    {
        // Default: no entity push. Hosts with an entity tracker override this.
    }
}
