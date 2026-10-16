# Changelog

This file records notable changes to UMPK.

## 0.9.0-beta.7.5.1 - 2026-10-16

- Install the server's `minecraft:worldgen/biome` registry during configuration: the registry_data packet's biome entries were deliberately dropped as consumerless, but `World.GetBiome` IS the consumer and the world was built over a hardcoded single-entry plains placeholder, so every position decoded a biome palette id that resolved to a guess or to nothing. The install is identity-only (the name-to-id mapping is the whole value; the definition fields stay unmodelled), elided known-pack entries keep their slot, and `SetupWorld` now prefers the server's table over the placeholder. Consumers resolve chunk palettes into named entries through `World.GetBiome` — on 1.20.5+ (protocol 766+) servers; the 764/765 combined NBT blob and older join-game registry paths stay unwritten, so those eras keep the placeholder table. The test-count baseline also catches up on the entity-scale cases the last four bumps forgot to record.

## 0.9.0-beta.7.5 - 2026-10-16

- Review-driven cleanup of the entity collision code, no behaviour change: the `SizeFor` doc now states that the `size` argument is the reported cube size (the `cube_size` key plus one), the data validator whitelists the exact `cube_size|is_baby` key pair instead of every key on index 14, the two physics-view collect passes share one box-building helper, the happy ghast row names its baby arguments like every other entry, and the cube-size read documents its negative-value fallback.

## 0.9.0-beta.7.4 - 2026-10-16

- Size cube-mob collision boxes by the cube_size metadata key (vanilla AbstractCubeMob.ID_SIZE, one below the reported size): slimes, magma cubes, and sulfur cubes stand in typeDimensions x getSize(), so a size-4 slime collides and pushes at its real 2.08x2.08 instead of the size-1 0.52x0.52. Non-cube types are unaffected, and the data validator now whitelists the one index vanilla assigns twice (14, shared by the AgeableMob baby bit and the cube size).

## 0.9.0-beta.7.3 - 2026-10-16

- Collision boxes follow the baby life stage: the `is_baby` metadata bit (index 14, shared by `AgeableMob`/`Zombie`/`Piglin`/`Zoglin`) is decoded on protocol 755+, and the 36 types with an explicit vanilla `BABY_DIMENSIONS` use it (baby zombie 0.49x0.98, baby cow 0.45x0.7, ...); every other type halves its adult box, matching vanilla's default `getAgeScale`. Baby and scale compose multiplicatively as in vanilla; eras without the key read every entity as adult.

## 0.9.0-beta.7.2 - 2026-10-16

- Size entity collision boxes by the server-synced `minecraft:scale` attribute (1.20.5+), clamped through vanilla's `sanitizeScale` bounds (ender dragon 1.0, happy ghast 1.0, shulker 3.0), so `/attribute` resize commands are collided with at their real size.

## 0.9.0-beta.7.1 - 2026-10-16

- Add entity collision physics for the local-player engine: hard colliders (boats, shulkers, happy ghasts) block movement and nearby living entities push it, both transcribed from the vanilla 26.3 client and gated behind the new `ClientFeatures.EntityCollision` switch.

## 0.9.0-beta.7 - 2026-10-11

- Reduce packet-processing allocations with cached session capabilities and callbacks, static border updates, and indexed applier dispatch.
- Initialize only the requested protocol descriptor during version lookup.
- Read buffered frames before creating timeout resources and decode only the requested sound name.

## 0.9.0-beta.6 - 2026-10-10

- Support encrypted offline login without session authentication on Minecraft 1.20.5+ and report authentication separately from encryption.
- Renew Microsoft access tokens during certificate renewal, using the Minecraft token expiry and retaining refresh tokens when no replacement is returned.
- Refresh certificates at their renewal time and serialize concurrent session refreshes.

## 0.9.0-beta.5 - 2026-10-09

- Use the platform AES-CFB8 provider for ARM32 processes while preserving the existing cipher selection on other architectures and the explicit software fallback.

## 0.9.0-beta.4 - 2026-09-30

- Replace the built-in HTTP CONNECT and SOCKS negotiation code with QuickProxyNet while preserving UMPK's proxy factory API and transport lifetime.
- Preserve graceful proxy transport shutdown across operating systems instead of inheriting QuickProxyNet's abortive socket-close default.

## 0.9.0-beta.3 - 2026-09-30

- Add weather snapshots and change events, dropped-item stack snapshots, and caller-supplied verified-navigation options for MCC integrations.
- Correct the NuGet package readme to list Minecraft Java 26.3 (protocol 777) as the newest supported version.

## 0.9.0-beta.2 - 2026-09-22

- Add Minecraft Java 26.3 (protocol 777) support and update affected packet codecs.

## 0.9.0-beta.1 - 2026-09-12

- First public beta release.
