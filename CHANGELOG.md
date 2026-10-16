# Changelog

This file records notable changes to UMPK.

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
