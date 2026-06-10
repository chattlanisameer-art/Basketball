# 01 — Full Unity Setup (Pico 4 Ultra)

Follow this top to bottom. Do not skip steps. By the end you'll have a project that builds to the headset and tracks your hands.

---

## 1. Unity version

**Use Unity 2022.3 LTS — specifically 2022.3.40f1 or newer 2022.3.x patch.**

Why this exact line:
- The PICO Unity Integration SDK officially supports 2022.3 LTS and it's the most stable combination with XR Interaction Toolkit + XR Hands.
- Unity 6 works with Pico's OpenXR runtime but the toolchain is less battle-tested; stay on 2022.3 LTS for the MVP, migrate later if needed.

**Install via Unity Hub** and check these modules during install:
- ✅ Android Build Support
- ✅ Android SDK & NDK Tools
- ✅ OpenJDK

(The Pico 4 Ultra is an Android device — every build is an Android APK.)

---

## 2. Create the project

1. Unity Hub → **New Project** → template: **3D (URP)**.
   - URP (Universal Render Pipeline) is mandatory for good performance on mobile VR chipsets (Snapdragon XR2 Gen 2 in the Pico 4 Ultra).
2. Name it `HesiVR`. Open it.

---

## 3. Packages

Open **Window → Package Manager** and install, in this order:

| Package | Version | Why |
|---|---|---|
| **XR Plugin Management** | 4.4.x | Manages which XR runtime boots |
| **XR Interaction Toolkit** | 2.5.x (enable *Starter Assets* sample) | Grab/throw interactions, XR rig |
| **XR Hands** | 1.4.x | Hand-tracking subsystem (26-joint skeleton) |
| **Input System** | 1.7.x | Modern input (installed as a dependency; confirm it's there) |
| **OpenXR Plugin** | 1.10.x | The runtime standard Pico 4 Ultra speaks |

Then install the **PICO Unity Integration SDK**:

1. Download the latest **PICO Unity Integration SDK (3.x)** from the [PICO developer portal](https://developer.picoxr.com/resources/).
2. Package Manager → **+ → Add package from tarball…** → select the downloaded `.tgz`.
3. When prompted, let it enable the **PICO OpenXR feature set**.

> **Why OpenXR + Pico SDK together?** OpenXR is the standard API; the Pico SDK supplies the device-specific OpenXR features (hand tracking permission, foveated rendering, 90 Hz mode) on top of it. This keeps your code portable to Quest later.

When the Input System package asks to restart the editor and switch the active input backend, click **Yes**.

---

## 4. Project Settings (exact values)

**Edit → Project Settings…**

### XR Plug-in Management
- **Android tab** → check **OpenXR**.
- Under **OpenXR → Android**:
  - Interaction profiles: add **PICO 4 Touch Controller Profile** (from the Pico SDK) and **Hand Interaction Profile**.
  - Features: enable **PICO XR Features**, **Hand Tracking Subsystem**, **Meta/Pico Hand Tracking Aim** if present.

### Player → Android
| Setting | Value | Why |
|---|---|---|
| Color Space | **Linear** | Required by URP + correct lighting |
| Graphics API | **Vulkan only** (remove GLES3) | Best perf on XR2 Gen 2 |
| Minimum API Level | **Android 10 (API 29)** | Pico 4 Ultra baseline |
| Target API Level | Automatic (highest installed) | |
| Scripting Backend | **IL2CPP** | Required for ARM64 |
| Target Architectures | **ARM64 only** | Pico is 64-bit |
| Multithreaded Rendering | ✅ On | Frees main thread for physics |
| Texture Compression | **ASTC** | Mobile standard |

### Quality (URP)
- Use the **Performant** URP asset for Android.
- MSAA: **4x** (cheap on tiled mobile GPUs, kills jaggies — huge for VR comfort).
- Shadows: one directional light, soft shadows OFF for MVP, shadow distance 20 m.
- Render scale: **1.0** (drop to 0.9 only if you can't hold frame rate).

### Time (critical for ball feel)
- **Fixed Timestep: `0.005`** (200 Hz physics).
  A basketball moving 15 m/s travels 7.5 cm per physics step at 200 Hz — at the default 50 Hz it would tunnel through your hand and the rim. This single setting is the difference between "floaty arcade ball" and "real ball".
- Maximum Allowed Timestep: `0.05`.

### Physics
- Default Solver Iterations: **12**, Velocity Iterations: **4**.
- Default Contact Offset: `0.005`.
- Enable **Enhanced Determinism** OFF (not needed, costs perf).
- Layer setup (create these in Tags & Layers):
  - `Ball`, `PlayerHands`, `Defender`, `Court`, `HoopTrigger`
  - Collision matrix: `HoopTrigger` collides with `Ball` only. `PlayerHands` collides with `Ball` only (not `Defender` — no hand-fouls physics jank in MVP).

---

## 5. Scene rig & input setup

1. Delete the default `Main Camera`.
2. **GameObject → XR → XR Origin (VR)** — this gives you `XR Origin → Camera Offset → Main Camera + LeftHand/RightHand Controller`.
3. On **XR Origin**: set Tracking Origin Mode = **Floor**.
4. Add an **Input Action Manager** component to XR Origin and assign the `XRI Default Input Actions` asset (from the XR Interaction Toolkit *Starter Assets* sample).
5. Add the **XR Hands** sample "HandVisualizer" prefab (Package Manager → XR Hands → Samples) under Camera Offset for debug hand rendering.
6. On both hand controllers, the default `XR Direct Interactor` will be replaced by our custom scripts (see `Docs/02_Architecture.md`), but keep `XRController` / `ActionBasedController` components — they feed poses.

**Hand tracking vs controllers:** the game supports both. `VRHandTracker.cs` (in `Assets/Scripts/Player/`) reads the XR Hands subsystem when hands are tracked and falls back to controller poses otherwise. Dribbling feels dramatically better with bare-hand tracking; shooting works great with either.

7. **AndroidManifest hand-tracking permission**: the Pico SDK adds `<uses-permission android:name="com.picovr.permission.HAND_TRACKING"/>` automatically when the Hand Tracking feature is enabled. Verify under **Project Settings → XR Plug-in Management → OpenXR → PICO features**.

---

## 6. Build & run checklist

1. **File → Build Settings** → switch platform to **Android**.
2. Add your scene (`Assets/Scenes/Court.unity`) to *Scenes in Build*.
3. Put the Pico 4 Ultra in **Developer Mode** (Settings → General → about → tap version 7×, then enable USB debugging).
4. Connect USB-C, accept the debugging prompt in-headset.
5. **Build and Run.** First build takes ~10 min (IL2CPP); subsequent builds ~2 min.

If the app launches but hands don't track: in-headset **Settings → Motion Tracking → Hand Tracking → ON**.

---

## 7. Frame-rate target

- Target **90 Hz** (request via `Unity.XR.OpenXR` display subsystem or Pico SDK `PXR_Manager` display refresh setting).
- Budget: **11 ms/frame**. Physics at 200 Hz costs ~1.5 ms with one ball + two characters — fine.
- Enable **Fixed Foveated Rendering (Level: Low)** via the Pico SDK feature. Free perf, invisible in the periphery.

Next: `Docs/02_Architecture.md`.
