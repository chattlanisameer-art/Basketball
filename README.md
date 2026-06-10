# HESI — VR Streetball

A physics-based 1v1 VR streetball game for **Pico 4 Ultra**, built in Unity.
Skill, timing, and hesitation moves decide everything. No arcade menus, no UI spam — just you, a defender, and a ball that behaves like a real basketball.

## What this repo contains

| Path | Contents |
|---|---|
| `Docs/01_UnitySetup.md` | Exact Unity version, packages, Pico 4 Ultra project settings, input setup |
| `Docs/02_Architecture.md` | Folder structure, scripts list, scene layout, how systems talk to each other |
| `Docs/03_GameplayDesign.md` | 1v1 match flow, scoring, possession, defense in VR space |
| `Docs/04_GameFeel.md` | Movement tuning, VR comfort, ball responsiveness, skill ceiling |
| `Docs/05_Roadmap.md` | Multiplayer parks, ranked, trick system, cosmetics, matchmaking |
| `Assets/Scripts/` | All core gameplay C# scripts, ready to drop into a Unity project |

## The core loop

1. **Check ball** at the top of the arc.
2. Dribble with real hand contact — every bounce is physics, not animation.
3. Use the **Hesi system**: slow down, freeze the defender, burst past him. The AI reads your *speed history*, so unpredictability is a real stat.
4. Shoot with your actual arm — release force, angle, and timing come from your hand velocity.
5. Game to 11. 1s and 2s. Win by 2. Loser's ball.

## The Hesi system (the heart of the game)

- **Slow before a drive** → defender's reaction latency grows (he's reading you on a delay).
- **Change speed unpredictably** → your "unpredictability score" rises → bigger advantage.
- **Over-dribble** with no progress → ball control and shot accuracy decay (real streetball punishes dancing).
- **Burst after hesitation** → short speed boost + a shot-bonus window where your release is more forgiving.

Start with `Docs/01_UnitySetup.md`.
