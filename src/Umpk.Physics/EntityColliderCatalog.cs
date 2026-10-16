using System.Collections.Frozen;

namespace Umpk.Physics;

/// <summary>The standing bounding-box size of one entity type, plus the two collision-relevant behavior flags.</summary>
/// <param name="width">Adult bounding-box width in blocks.</param>
/// <param name="height">Adult bounding-box height in blocks.</param>
/// <param name="hardCollider">Whether the type blocks movement the way a block does — vanilla <c>canBeCollidedWith</c>: boats and rafts always, shulkers and happy ghasts while alive.</param>
/// <param name="living">Whether the type ticks <c>LivingEntity.aiStep</c>, which is the client-side source of entity push: only living entities initiate a push against the local player.</param>
/// <param name="maxScale">Upper bound the type clamps its <c>minecraft:scale</c> attribute to — vanilla's <c>sanitizeScale</c> overrides: ender dragon forces 1.0, happy ghast clamps to 1.0, shulker clamps to 3.0, every other type is unlimited.</param>
/// <param name="babyWidth">The type's explicit vanilla <c>BABY_DIMENSIONS</c> width, or <see cref="double.PositiveInfinity"/> when the type has none.</param>
/// <param name="babyHeight">The type's explicit vanilla <c>BABY_DIMENSIONS</c> height, or <see cref="double.PositiveInfinity"/> when the type has none.</param>
/// <param name="sizeScaled">Whether the type's standing box is multiplied by its cube-mob size — vanilla's <c>AbstractCubeMob.getDefaultDimensions</c> override shared by slimes, magma cubes, and sulfur cubes (<c>typeDimensions x getSize()</c>).</param>
public readonly struct EntityColliderInfo(
    double width,
    double height,
    bool hardCollider,
    bool living,
    double maxScale = double.PositiveInfinity,
    double babyWidth = double.PositiveInfinity,
    double babyHeight = double.PositiveInfinity,
    bool sizeScaled = false)
{
    /// <summary>Adult bounding-box width in blocks.</summary>
    public double Width { get; } = width;

    /// <summary>Adult bounding-box height in blocks.</summary>
    public double Height { get; } = height;

    /// <summary>Whether the type blocks movement the way a block does (vanilla <c>canBeCollidedWith</c>).</summary>
    public bool HardCollider { get; } = hardCollider;

    /// <summary>Whether the type ticks <c>LivingEntity.aiStep</c> and therefore initiates client-side entity push.</summary>
    public bool Living { get; } = living;

    /// <summary>Upper bound the type clamps its scale attribute to (vanilla <c>sanitizeScale</c>); <see cref="double.PositiveInfinity"/> means vanilla applies no clamp.</summary>
    public double MaxScale { get; } = maxScale;

    /// <summary>The type's explicit vanilla <c>BABY_DIMENSIONS</c> width; <see cref="double.PositiveInfinity"/> means the type has no explicit baby box.</summary>
    public double BabyWidth { get; } = babyWidth;

    /// <summary>The type's explicit vanilla <c>BABY_DIMENSIONS</c> height; <see cref="double.PositiveInfinity"/> means the type has no explicit baby box.</summary>
    public double BabyHeight { get; } = babyHeight;

    /// <summary>Whether the type's standing box is multiplied by its cube-mob size (vanilla <c>AbstractCubeMob.getDefaultDimensions</c>); the size arrives as the <c>cube_size</c> metadata key, one below the reported size.</summary>
    public bool SizeScaled { get; } = sizeScaled;

    /// <summary>Vanilla 26.3 <c>LivingEntity.getDimensions</c>: the standing box is <c>typeDimensions x getAgeScale() x getScale()</c>, where a type with an explicit <c>BABY_DIMENSIONS</c> overrides the whole age term and every other baby halves the adult box (<c>getAgeScale</c> returning 0.5). Cube-mob types (<paramref name="size"/> from the <c>cube_size</c> metadata key, one below the reported size) instead multiply by their size per <c>AbstractCubeMob.getDefaultDimensions</c>. <paramref name="scale"/> is the <c>minecraft:scale</c> attribute value already clamped through <see cref="MaxScale"/>.</summary>
    public (double Width, double Height) SizeFor(bool baby, double scale, double size = 1.0)
    {
        if (!baby)
            return (Width * size * scale, Height * size * scale);

        return double.IsPositiveInfinity(BabyWidth)
            ? (Width * 0.5 * size * scale, Height * 0.5 * size * scale)
            : (BabyWidth * size * scale, BabyHeight * size * scale);
    }
}

/// <summary>Hand-curated entity bounding-box sizes and collision behavior, transcribed from the vanilla 26.3 client sources (<c>net.minecraft.world.entity.EntityTypes</c> builder calls plus the <c>canBeCollidedWith</c>/<c>aiStep</c> overrides).
///
/// <para>The protocol registry only carries entity ids and names — Mojang's data reports contain no bounding boxes, and the generated per-version datasets (<c>data/java/</c>) therefore default every type to the player's 0.6x1.8. This table is the physics-only fix: it is version-independent because entity dimensions are stable vanilla constants, and it deliberately does NOT flow back into the entity registry, so tracking/rendering sizes stay as reported. Types missing here fall back to the same 0.6x1.8 default.</para>
///
/// <para>Keyed by the bare registry path (<c>minecraft:zombie</c> and <c>zombie</c> both resolve), so a modded namespace that reuses a vanilla path gets the vanilla shape — harmless for physics, where a mis-sized box costs a fraction of a block of push or clearance.</para>
///
/// <para>Scale support: vanilla 26.3 sizes every living entity's standing box as <c>typeDimensions x getAgeScale() x getScale()</c>, where <c>getScale()</c> reads the server-synced <c>minecraft:scale</c> attribute (1.20.5+, default 1.0) and clamps it through <c>sanitizeScale</c> — ender dragon forces 1.0, happy ghast clamps to 1.0, shulker clamps to 3.0. The consumer resolves the box through <see cref="Umpk.Physics.EntityColliderInfo.SizeFor"/>, which folds in the baby stage (see below) and multiplies by the scale attribute clamped through <see cref="Umpk.Physics.EntityColliderInfo.MaxScale"/>, so a command-modified entity is collided with at its real size.</para>
///
/// <para>Baby support: since the 26.1 snapshots (starting with the baby-model rework) 36 types declare an explicit <c>BABY_DIMENSIONS</c> constant that replaces the whole age term — a baby zombie is 0.49x0.98, not the 0.3x0.975 half-box the old 0.5 age scale would give. Those types carry their explicit baby pair here; every other baby halves the adult box exactly as vanilla's default <c>getAgeScale</c> does. The baby bit arrives as the <c>is_baby</c> metadata key (index 14, shared by <c>AgeableMob</c>/<c>Zombie</c>/<c>Piglin</c>/<c>Zoglin</c>) on protocol 755+; older eras have no such key, so every entity reads as adult.</para>
///
/// <para>Known approximations, both accepted because the client has no server-side entity state: shulker and happy-ghast hardness is per <c>isAlive</c> in vanilla but modeled as always hard (a dead one despawns within a tick or two), and shulker peek/open states use the closed 1.0 box.</para></summary>
public static class EntityColliderCatalog
{
    /// <summary>The fallback for types missing from the table: the player-sized box, neither hard nor living.</summary>
    public static EntityColliderInfo Default { get; } = new(0.6, 1.8, false, false);

    private static readonly FrozenDictionary<string, EntityColliderInfo> Table = new Dictionary<string, EntityColliderInfo>
    {
        // Boats and rafts: AbstractBoat.canBeCollidedWith -> true. All twenty-two hulls share the 1.375x0.5625 box.
        ["acacia_boat"] = new(1.375, 0.5625, true, false),
        ["acacia_chest_boat"] = new(1.375, 0.5625, true, false),
        ["bamboo_chest_raft"] = new(1.375, 0.5625, true, false),
        ["bamboo_raft"] = new(1.375, 0.5625, true, false),
        ["birch_boat"] = new(1.375, 0.5625, true, false),
        ["birch_chest_boat"] = new(1.375, 0.5625, true, false),
        ["cherry_boat"] = new(1.375, 0.5625, true, false),
        ["cherry_chest_boat"] = new(1.375, 0.5625, true, false),
        ["dark_oak_boat"] = new(1.375, 0.5625, true, false),
        ["dark_oak_chest_boat"] = new(1.375, 0.5625, true, false),
        ["jungle_boat"] = new(1.375, 0.5625, true, false),
        ["jungle_chest_boat"] = new(1.375, 0.5625, true, false),
        ["mangrove_boat"] = new(1.375, 0.5625, true, false),
        ["mangrove_chest_boat"] = new(1.375, 0.5625, true, false),
        ["oak_boat"] = new(1.375, 0.5625, true, false),
        ["oak_chest_boat"] = new(1.375, 0.5625, true, false),
        ["pale_oak_boat"] = new(1.375, 0.5625, true, false),
        ["pale_oak_chest_boat"] = new(1.375, 0.5625, true, false),
        ["poplar_boat"] = new(1.375, 0.5625, true, false),
        ["poplar_chest_boat"] = new(1.375, 0.5625, true, false),
        ["spruce_boat"] = new(1.375, 0.5625, true, false),
        ["spruce_chest_boat"] = new(1.375, 0.5625, true, false),

        // Shulker (Shulker.canBeCollidedWith -> isAlive) and happy ghast (adult + alive). Happy ghast babies
        // are tracked and ride the explicit 0.2375-scaled baby box.
        ["shulker"] = new(1.0, 1.0, true, true, 3.0),
        ["happy_ghast"] = new(4.0, 4.0, true, true, 1.0, 4.0 * 0.2375, 4.0 * 0.2375),

        // Minecarts: isPushable -> true but canBeCollidedWith stays false — the player walks through them.
        ["chest_minecart"] = new(0.98, 0.7, false, false),
        ["command_block_minecart"] = new(0.98, 0.7, false, false),
        ["furnace_minecart"] = new(0.98, 0.7, false, false),
        ["hopper_minecart"] = new(0.98, 0.7, false, false),
        ["minecart"] = new(0.98, 0.7, false, false),
        ["spawner_minecart"] = new(0.98, 0.7, false, false),
        ["tnt_minecart"] = new(0.98, 0.7, false, false),

        // Living entities: everything that ticks LivingEntity.aiStep (the client push source) — mobs, golems,
        // villagers, armor stands (LivingEntity subclass despite the decoration package), and the player type.
        // Baby boxes: the trailing baby size pair is the type's explicit vanilla BABY_DIMENSIONS when one
        // exists (26.1 snapshots gave 36 types their own baby boxes; the F26.3 sources spell them as either
        // EntityDimensions.scalable(w, h) or typeDimensions.scale(f), transcribed below in either form), and
        // types without one halve the adult box through getAgeScale at read time. ArmorStand defines a
        // BABY_DIMENSIONS constant but has no baby data accessor and can never be a baby, so it gets none.
        ["allay"] = new(0.35, 0.6, false, true),
        ["armadillo"] = new(0.7, 0.65, false, true, babyWidth: 0.7 * 0.6, babyHeight: 0.65 * 0.6),
        ["armor_stand"] = new(0.5, 1.975, false, true),
        ["axolotl"] = new(0.75, 0.42, false, true, babyWidth: 0.375, babyHeight: 0.21),
        ["bat"] = new(0.5, 0.9, false, true),
        ["bee"] = new(0.55, 0.5, false, true),
        ["blaze"] = new(0.6, 1.8, false, true),
        ["bogged"] = new(0.6, 1.99, false, true),
        ["breeze"] = new(0.6, 1.77, false, true),
        ["camel"] = new(1.7, 2.375, false, true),
        ["camel_husk"] = new(1.7, 2.375, false, true),
        ["cat"] = new(0.6, 0.7, false, true, babyWidth: 0.3, babyHeight: 0.35),
        ["cave_spider"] = new(0.7, 0.5, false, true),
        ["chicken"] = new(0.4, 0.7, false, true, babyWidth: 0.3, babyHeight: 0.4),
        ["cod"] = new(0.5, 0.3, false, true),
        ["copper_golem"] = new(0.49, 0.98, false, true),
        ["cow"] = new(0.9, 1.4, false, true, babyWidth: 0.45, babyHeight: 0.7),
        ["creaking"] = new(0.9, 2.7, false, true),
        ["creeper"] = new(0.6, 1.7, false, true),
        ["dolphin"] = new(0.9, 0.6, false, true, babyWidth: 0.9 * 0.65, babyHeight: 0.6 * 0.65),
        ["donkey"] = new(1.3964844, 1.5, false, true),
        ["drowned"] = new(0.6, 1.95, false, true, babyWidth: 0.49, babyHeight: 0.98),
        ["elder_guardian"] = new(1.9975, 1.9975, false, true),
        ["ender_dragon"] = new(16.0, 8.0, false, true, 1.0),
        ["enderman"] = new(0.6, 2.9, false, true),
        ["endermite"] = new(0.4, 0.3, false, true),
        ["evoker"] = new(0.6, 1.95, false, true),
        ["fox"] = new(0.6, 0.7, false, true, babyWidth: 0.6 * 0.6, babyHeight: 0.7 * 0.6),
        ["frog"] = new(0.5, 0.5, false, true),
        ["ghast"] = new(4.0, 4.0, false, true),
        ["giant"] = new(3.6, 12.0, false, true),
        ["glow_squid"] = new(0.8, 0.8, false, true, babyWidth: 0.5, babyHeight: 0.5),
        ["goat"] = new(0.9, 1.3, false, true, babyWidth: 0.45, babyHeight: 0.65),
        ["guardian"] = new(0.85, 0.85, false, true),
        ["hoglin"] = new(1.3964844, 1.4, false, true, babyWidth: 0.75, babyHeight: 0.85),
        ["horse"] = new(1.3964844, 1.6, false, true, babyWidth: 1.3964844 * 0.7, babyHeight: 1.6 * 0.7),
        ["husk"] = new(0.6, 1.95, false, true, babyWidth: 0.49, babyHeight: 0.98),
        ["illusioner"] = new(0.6, 1.95, false, true),
        ["iron_golem"] = new(1.4, 2.7, false, true),
        ["llama"] = new(0.9, 1.87, false, true, babyWidth: 0.9 * 0.5, babyHeight: 1.87 * 0.5),
        ["magma_cube"] = new(0.52, 0.52, false, true, sizeScaled: true),
        ["mannequin"] = new(0.6, 1.8, false, true),
        ["mooshroom"] = new(0.9, 1.4, false, true, babyWidth: 0.45, babyHeight: 0.7),
        ["mule"] = new(1.3964844, 1.6, false, true),
        ["nautilus"] = new(0.875, 0.95, false, true, babyWidth: 0.875 * 0.5, babyHeight: 0.95 * 0.5),
        ["ocelot"] = new(0.6, 0.7, false, true, babyWidth: 0.3, babyHeight: 0.35),
        ["panda"] = new(1.3, 1.25, false, true, babyWidth: 1.3 * 0.5, babyHeight: 1.25 * 0.5),
        ["parched"] = new(0.6, 1.99, false, true),
        ["parrot"] = new(0.5, 0.9, false, true),
        ["phantom"] = new(0.9, 0.5, false, true),
        ["pig"] = new(0.9, 0.9, false, true, babyWidth: 0.45, babyHeight: 0.45),
        ["piglin"] = new(0.6, 1.95, false, true, babyWidth: 0.49, babyHeight: 0.98),
        ["piglin_brute"] = new(0.6, 1.95, false, true),
        ["pillager"] = new(0.6, 1.95, false, true),
        ["polar_bear"] = new(1.4, 1.4, false, true, babyWidth: 0.7, babyHeight: 0.7),
        ["pufferfish"] = new(0.7, 0.7, false, true),
        ["rabbit"] = new(0.49, 0.6, false, true, babyWidth: 0.24, babyHeight: 0.4),
        ["ravager"] = new(1.95, 2.2, false, true),
        ["salmon"] = new(0.7, 0.4, false, true),
        ["sheep"] = new(0.9, 1.3, false, true, babyWidth: 0.45, babyHeight: 0.65),
        ["silverfish"] = new(0.4, 0.3, false, true),
        ["skeleton"] = new(0.6, 1.99, false, true),
        ["skeleton_horse"] = new(1.3964844, 1.6, false, true, babyWidth: 1.3964844 * 0.7, babyHeight: 1.6 * 0.7),
        ["slime"] = new(0.52, 0.52, false, true, sizeScaled: true),
        ["sniffer"] = new(1.9, 1.75, false, true),
        ["snow_golem"] = new(0.7, 1.9, false, true),
        ["spider"] = new(1.4, 0.9, false, true),
        ["squid"] = new(0.8, 0.8, false, true, babyWidth: 0.5, babyHeight: 0.5),
        ["stray"] = new(0.6, 1.99, false, true),
        ["strider"] = new(0.9, 1.7, false, true, babyWidth: 0.45, babyHeight: 0.85),
        ["sulfur_cube"] = new(0.49, 0.49, false, true, sizeScaled: true),
        ["tadpole"] = new(0.4, 0.3, false, true),
        ["trader_llama"] = new(0.9, 1.87, false, true, babyWidth: 0.9 * 0.5, babyHeight: 1.87 * 0.5),
        ["tropical_fish"] = new(0.5, 0.4, false, true),
        ["turtle"] = new(1.2, 0.4, false, true, babyWidth: 1.2 * 0.3, babyHeight: 0.4 * 0.3),
        ["vex"] = new(0.4, 0.8, false, true),
        ["villager"] = new(0.6, 1.95, false, true, babyWidth: 0.49, babyHeight: 0.98),
        ["vindicator"] = new(0.6, 1.95, false, true),
        ["wandering_trader"] = new(0.6, 1.95, false, true),
        ["warden"] = new(0.9, 2.9, false, true),
        ["witch"] = new(0.6, 1.95, false, true),
        ["wither"] = new(0.9, 3.5, false, true),
        ["wither_skeleton"] = new(0.7, 2.4, false, true),
        ["wolf"] = new(0.6, 0.85, false, true, babyWidth: 0.3, babyHeight: 0.425),
        ["zoglin"] = new(1.3964844, 1.4, false, true, babyWidth: 0.75, babyHeight: 0.85),
        ["zombie"] = new(0.6, 1.95, false, true, babyWidth: 0.49, babyHeight: 0.98),
        ["zombie_horse"] = new(1.3964844, 1.6, false, true, babyWidth: 1.3964844 * 0.7, babyHeight: 1.6 * 0.7),
        ["zombie_nautilus"] = new(0.875, 0.95, false, true),
        ["zombie_villager"] = new(0.6, 1.95, false, true, babyWidth: 0.49, babyHeight: 0.98),
        ["zombified_piglin"] = new(0.6, 1.95, false, true, babyWidth: 0.49, babyHeight: 0.98),
        ["player"] = new(0.6, 1.8, false, true),

        // Non-living projectiles, items, and effects: they never tick aiStep, so they neither push the
        // player nor block it (walking over a dropped item or through an arrow must not shove the body).
        ["area_effect_cloud"] = new(6.0, 0.5, false, false),
        ["arrow"] = new(0.5, 0.5, false, false),
        ["block_display"] = new(0.0, 0.0, false, false),
        ["breeze_wind_charge"] = new(0.3125, 0.3125, false, false),
        ["cushion"] = new(1.0, 0.25, false, false),
        ["dragon_fireball"] = new(1.0, 1.0, false, false),
        ["egg"] = new(0.25, 0.25, false, false),
        ["end_crystal"] = new(2.0, 2.0, false, false),
        ["ender_pearl"] = new(0.25, 0.25, false, false),
        ["evoker_fangs"] = new(0.5, 0.8, false, false),
        ["experience_bottle"] = new(0.25, 0.25, false, false),
        ["experience_orb"] = new(0.5, 0.5, false, false),
        ["eye_of_ender"] = new(0.25, 0.25, false, false),
        ["falling_block"] = new(0.98, 0.98, false, false),
        ["fireball"] = new(1.0, 1.0, false, false),
        ["firework_rocket"] = new(0.25, 0.25, false, false),
        ["fishing_bobber"] = new(0.25, 0.25, false, false),
        ["glow_item_frame"] = new(0.5, 0.5, false, false),
        ["interaction"] = new(0.0, 0.0, false, false),
        ["item"] = new(0.25, 0.25, false, false),
        ["item_frame"] = new(0.5, 0.5, false, false),
        ["leash_knot"] = new(0.375, 0.5, false, false),
        ["lightning_bolt"] = new(0.0, 0.0, false, false),
        ["llama_spit"] = new(0.25, 0.25, false, false),
        ["marker"] = new(0.0, 0.0, false, false),
        ["ominous_item_spawner"] = new(0.25, 0.25, false, false),
        ["painting"] = new(0.5, 0.5, false, false),
        ["shulker_bullet"] = new(0.3125, 0.3125, false, false),
        ["small_fireball"] = new(0.3125, 0.3125, false, false),
        ["snowball"] = new(0.25, 0.25, false, false),
        ["spectral_arrow"] = new(0.5, 0.5, false, false),
        ["text_display"] = new(0.0, 0.0, false, false),
        ["tnt"] = new(0.98, 0.98, false, false),
        ["trident"] = new(0.5, 0.5, false, false),
        ["wind_charge"] = new(0.3125, 0.3125, false, false),
        ["wither_skull"] = new(0.3125, 0.3125, false, false),
    }.ToFrozenDictionary();

    /// <summary>The collision info for an entity-type id (full <c>minecraft:zombie</c> or bare <c>zombie</c>), or <see cref="Default"/> when the type is not in the table.</summary>
    public static EntityColliderInfo Lookup(string typeId)
    {
        // Bare-path matching without allocation: strip a leading "minecraft:" (the only namespace vanilla
        // uses) and fall through to the dictionary. Modded namespaces reuse the vanilla path, which is fine.
        if (typeId.AsSpan().StartsWith("minecraft:"))
            typeId = typeId["minecraft:".Length..];

        return Table.TryGetValue(typeId, out EntityColliderInfo info) ? info : Default;
    }
}
