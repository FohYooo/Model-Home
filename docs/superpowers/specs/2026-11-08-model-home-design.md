# Model Home — Uncanny-Valley Horror Demo (Design)

Date: 2026-11-08
Status: Approved in conversation; awaiting written-spec review

## 1. Intent

A first-person horror game whose fear comes from the **uncanny valley**: things that are
*almost* human. The look is eerie and realistic. This document covers the **short demo** only —
one location, ~8 minutes — whose job is to prove the uncanny feeling works before the game grows.

Success for the demo:

- A player who walks through all three laps reports the figures felt "wrong" before anything
  overtly scary happened.
- The ending lands without a single conventional jump-scare sound sting doing the work.
- Runs smoothly in the Unity editor on the user's Mac (Apple Silicon), no console errors.
- Ships as a **Windows PC (x64) build that renders on the GPU**: Direct3D 12 with Direct3D 11
  fallback, GPU lightmapper for bakes, dedicated GPU preferred on hybrid-graphics laptops
  (added by the user on 2026-11-08).

Out of scope for the demo: upstairs, death/fail states, saving, menus beyond a title card,
builds for platforms other than Windows x64 (and the editor), voice acting.

## 2. Experience

### Setting

"Lot 14", a staged show home in a half-built suburb, at night. **Ground floor only**:

| Room | Contents |
|---|---|
| Foyer / guard post | Front door, folding table with clipboard + lamp, taped-off stairs ("Upstairs not staged") |
| Living room | Sofa, armchair (Dad), coffee table, lamp, framed family photo |
| Kitchen + dining (open plan) | Counter, fridge (hum), window (Mom), dining table + chairs (Daughter), bassinet (Baby), wall clock (tick) |
| Study | Desk, desk lamp, window |
| Back hall | Back door, utility window |

### Player

Contract night guard. Controls: **WASD + mouse** move/look, **F** flashlight, **E** interact,
**Tab** hold to view clipboard. Slow walk only (no sprint). No combat, no death.

### The figures

Four display figures ("the family") staged to make the house feel lived in:

| Figure | Base pose / place |
|---|---|
| Dad | Seated in living-room armchair, newspaper in lap |
| Mom | Standing at kitchen counter, mug in hand, facing window |
| Daughter | Seated at dining table |
| Baby | Lying in bassinet, blanket to the chin |

Look: smooth, idealised, slightly-too-perfect skin; realistic glass eyes with eyelids; neutral
catalogue clothing. Framed "family photos" of the figures hang in the house.

**Core uncanny rule:** *micro-life happens while you watch* (eyes tracking, breathing, blinks);
*macro-changes happen only when unseen* (a figure relocates, re-poses, changes proportion).

### Loop

Three laps. Each lap the clipboard lists 4–5 tasks placed so the route passes every figure.
Completing all tasks and interacting with the guard-post clipboard ends the lap: fade to black,
clock jumps (01:00 → 02:00 → 03:00), fade in.

| Lap | Tasks (example) | Figure staging |
|---|---|---|
| 1 Baseline (01:00) | Lock front door; check study window latch; turn off living-room lamp; lock back door | All perfectly still. On lap completion (unseen), Daughter's head tilt changes by ~8°. |
| 2 Alive (02:00) | Check kitchen window; turn off study lamp; check utility window; lock back door | Eye tracking ON for all figures (lagged, only when lit by flashlight or within 4 m). Dad breathing ON. Mom re-staged facing the hallway. Blink-when-glanced-away ON. Soft inhale audio cue near a figure once. |
| 3 Wrong (03:00) | Check kitchen window; "Confirm display figures present (4)" at dining table; turn off study lamp; lock back door | Dad `NeckLong` ~35%; Mom `SmileWide` ~40%; Dad and Mom relocated a few metres toward the player's route; Daughter's chair **empty** (figure hidden). The "confirm 4" task cannot be ticked honestly — interacting shows "3 present." and completes the task anyway. |
| Ending | — | At the post: house lights cut, flashlight sputters 2 s. Daughter is placed ~0.6 m behind the player, facing them, eyes locked. When the player turns and she is on screen ≥0.8 s: she blinks, hard cut to black, 2 s silence, title card "MODEL HOME". |

### Audio (synthesised, no downloads)

House hum (room tone), fridge buzz, wall-clock tick, footsteps (carpet/wood variants), soft
inhale, flashlight click, light-switch click, lock clunk, low drone for the ending.

## 3. Technical design

### Project and rendering

- Unity project: `unity-playground/` (Unity 6000.3.28f1). Unity MCP package
  `com.ivanmurzak.unity.mcp` already installed; used for scene setup, play testing, screenshots.
- Add `com.unity.render-pipelines.universal` 17.3.0. Create a URP asset + renderer with
  SSAO; assign in Graphics and Quality settings.
- Global post-process Volume: ACES tonemapping, film grain, vignette, slight chromatic
  aberration, cool/desaturated colour grade, fixed exposure slightly under.
- Lighting: dim practical lamps (Mixed), cold moonlight through windows (directional, low
  intensity), flashlight spot with soft shadows. Light probes for figures. Exponential fog, low.
- Input: legacy Input Manager (project `activeInputHandler: 0`).
- All new content under `Assets/ModelHome/{Scenes,Scripts,Models,Materials,Textures,Audio,Prefabs}`.
  Tests under `Assets/ModelHome/Tests/EditMode`.

### Asset pipeline

Blender 5.2.3 run headless (`Blender --background --python <script>`); scripts live in
`unity-playground/Tools/blender/` (outside `Assets/`), output FBX to `Assets/ModelHome/Models/`.

- `figure_gen.py` — parametric figure generator (one script, four figures):
  - Body: metaball blend → mesh → remesh + smooth (seamless idealised skin). Per-figure
    proportions (height, build) and pose (seated / standing / lying).
  - Clothing: offset shell meshes over torso/limbs, fabric materials.
  - Head: separate object with sculpted features (brow, nose, cheekbones, lips, chin) via
    vertex displacement of a subdivided base; pivot at the neck.
  - Eyes: separate eyeball objects (sclera + iris + cornea material slots), pivots at eye centres.
  - Eyelids: separate meshes per eye with a `Blink` shape key.
  - Shape keys exported as blend shapes: head `SmileWide`; body `NeckLong`, `Breathe`.
  - Object names are the contract with Unity: `Body`, `Head`, `Eye_L`, `Eye_R`, `Lid_L`, `Lid_R`.
- `house_gen.py` — ground-floor shell (walls, floors, ceilings, openings, skirting), world-scale
  UVs for tiling, plus furniture as separate named objects.
- Textures: CC0 PBR sets from Poly Haven (approved by user) for wood floor, plaster, fabric,
  carpet; skin and anything missing baked from Blender procedural shaders.
- Family photos: Blender renders of the figures, used as textures in picture frames.
- Audio: `Tools/audio/synth.py` (Python + numpy) writes WAVs into `Assets/ModelHome/Audio/`.

### Runtime scripts (`Assets/ModelHome/Scripts/`)

| Script | Responsibility | Depends on |
|---|---|---|
| `PlayerController` | CharacterController movement, mouse look, head bob, footsteps | — |
| `Flashlight` | Toggle; `Sputter(duration)`; exposes `IsLit(Vector3)` cone test | — |
| `Interactable` | Prompt text, `Interact()` → UnityEvent; enable/disable | — |
| `PlayerInteractor` | Raycast from camera, shows prompt, calls `Interact()` | `Interactable` |
| `ChecklistModel` | Pure C#: task list per lap, completion state, `AllComplete` | — |
| `Checklist` | MonoBehaviour wrapper + clipboard UI | `ChecklistModel` |
| `LapModel` | Pure C#: lap state machine (Lap1 → Lap2 → Lap3 → Ending), transitions | — |
| `LapDirector` | Drives `LapModel`; applies per-lap `LapStaging` assets; fade + clock | most below |
| `LapStaging` (ScriptableObject) | Data: tasks, figure placements, figure behaviour flags, blend-shape weights | — |
| `DisplayFigure` | Eye tracking (lagged), blink, breathing, head tilt, blend shapes, `TryRelocate(anchor)` only when unseen | `Visibility` |
| `Visibility` | Is a renderer in the camera frustum and not occluded (raycasts to bounds points) | — |
| `HouseLights` | Flicker, power cut | — |
| `EndingSequence` | Blackout → place Daughter behind player → wait until seen → blink → cut → title | `DisplayFigure`, `Flashlight`, `HouseLights` |

Pure-logic classes (`ChecklistModel`, `LapModel`) have no Unity dependencies so they are tested in
EditMode.

### Error handling

- Missing references (e.g. a figure without `Eye_L`) log a single clear error at `Awake` and
  disable only that feature, not the whole figure.
- `TryRelocate` never teleports a figure that is visible; it retries each frame until unseen.
- The ending's "wait until seen" has a 20 s fallback: if the player never turns, Daughter's breath
  audio plays from behind them, then the cut happens anyway.

### Testing and verification

- EditMode tests (Unity Test Framework — add `com.unity.test-framework` 1.6.0, not yet in this
  project): `ChecklistModel` (completion, per-lap reset),
  `LapModel` (transition order, no skipping, Ending is terminal).
- Play-mode verification via Unity MCP: enter play, drive each lap, capture screenshots of each
  figure per lap, confirm console is free of errors.

## 4. Constraints and notes

- Original setting and characters; no assets or likenesses from existing games.
- Git: repo root is `Game-development/` with no commits yet; nothing is committed without the
  user's go-ahead, and only as the FohYooo identity (see `Game-development/CLAUDE.md`).
