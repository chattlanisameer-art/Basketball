# 04 — The Feel of the Game

This doc is the tuning bible. Every number here already matches the script defaults; change them together.

## 1. Movement speed tuning

| Parameter | Value | Why |
|---|---|---|
| Base stick locomotion speed | **2.6 m/s** | Brisk walk-jog. Faster causes vection sickness; slower kills the streetball energy. |
| Momentum range | ×0.88 → ×1.18 | First step feels weighty, full stride feels fast: 2.3 → 3.1 m/s. |
| Hesi burst boost | up to ×1.35, 1.4 s | Stacked on full momentum = **4.1 m/s peak** — only reachable through a real setup. The fastest movement in the game is *earned*, never default. |
| Defender lateral / recovery | 3.2 / 4.2 m/s | He's slightly faster than your base but slower than your burst. The only way past him is craft. This ratio IS the difficulty curve. |
| Hard cut momentum retention | 45% | Cuts cost something, like real ankles do. |

**Locomotion model:** continuous stick locomotion on the left stick, head-relative, **no artificial smooth turning** (snap turn 45° on right stick if needed — but on a 15 m court most players physically turn). Room-scale movement is always 1:1 and always wins; stick is for covering court distance. Apply `MomentumSystem.SpeedMultiplier` to the locomotion provider's speed each frame.

## 2. VR comfort settings

- **90 Hz always.** A dropped frame is worse than any graphics downgrade. Fixed Foveated Rendering on, soft shadows off, one realtime light.
- **No camera acceleration.** Stick speed changes are instant (the momentum multiplier shifts over ~1.5 s, which reads as effort, not as vection — it's slow enough to be comfortable).
- **No screen shake, no FOV kicks, no vignette by default.** Vignette-on-locomotion as an *opt-in* comfort toggle only.
- **Horizon lock:** the camera is never rotated or offset by gameplay. Ankle-breaker feedback is audio + defender animation, never a camera effect.
- **The check-ball rhythm is the comfort system.** Possessions are 10–25 s bursts of intensity followed by a calm walk back. Playtests of this loop pattern show it lets stick-locomotion-sensitive players go far longer than constant motion does.
- Height calibration: Floor tracking origin, no virtual crouch — your real height is your height.

## 3. Ball responsiveness

The ball must feel *trustworthy* — the moment players stop believing the bounce, the game dies:

- **200 Hz physics** (`GameManager` forces it). Non-negotiable; at 50 Hz dribbling is unplayable.
- **Hand→ball latency:** `VRHandTracker` velocity window 0.09 s. Shorter feels jittery, longer feels like throwing through syrup.
- **Dribble push transfer 1.25×** — the ball comes off your hand slightly hotter than your hand moved, which reads as "live ball, good grip" (1.0 feels dead).
- **Catch assist 18 m/s², 0.18 m radius** — strong enough that a decent dribble rhythm sustains itself, weak enough that you must actually track the ball. Tune this FIRST in playtesting; it's the single biggest feel dial in the game.
- **Restitution 0.72 + spin transfer** — crossovers behave: a hard angled push comes back across your body, backspin floats, asphalt sounds change with impact speed.
- Throw release uses smoothed velocity + **wrist snap factor 0.15** — the flick at the end of a real shooting motion meaningfully adds arc and backspin, so proper form is mechanically rewarded before any assist math runs.

## 4. Skill ceiling progression

The same systems read differently as players improve — no unlocks, just depth:

| Stage | What the player relies on | What stops working |
|---|---|---|
| **Day 1** | Shot assist near the rim, catch assist for basic dribbling | Standing still (steals), wild heaves (no assist beyond intent radius) |
| **Week 1** | Deliberate hesi → burst, finishing the open look in the bonus window | Telegraphed rhythm — constant-speed drives get smothered by the 0.28 s defender |
| **Month 1** | Unpredictability stacking: varied speeds *between* moves so every hesi starts from a high multiplier; 2-point pull-ups out of the burst | Over-dribbling — fishing for freezes drains control and gets stripped |
| **Ceiling** | Chaining: cut (keeps 45% momentum) → re-accelerate → hesi at full momentum + max variance → frozen defender → step-back 2. Clean-swish hunting. | Nothing — at this level the assist blend is mostly irrelevant because raw form is already on line |

Difficulty dial for the defender: `baseReactionTime` 0.2 (elite) / 0.28 (default) / 0.45 (rookie), plus `stealAggression` and `lateralSpeed`. Ship three presets, no menus mid-game — pick by which court you walk to (post-MVP).

## 5. Sound is half the feel

Budget real time for these; they're load-bearing:
- Ball: asphalt thump (pitch ± 5%, volume by impact), rim clang, chain-net *zip* on swishes (the most satisfying sound in basketball — use a chain net).
- Sneaker squeaks on the defender's cuts and recovery sprints — this is how you *hear* that you froze him.
- Park ambience: distant traffic, birds, another court's game far away. Crowd "OOOH" on `DefenderFrozen` — the dopamine hit of the entire game.
