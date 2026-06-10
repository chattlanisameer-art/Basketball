# 03 — Gameplay Design

## 1. How a 1v1 match flows, start to finish

There are no menus mid-game. The entire match is played through physical actions:

1. **Spawn.** You appear at the top of the arc with the ball resting at the check spot. The defender stands two steps inside the arc. Ambient park sounds; no UI.
2. **Check ball.** You pick the ball up (`BallGrabHandler`). The moment you take your **first dribble**, `PossessionManager` flips play to live and `GameEvents.PlayStarted` fires — the defender steps up. The dribble *is* the start button.
3. **The possession.** You attack. Everything is your real body:
   - Dribble to keep your handle alive (holding the ball while walking will be a travel/carry rule post-MVP; in the MVP the over-dribble and steal systems already punish standing still).
   - Set up the defender with speed changes — see the Hesi loop below.
   - Get to your spot and shoot with your real arm.
4. **Resolution.** One of four things ends the possession:
   - **Make** → `HoopScoreDetector` fires `Scored(points, clean)` → loser's ball, reset to check spot.
   - **Miss** → live rebound. MVP: ball stays live; whoever it ends up near... in practice you chase it. (Defensive rebounds by the AI come post-MVP; today a long miss usually rolls out → possession resets.)
   - **Steal** → defender pokes the ball loose, brief beat, reset.
   - **Out of bounds** → last touch loses it, reset.
5. **Repeat to 11.** 1s and 2s, win by 2. Game point gets an audio call-out.
6. **Game over.** Crowd reaction, 4-second cooldown, then it automatically "runs it back" — grab the ball and check up again.

## 2. How scoring works

- Inside the arc = **1 point**. Behind the arc (6.75 m) = **2 points** — measured from where the ball *left your hand*, captured by `HoopScoreDetector` on the `BallReleased` event.
- A make only counts via the **two-trigger gate**: ball enters the top trigger moving downward, then the bottom trigger within 0.8 s. Rim-rollers that pop out never fire; sideways clips never fire.
- **Clean vs rim** is tracked (`RimContactRelay`). A swish currently just sounds different — post-MVP it feeds the trick/style system.
- After a turnover you must **take it back** beyond the arc before your bucket counts (`PossessionManager.ScoreIsValid`).

## 3. How possession works

Possession is a state in `PossessionManager`, changed only by game events — never by a menu:

| Event | New state |
|---|---|
| Score | Loser's ball (or make-it-take-it via inspector toggle), reset to check spot |
| Steal | Defender's ball → MVP: returns to you after 2 s (AI is defense-only for now) |
| Out of bounds | Other side's ball, reset to check spot |

The **check spot** is sacred: every dead ball returns there, floating at hand height, waiting. This gives the game a natural breathing rhythm — sprint, battle, resolve, walk back, breathe, check up — which doubles as the VR comfort recovery period.

## 4. How defense works in VR space

The defender (`DefenderAI`) occupies real space on the court and plays positional defense:

- **He guards the line, not you.** He stays on the segment between you and the rim at a 1.1 m gap. To score you must *displace* him — make his stale picture of you wrong.
- **His eyes have latency.** He acts on your position/velocity from `ReactionTime` seconds ago (base 0.28 s + your earned hesi penalty up to +0.45 s). This is the entire defensive model and it's honest: you can't cheese him with wiggle-jitter (variance only builds penalty when combined with a real hesi pattern), but a genuine rocker step beats him exactly the way it beats a human.
- **He punishes lazy handles.** Low, slow dribbles within his 0.8 m reach draw steal pokes. High-frequency rhythm dribbles and fast crossovers are safe.
- **He contests shots.** When you slow down in scoring range he closes out with a hand up; defender hand within 0.9 m of your release shaves up to 50% off shot quality. Shooting over a contest with bad form bricks. Creating separation first (hesi burst → shot-bonus window) is the designed counter.
- **He can get beat.** If your actual direction diverges >55° from his perceived direction while you're at speed and past him, he enters Recovering — a real sprint back, not a teleport. A max-strength burst against a built-up penalty triggers **Frozen** (the ankle-breaker): 0.55 s with his feet in cement, crowd reaction, wide-open look.

Physical safety note: the defender is kinematic and never collides with the player rig — he yields position rather than body-checking you. Defense in VR must pressure space, not push the human.

## 5. The Hesi loop in play (the designed possession)

```
build rhythm (momentum ↑, unpredictability from real variance ↑)
        ↓
hard deceleration with the ball  →  HesiState.Hesitating
        ↓                            defender penalty builds 0 → 0.45s
hold the freeze 0.3–0.6 s            (hold > 1.1 s = defender resets, no harm)
        ↓
explode  →  HesiState.Bursting
        speed boost ×1.35 stacks on momentum ×1.18
        defender guards a 600 ms-old ghost → beaten/frozen
        ↓
2-second SHOT BONUS window — your open look, release is +25% forgiving
        ↓
finish at the rim or pull up — both are the "right" play, defense-dependent
```

Counter-pressure that keeps it honest: over-dribbling to fish for a freeze raises `ControlPenalty` (ball squirts off line, weaker catch assist, −40% on shot quality) and feeds the steal system. The optimal play is *decisive* — exactly streetball.
