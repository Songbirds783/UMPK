using Umpk.Geometry;
using Umpk.Physics.Tests.Fixtures;
using Xunit;

namespace Umpk.Physics.Tests;

/// <summary>Vanilla entity push: the exact <c>Entity.push</c> delta and its 0.01 gate, per-pusher accumulation, and the tick-pipeline placement (push lands after zeroing, before travel — proven by seed equivalence).</summary>
public sealed class EntityPushTests
{
    private static PlayerPhysics NewEngine(FixtureWorld world, Vec3d start)
    {
        var engine = new PlayerPhysics(world, PhysicsProfile.Modern);
        engine.SetConditions(PhysicsConditions.Default);
        engine.Reset(start, 0f, 0f);
        return engine;
    }

    private static FixtureWorld Floor() => new FixtureWorld().Floor(-5, 5, -5, 5, 63, BlockKind.Stone);

    /// <summary>The vanilla <c>Entity.push</c> delta from pusher to pushee, replaying the 26.3 op sequence exactly: direction (dx, dz)/sqrt(max(|dx|, |dz|)), scaled by 0.05 * min(1, 1/sqrt(max(|dx|, |dz|))). Note the "unit" direction is NOT Euclidean-normalized — a 0.4 single-axis separation yields a 0.05*sqrt(0.4) delta, not 0.05.</summary>
    private static (double X, double Z) VanillaPushDelta(double dx, double dz)
    {
        double dd = Math.Max(Math.Abs(dx), Math.Abs(dz));
        Assert.True(dd >= 0.009999999776482582, "caller must pick a separation above the vanilla gate");
        double scale = Math.Sqrt(dd);
        dx /= scale;
        dz /= scale;
        double pow = Math.Min(1.0, 1.0 / scale);
        return (dx * pow * 0.05000000074505806, dz * pow * 0.05000000074505806);
    }

    [Fact]
    public void Push_MatchesVanillaDelta_AndFeedsSameTickTravel()
    {
        // Pusher 0.4 blocks south: vanilla delta = 0.05 * sqrt(0.4) ~ 0.0316 north. An engine that instead
        // STARTS the tick already carrying that velocity must land identically: this pins both the formula
        // and the aiStep placement, because the push is inserted after tiny-velocity zeroing and before
        // travel — the same place a pre-seeded velocity would have to sit to produce the same motion.
        (double dx, double dz) = VanillaPushDelta(0.0, 0.4);

        var pushed = NewEngine(Floor().AddEntityPusher(0.5, 0.9), new Vec3d(0.5, 64.0, 0.5));
        var seeded = NewEngine(Floor(), new Vec3d(0.5, 64.0, 0.5));
        seeded.SetVelocity(new Vec3d(-dx, 0.0, -dz));

        StepResult a = pushed.Step(MovementInput.None);
        StepResult b = seeded.Step(MovementInput.None);

        Assert.Equal(b.State.Position.X, a.State.Position.X, 12);
        Assert.Equal(b.State.Position.Z, a.State.Position.Z, 12);
        Assert.Equal(b.State.Velocity.X, a.State.Velocity.X, 12);
        Assert.Equal(b.State.Velocity.Z, a.State.Velocity.Z, 12);
    }

    [Fact]
    public void Push_BelowOneCentiGate_DoesNothing()
    {
        // Vanilla skips the push entirely when the larger axis offset is under 0.01.
        var pushed = NewEngine(Floor().AddEntityPusher(0.5, 0.505), new Vec3d(0.5, 64.0, 0.5));
        var plain = NewEngine(Floor(), new Vec3d(0.5, 64.0, 0.5));

        StepResult a = pushed.Step(MovementInput.None);
        StepResult b = plain.Step(MovementInput.None);

        Assert.Equal(b.State.Position.Z, a.State.Position.Z, 12);
        Assert.Equal(b.State.Velocity.Z, a.State.Velocity.Z, 12);
    }

    [Fact]
    public void Push_AppliesVanillaFalloff_OnBothAxes()
    {
        // Pusher 1.2 east and 0.3 south: dd = 1.2 > 1, so pow = 1/sqrt(1.2) < 1 — the falloff branch. The
        // X component still works out to exactly 0.05 (single dominant axis cancels), but the secondary
        // Z component is scaled down by the same factor, and the direction is away from the pusher.
        (double dx, double dz) = VanillaPushDelta(1.2, 0.3);
        Assert.Equal(0.05, dx, 5);
        Assert.True(dz < 0.013 && dz > 0.012, $"falloff Z delta out of range: {dz}");

        var pushed = NewEngine(Floor().AddEntityPusher(1.7, 0.8), new Vec3d(0.5, 64.0, 0.5));
        var seeded = NewEngine(Floor(), new Vec3d(0.5, 64.0, 0.5));
        seeded.SetVelocity(new Vec3d(-dx, 0.0, -dz));

        StepResult a = pushed.Step(MovementInput.None);
        StepResult b = seeded.Step(MovementInput.None);

        Assert.Equal(b.State.Velocity.X, a.State.Velocity.X, 12);
        Assert.Equal(b.State.Velocity.Z, a.State.Velocity.Z, 12);
    }

    [Fact]
    public void Push_Direction_IsAwayFromPusher()
    {
        // Pusher north (smaller Z): the body must drift SOUTH over the next ticks with no input.
        var engine = NewEngine(Floor().AddEntityPusher(0.5, 0.1), new Vec3d(0.5, 64.0, 0.5));

        engine.Step(MovementInput.None);
        engine.Step(MovementInput.None);

        Assert.True(engine.State.Position.Z > 0.51, $"drifted wrong way or not at all: z={engine.State.Position.Z}");
    }

    [Fact]
    public void Push_OppositePushers_Cancel()
    {
        // Each pusher contributes its own vanilla delta; symmetric placement sums to exactly zero motion.
        var engine = NewEngine(Floor().AddEntityPusher(0.5, 0.9).AddEntityPusher(0.5, 0.1), new Vec3d(0.5, 64.0, 0.5));
        var plain = NewEngine(Floor(), new Vec3d(0.5, 64.0, 0.5));

        engine.Step(MovementInput.None);
        plain.Step(MovementInput.None);

        Assert.Equal(plain.State.Position.Z, engine.State.Position.Z, 12);
        Assert.Equal(plain.State.Velocity.Z, engine.State.Velocity.Z, 12);
    }
}
