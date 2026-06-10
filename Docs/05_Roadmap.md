# 05 — Expansion Roadmap

Ship the MVP first: one court, one defender, the hesi loop, game to 11. Everything below builds on the event-bus architecture without rewrites.

## Phase 1 — Deepen the 1v1 (post-MVP, ~1–2 months)

**Trick system (crossover combos, ankle-breakers).**
The detection layer already exists: `DribbleBounce` events carry positions, `PlayerMotionTracker` has the motion history, `HesiBurst`/`DefenderFrozen` already fire. Add a `TrickRecognizer` that pattern-matches event sequences:
- *Crossover* = consecutive bounces on opposite sides of the body line within 0.6 s.
- *Between-the-legs / behind-the-back* = bounce position relative to body forward axis.
- *Combo* = tricks chained inside one possession multiply `UnpredictabilityScore` gain — style becomes mechanically real, and combo→freeze→swish becomes the highlight play.

**AI offense.** `PossessionManager` already has the two-sided possession flow; replace the give-back stub with an `AttackerAI` that uses the same motion/hesi vocabulary against you, so defense (cutting off the drive line with your body, contest timing with your real hand) becomes the second half of the skill set.

**Rebounding** — live misses with box-out positioning, removing the MVP's reset-on-miss simplification.

## Phase 2 — Multiplayer streetball parks (~3–4 months)

- **Netcode:** Unity Netcode for GameObjects + Unity Relay/Lobby is the path of least resistance. The architecture was built for this: every gameplay-relevant fact already flows through `GameEvents` or is a serializable component state. Networking work = sync ball rigidbody (state sync w/ owner authority on the current handler), hand poses (compressed), and relay the event bus.
- **The park is the lobby.** No menu matchmaking — walk up to a court, ball spins at the check spot, "got next" by physically standing on the sideline. Spectators wait on benches. This keeps the no-UI soul intact online.
- **Trust model:** ball-handler client is authoritative over the ball (VR physics can't survive server authority latency), with server-side sanity checks (max release velocity, teleport detection) as the anti-cheat baseline.
- 2v2 before 1v1-online if playtests say so — VR streetball with a teammate calling for the rock is the social magic.

## Phase 3 — Ranked & matchmaking (~2 months, overlaps Phase 2)

- **Rating:** Glicko-2 per mode (1v1 / 2v2). Park-themed rank names (e.g., *Rookie → Hooper → Bucket → Problem → Legend*) instead of bronze/silver/gold.
- **Ranked = specific courts.** The "pro park" at night, under lights. You queue by walking to it.
- Backend: Unity Lobby + a thin rating service (Cloud Code or a small Node service). Match results post from the host with both clients' signed confirmation.
- **Integrity matters more in VR:** velocity-plausibility checks on shots (human arm ≤ ~14 m/s hand speed) catch most cheats for free.

## Phase 4 — Cosmetics & identity (ongoing revenue layer)

- Avatar: sneakers, shorts, tees, sleeves, headbands; ball skins (worn rubber, chrome, glow); celebration emotes bound to real gestures (point to the sky after a 2).
- **Earned, not just bought:** swish-streak counters and rank unlock the prestige items — cosmetics that *prove* something, like a real park reputation.
- Pipeline: addressables for cosmetic bundles so drops don't require app updates.

## Phase 5 — Live park (long term)

- Daily challenge courts (modified rules: 2s-only, no-dribble, tiny rim).
- Seasonal tournaments with spectator mode (free-cam + courtside seats).
- Creator courts: court-painter tool, share codes.
- Cross-platform: the OpenXR + XR Hands stack means a Quest port is mostly build-config work, not code work. Do it when multiplayer needs the player base.

## Sequencing rule

Every phase has a "does the core still feel like streetball?" gate. If a feature adds a menu, a HUD element, or an interruption to the check-up rhythm, it gets redesigned diegetically or cut. The hesi loop is the product; everything else is distribution.
