# Model Home — play-through verification (2026-11-08)

Driven through UnityMCP in the Unity 6000.3.28f1 editor on an Apple M3. Laps were completed with
`ModelHome.EditorTools.DebugTools.CompleteCurrentLap()`, which uses each task's real world object
(locks the door, latches the window, switches the lamp off, counts the figures), then the guard-post
clipboard. Screenshots come from the game camera (post-processing on); HUD-only checks were read from
the scene state.

## Results

| Check (spec / plan Task 12) | Result |
|---|---|
| Opening: black screen, clock `01:00`, fade in, input enabled | Pass |
| Early clipboard turn-in shows "Tasks remaining: 4", lap stays 1 (Review Focus 1) | Pass |
| Lap 1: all figures still, Mom faces the kitchen window | Pass (`lap1_*`) |
| Lap 1 -> 2: tasks tick via world objects; house resets (lamps on, doors unlocked) | Pass |
| Lap 2: Mom faces the hall, Daughter head tilt 8 deg, Dad breathing, eye tracking on | Pass (`sheet_lap2`) |
| Lap 2: eyes follow the torch — 25.6 deg off the face axis, gaze lands within 4.0 deg of the camera | Pass (measured) |
| Lap 3: Dad standing in the arch, NeckLong 0.35; Mom at the back-hall door, SmileWide 0.40; Daughter's chair empty | Pass (`sheet_lap3`) |
| Lap 3: first count shows "3 present." and ticks the task | Pass after fix (see below) |
| Ending: power cut, torch sputters, Daughter placed 0.60 m behind the guard, upright, facing them | Pass after fix (`ending_behind_you_lit`) |
| Ending: blink, hard cut, silence (AudioListener 0), 2 s, title "MODEL HOME", only room hum at 0.08 | Pass (`title_small`) |
| Console: no errors or warnings from ModelHome code | Pass (only editor notices: MCP WebSocket reconnect, "PlayerLoop called recursively" from screenshot capture, Metal "memoryless depth") |
| Performance: render time in the kitchen with the torch on | 5.1 ms (~195 fps headroom); 184 batches, 1.2 M tris incl. shadow passes |
| EditMode suite | 83/83 |

## Bugs found and fixed during the play-through

1. **Ending figure flipped upside down.** `DisplayFigure.PlaceFacing` used `Quaternion.FromToRotation`,
   which picks an arbitrary axis for an exact 180 deg turn (the normal case behind the guard). Now yaw-only.
   Test `PlaceFacing_TurnsFaceTowardTarget` gained an upright assertion (RED at -1.108 m, then GREEN).
2. **"3 present." buried.** The checklist's "Done: ..." toast replaced the count on the first use.
   `FigureCount` now reports the task before speaking. Verified in a second full run.
3. **Torch blew faces out to white at 1 m** (intensity 6 with inverse-square falloff) — now 2.2.
4. **Bare shoulders** — the neckline cut plane also stripped the shoulder tops; limited to a collar radius.
5. **Black wedges from closed lampshades** — open shades, shade meshes cast no shadow, shades glow.
6. **White tee clipping at 0.6 m** in the ending — tee albedo lowered to off-white.

## Not verifiable through MCP (for the user's own play test)

- Keyboard and mouse feel: WASD speed, mouse sensitivity, F (torch), E (use), Tab (clipboard).
- Esc releases the cursor, a click re-locks it (Review Focus 5).
- The glance-away blink timing and the Lap 2 inhale cue by ear (both covered by logic tests only).
- The Windows build on a real PC (Task 13).

## Screenshots (`img/`)

`first_look_spawn`, `living_room`, `lap1_dad`, `lap1_dad_face_3`, `sheet_lap1` (Daughter, Mom, Baby,
stairs), `sheet_lap2` (Dad's eyes, Mom facing the hall, Daughter's tilt), `sheet_lap3` (Dad in the arch,
Mom's smile, empty chair), `ending_behind_you_lit`, `title_small`.
