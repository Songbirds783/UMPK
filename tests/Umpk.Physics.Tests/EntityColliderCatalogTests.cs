using Xunit;

namespace Umpk.Physics.Tests;

/// <summary>Spot checks against the hand-curated catalog: the transcribed vanilla values, the two behavior flags, and the 0.6x1.8 fallback.</summary>
public sealed class EntityColliderCatalogTests
{
    [Fact]
    public void Boats_AreHardColliders()
    {
        EntityColliderInfo boat = EntityColliderCatalog.Lookup("oak_boat");
        Assert.Equal(1.375, boat.Width, 6);
        Assert.Equal(0.5625, boat.Height, 6);
        Assert.True(boat.HardCollider);
        Assert.False(boat.Living);
    }

    [Fact]
    public void NamespacedIds_ResolveByBarePath()
    {
        Assert.Equal(EntityColliderCatalog.Lookup("zombie"), EntityColliderCatalog.Lookup("minecraft:zombie"));
    }

    [Fact]
    public void Shulker_AndHappyGhast_AreHardColliders()
    {
        Assert.True(EntityColliderCatalog.Lookup("shulker").HardCollider);
        Assert.True(EntityColliderCatalog.Lookup("happy_ghast").HardCollider);
    }

    [Fact]
    public void Minecarts_AreNotHardColliders()
    {
        // Pushable but passable: vanilla canBeCollidedWith stays false, so the player walks through.
        Assert.False(EntityColliderCatalog.Lookup("minecart").HardCollider);
        Assert.True(EntityColliderCatalog.Lookup("minecart").Width < 1.0);
    }

    [Fact]
    public void Mobs_AreLivingPushers_WithVanillaSizes()
    {
        EntityColliderInfo zombie = EntityColliderCatalog.Lookup("zombie");
        Assert.Equal(0.6, zombie.Width, 6);
        Assert.Equal(1.95, zombie.Height, 6);
        Assert.True(zombie.Living);
        Assert.False(zombie.HardCollider);

        Assert.Equal(2.9, EntityColliderCatalog.Lookup("enderman").Height, 6);
        Assert.Equal(1.4, EntityColliderCatalog.Lookup("iron_golem").Width, 6);
        Assert.Equal(16.0, EntityColliderCatalog.Lookup("ender_dragon").Width, 6);
    }

    [Fact]
    public void Player_AndArmorStand_CountAsLiving()
    {
        Assert.True(EntityColliderCatalog.Lookup("player").Living);
        // Armor stand subclasses LivingEntity despite its decoration package placement.
        Assert.True(EntityColliderCatalog.Lookup("armor_stand").Living);
        Assert.Equal(1.975, EntityColliderCatalog.Lookup("armor_stand").Height, 6);
    }

    [Fact]
    public void NonLivingTypes_NeverPush()
    {
        Assert.False(EntityColliderCatalog.Lookup("item").Living);
        Assert.False(EntityColliderCatalog.Lookup("arrow").Living);
        Assert.False(EntityColliderCatalog.Lookup("experience_orb").Living);
        Assert.False(EntityColliderCatalog.Lookup("oak_boat").Living);
    }

    [Fact]
    public void UnknownTypes_FallBackToPlayerBox()
    {
        EntityColliderInfo fallback = EntityColliderCatalog.Lookup("some_mod_custom_beast");
        Assert.Equal(0.6, fallback.Width, 6);
        Assert.Equal(1.8, fallback.Height, 6);
        Assert.False(fallback.HardCollider);
        Assert.False(fallback.Living);
    }
}
