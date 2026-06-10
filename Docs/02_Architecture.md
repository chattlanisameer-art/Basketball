# 02 — Game Architecture

## 1. Folder structure

```
Assets/
├── Scenes/
│   └── Court.unity              # The one and only MVP scene
├── Scripts/
│   ├── Core/                    # Game flow — no physics here
│   │   ├── GameEvents.cs        # Static event bus (everything talks through this)
│   │   ├── GameManager.cs       # Bootstraps systems, holds references
│   │   ├── MatchManager.cs      # Score, game-to-11, win-by-2, match states
│   │   └── PossessionManager.cs # Check-ball, take-backs, out of bounds
│   ├── Player/
│   │   ├── VRHandTracker.cs     # Palm pose + velocity (hands OR controllers)
│   │   ├── PlayerMotionTracker.cs # Body speed/direction history (feeds Hesi + AI)
│   │   ├── MomentumSystem.cs    # Speed modulation from movement style
│   │   ├── HesiSystem.cs        # ★ THE core mechanic
│   │   ├── DribbleController.cs # Contact-bounce dribbling
│   │   └── ShootingSystem.cs    # Velocity-based shooting + timing accuracy
│   ├── Ball/
│   │   ├── BasketballPhysics.cs # Bounce, spin, Magnus lift, audio hooks
│   │   └── BallGrabHandler.cs   # Grab + throw with velocity buffering
│   ├── AI/
│   │   └── DefenderAI.cs        # Reactive 1v1 defender (reads motion history)
│   └── Court/
│       └── HoopScoreDetector.cs # Two-trigger scoring, clean/rim detection
├── Prefabs/
│   ├── Basketball.prefab
│   ├── Hoop.prefab
│   └── Defender.prefab
├── Materials/  Audio/  Settings/ (URP assets)
```

**Design rule:** systems never call each other directly for *game events* — they raise/listen on `GameEvents`. `HesiSystem` doesn't know `DefenderAI` exists; it just publishes numbers (`DefenderReactionPenalty`, `ShotBonus`) that anyone can read. This is what makes the project expandable to multiplayer later: replace local readers with networked ones, nothing else changes.

## 2. Core scripts list & responsibilities

| Script | Reads from | Produces |
|---|---|---|
| `VRHandTracker` | XR Hands subsystem / controllers | Palm position, rotation, smoothed velocity per hand |
| `PlayerMotionTracker` | XR Origin head position | Horizontal speed, direction, 2-second speed history ring buffer |
| `HesiSystem` | MotionTracker, DribbleController | `DefenderReactionPenalty`, `UnpredictabilityScore`, `ControlPenalty`, burst & shot-bonus windows |
| `MomentumSystem` | MotionTracker, HesiSystem | `SpeedMultiplier` for locomotion |
| `DribbleController` | HandTracker, ball | Dribble impulses, dribble events (count, location) |
| `BallGrabHandler` | HandTracker | Kinematic ball follow while held, throw release velocity |
| `ShootingSystem` | BallGrabHandler, HesiSystem, DefenderAI proximity | Shot release with accuracy blend |
| `BasketballPhysics` | Rigidbody contacts | Realistic restitution, backspin Magnus lift, bounce sounds |
| `DefenderAI` | MotionTracker (delayed!), ball state, HesiSystem penalty | Defender movement, contests, steal attempts |
| `HoopScoreDetector` | Ball triggers | `OnScore(points, clean)` event |
| `PossessionManager` | Score/OOB/steal events | Whose ball, check-ball state, take-back validation |
| `MatchManager` | PossessionManager, score events | Match flow, win condition |

## 3. Scene layout (Court.unity)

```
Court (root)
├── Environment
│   ├── CourtGround        # 15m × 11m half-court, BoxCollider, layer Court
│   │                      # PhysicMaterial: asphalt (bounciness 0, friction 0.9)
│   ├── ThreePointArc      # Visual line + invisible trigger volume (take-back check)
│   ├── Fence / Park props # Visual only, colliders on fence
│   └── Lighting           # 1 directional light (sun), baked GI for props
├── Hoop (prefab)
│   ├── Backboard          # BoxCollider, restitution 0.6
│   ├── Rim                # Torus approximated by 8 capsule colliders, restitution 0.55
│   ├── Net                # Cloth or simple skinned mesh (visual; tiny drag trigger)
│   ├── TriggerTop         # SphereCollider isTrigger, just above rim plane
│   └── TriggerBottom      # SphereCollider isTrigger, just below net
├── Basketball (prefab)    # Sphere r=0.121m, mass 0.62kg, layer Ball
├── XR Origin (player rig)
│   ├── Camera Offset → Main Camera
│   ├── LeftHand  → VRHandTracker, DribbleController(L)
│   └── RightHand → VRHandTracker, DribbleController(R)
├── Defender (prefab)      # CapsuleCollider + Rigidbody(kinematic), DefenderAI
├── SpawnPoints
│   ├── CheckBallSpot      # Top of the arc — possession starts here
│   ├── PlayerSpawn
│   └── DefenderSpawn
└── _Systems (empty GO)    # GameManager, MatchManager, PossessionManager,
                           # HesiSystem, MomentumSystem, PlayerMotionTracker
```

**Court dimensions (real half-court):** 15.24 m wide × 11 m deep, rim at **3.048 m**, free-throw line 4.6 m from backboard, three-point arc 6.75 m. Use real dimensions — VR players have real-world depth perception and a fake-scale court feels instantly wrong.

## 4. Hoop rim construction (important)

Don't use a mesh collider for the rim. Build it from **8 capsule colliders** arranged in a circle (radius 0.2286 m). Capsules give smooth, predictable bounces and are ~10× cheaper than a mesh collider at 200 Hz physics. The `Hoop.prefab` hierarchy in the scene layout above reflects this.

## 5. Execution order

Set **Project Settings → Script Execution Order**:

```
VRHandTracker        -100   (poses first)
PlayerMotionTracker   -90
HesiSystem            -80   (derives from motion before consumers)
MomentumSystem        -70
DribbleController     -50
DefenderAI            default
Everything else       default
```

Next: drop the scripts from `Assets/Scripts/` into your project — every file compiles standalone and is documented inline. Then read `Docs/03_GameplayDesign.md`.
