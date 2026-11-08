# Model Home Demo Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A playable ~8-minute first-person uncanny-valley horror demo ("Model Home") in Unity 6 URP: three checklist laps through a staged show home whose display figures drift from still → subtly alive → wrong, ending with the Daughter figure behind the player.

**Architecture:** Assets are generated headlessly by Blender 5.2 Python scripts (figures, house, family photo) and Python/numpy (audio); textures are CC0 Poly Haven sets. The Unity scene is *built by an editor script* (`ModelHomeSceneBuilder`) so it is reproducible; the Unity MCP runs that builder, the tests, and play-mode screenshots. Runtime code is small single-purpose MonoBehaviours around pure-C# logic/math classes that are EditMode-tested.

**Tech Stack:** Unity 6000.3.28f1, URP 17.3.0, Unity Test Framework 1.6.0 (NUnit), legacy Input Manager, legacy uGUI `Text`; Blender 5.2.3 (`bpy`); Python 3.13 + numpy + Pillow; Unity MCP `com.ivanmurzak.unity.mcp` 1.7.1.

**Spec:** `docs/superpowers/specs/2026-11-08-model-home-design.md`

## Global Constraints

- Project root: `/Users/thomasschrauwen/Projects/Game-development/unity-playground`. All paths below are relative to it.
- Unity 6000.3.28f1; URP `com.unity.render-pipelines.universal` 17.3.0; `com.unity.test-framework` 1.6.0.
- Input: legacy Input Manager only (`activeInputHandler: 0`) — `Input.GetAxis`, `Input.GetKey`.
- All new Unity content under `Assets/ModelHome/{Scenes,Scripts,Editor,Models,Materials,Textures,Audio,Prefabs,Staging,Tests}`.
- Namespaces: runtime `ModelHome`, editor `ModelHome.EditorTools`, tests `ModelHome.Tests`.
- Assembly definitions: `ModelHome.Runtime` (Scripts), `ModelHome.Editor` (Editor, editor-only), `ModelHome.Tests.EditMode` (Tests/EditMode, editor-only, refs Runtime + Editor + nunit).
- Blender: `/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python <script> -- <args>`. Scripts in `Tools/blender/`.
- Python helper scripts run with `python3 -I`.
- Original content only: no names, likenesses or assets from existing games.
- Target: Windows x64 standalone player rendering on the GPU (Direct3D12, Direct3D11 fallback); the Mac editor is the dev environment (user requirement 2026-11-08).
- No death/fail state, no sprint, no menus beyond the title card (spec §1 out-of-scope list).
- Git: commits only as `FohYooo` / `353847037+FohYooo@users.noreply.github.com` (local config). **No `Co-Authored-By` trailers** (workspace `CLAUDE.md` forbids crediting other GitHub accounts). Never push.
- Figure object-name contract (Blender → Unity): root empty `Figure_<Variant>`; children `Body`, `Clothes`, `Head`; `Head` children `Eye_L`, `Eye_R`, `Lid_L`, `Lid_R`, `Hair` (absent on Baby), and optional `Prop` under root.
- Blend-shape contract: `Body` and `Clothes` have `Breathe`, `NeckLong`; `Head` has `SmileWide`; `Lid_L`/`Lid_R` have `Blink` (0 = open, 100 = closed). `NeckLong` = 100 lifts the neck top by **0.08 m**.
- Figure variants (one FBX each, `Assets/ModelHome/Models/Figures/Figure_<Variant>.fbx`): `Dad_Seated`, `Dad_Standing`, `Mom_Standing`, `Daughter_Seated`, `Daughter_Standing`, `Baby_Lying`.

## Review Focus

1. **Clipboard turned in early** — interacting with the guard-post clipboard with tasks left must show "Tasks remaining: N" and must not advance the lap. Pinned in Task 2 (`LapModel` test) and Task 9 (`GuardClipboard` toast).
2. **Ending spawn inside geometry** — if a wall/furniture is behind the player, Daughter must be placed at the first clear candidate, never inside a collider. Pinned in Task 11 (`EndingPlacement.Candidates` test + capsule check).
3. **Gaze at extreme angles** — player directly behind/above a figure must not make eyes roll back into the head; eyes clamp to 30° from face-forward. Pinned in Task 8 (`GazeMath` tests).
4. **Out-of-lap / repeated interactions** — using an object whose task isn't in this lap, or a task already done, must still give feedback but never double-count or tick anything. Pinned in Task 2 (`ChecklistModel` tests) and Task 7 (`Interactable` cooldown).
5. **Cursor lock** — Esc must release the cursor and pause look; clicking the Game view re-locks. Pinned in Task 7 verification step.

---

### Task 0: Repository and prerequisites

**Files:**
- Modify: `.gitignore`
- Create: `.git/` (new repo in `unity-playground/`, mirroring `splash-trial/`)

- [ ] **Step 1: User approves the Unity MCP server** — the user runs `claude` once in `unity-playground/` (or approves via the app prompt) so `ai-game-developer` leaves "Pending approval". Verify: `claude mcp list` shows `ai-game-developer … ✔ Connected`. If not approved, every "via MCP" step below uses its listed batchmode fallback (requires the Unity editor to be closed).

- [ ] **Step 2: Init repo with local identity**

```bash
git init -b main
git config user.name FohYooo
git config user.email 353847037+FohYooo@users.noreply.github.com
```
Expected: `git rev-parse --show-toplevel` prints `.../unity-playground`.

- [ ] **Step 3: Extend `.gitignore`** — append: `MemoryCaptures/`, `.vs/`, `.idea/`, `My project/` (separate URP template project, not part of this repo), `Tools/blender/out/`, `Assets/ModelHome/Textures/PolyHaven/**/*.jpg`, `Assets/ModelHome/Textures/PolyHaven/**/*.png` (payloads are re-fetchable; their `.meta` files ARE committed so material GUIDs survive).

- [ ] **Step 4: Commit**

```bash
git add .gitignore .mcp.json Assets Packages ProjectSettings docs
git commit -m "chore: initial unity-playground repo with Model Home spec and plan"
```

---

### Task 1: URP foundation, assemblies, post-processing profile

**Files:**
- Modify: `Packages/manifest.json` (add URP 17.3.0, test-framework 1.6.0)
- Create: `Assets/ModelHome/Scripts/ModelHome.Runtime.asmdef`, `Assets/ModelHome/Editor/ModelHome.Editor.asmdef`, `Assets/ModelHome/Tests/EditMode/ModelHome.Tests.EditMode.asmdef`
- Create: `Assets/ModelHome/Editor/ProjectSetup.cs`
- Test: `Assets/ModelHome/Tests/EditMode/ProjectSetupTests.cs`

**Interfaces:**
- Produces: `ModelHome.EditorTools.ProjectSetup.Run()` (idempotent; `[MenuItem("Model Home/1. Project Setup")]`); assets `Assets/ModelHome/Settings/MH_URP.asset`, `MH_Renderer.asset`, `MH_PostProfile.asset` (VolumeProfile).

- [ ] **Step 1: Add packages** to `Packages/manifest.json` dependencies: `"com.unity.render-pipelines.universal": "17.3.0"`, `"com.unity.test-framework": "1.6.0"`. Via MCP `package-add` or by editing the file and letting the editor resolve. Verify: console has no package errors.

- [ ] **Step 2: Create the three asmdefs** (references per Global Constraints; Runtime also refs `Unity.RenderPipelines.Universal.Runtime`, `Unity.RenderPipelines.Core.Runtime`; Editor refs those plus Runtime; Tests set `"defineConstraints": ["UNITY_INCLUDE_TESTS"]`, `"includePlatforms": ["Editor"]`, `"overrideReferences": true`, `"precompiledReferences": ["nunit.framework.dll"]`, refs `UnityEngine.TestRunner`, `UnityEditor.TestRunner`).

- [ ] **Step 3: Write the failing test**

```csharp
[Test] public void DefaultPipeline_IsMhUrpAsset() {
    var rp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
    Assert.IsNotNull(rp); Assert.AreEqual("MH_URP", rp.name);
    for (int i = 0; i < QualitySettings.names.Length; i++) Assert.AreSame(rp, QualitySettings.GetRenderPipelineAssetAt(i));
}
[Test] public void Renderer_HasSsaoFeature() {
    var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/ModelHome/Settings/MH_Renderer.asset");
    Assert.IsTrue(data.rendererFeatures.Any(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion"));
    Assert.AreEqual(RenderingMode.ForwardPlus, data.renderingMode);
}
[Test] public void PostProfile_HasSpecEffects() {
    var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/ModelHome/Settings/MH_PostProfile.asset");
    Assert.IsTrue(p.TryGet<Tonemapping>(out var t)); Assert.AreEqual(TonemappingMode.ACES, t.mode.value);
    Assert.IsTrue(p.Has<FilmGrain>()); Assert.IsTrue(p.Has<Vignette>());
    Assert.IsTrue(p.Has<ChromaticAberration>()); Assert.IsTrue(p.TryGet<ColorAdjustments>(out var c));
    Assert.Less(c.postExposure.value, 0f);
}
```

- [ ] **Step 4: Run tests — expect FAIL** (assets missing). Via MCP `tests-run` (EditMode). Fallback: `/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml` and read `Logs/editmode.xml`.

- [ ] **Step 5: Implement `ProjectSetup.Run()`** — creates `UniversalRendererData` (Forward+), adds SSAO feature (type is resolved by name `UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion` via reflection if not public; `AssetDatabase.AddObjectToAsset`), creates `UniversalRenderPipelineAsset.Create(renderer)` (HDR on, MSAA off, soft shadows on, shadow distance 25, additional-light shadows on, 2048 atlas), assigns to `GraphicsSettings.defaultRenderPipeline` and every quality level. Creates `MH_PostProfile` with exact values: Tonemapping ACES; FilmGrain type `Medium3` intensity 0.35 response 0.8; Vignette intensity 0.38 smoothness 0.45; ChromaticAberration 0.08; ColorAdjustments postExposure −0.4, contrast 12, saturation −25, colorFilter `#D8E4F0`; WhiteBalance temperature −12; Bloom threshold 1.1 intensity 0.2. Sets `PlayerSettings.colorSpace = Linear`.

- [ ] **Step 6: Run `ProjectSetup.Run()` then tests — expect PASS.** MCP: `script-execute` with `ModelHome.EditorTools.ProjectSetup.Run();`. Fallback: `-batchmode -quit -executeMethod ModelHome.EditorTools.ProjectSetup.Run`.

- [ ] **Step 7: Commit** — `git add Packages Assets/ModelHome ProjectSettings && git commit -m "feat: URP foundation, assemblies and post-processing profile"`

---

### Task 2: Checklist and lap logic (pure C#)

**Files:**
- Create: `Assets/ModelHome/Scripts/Logic/ChecklistModel.cs`, `Assets/ModelHome/Scripts/Logic/LapModel.cs`
- Test: `Assets/ModelHome/Tests/EditMode/ChecklistModelTests.cs`, `LapModelTests.cs`

**Interfaces:**
- Produces:
  - `readonly struct ChecklistTask { string Id; string Label; }` (ctor `(string id, string label)`)
  - `class ChecklistModel { ChecklistModel(IEnumerable<ChecklistTask>); IReadOnlyList<ChecklistTask> Tasks; bool IsDone(string id); bool Complete(string id); int RemainingCount; bool AllComplete; void Load(IEnumerable<ChecklistTask>); event Action<string> TaskCompleted; }`
  - `enum LapPhase { Lap1, Lap2, Lap3, Ending }`; `class LapModel { LapPhase Phase; bool TryAdvance(bool checklistComplete); event Action<LapPhase> PhaseChanged; }`

- [ ] **Step 1: Write the failing tests**

```csharp
static ChecklistModel M() => new(new[]{ new ChecklistTask("a","A"), new ChecklistTask("b","B") });
[Test] public void New_NotComplete() { var m=M(); Assert.IsFalse(m.AllComplete); Assert.AreEqual(2,m.RemainingCount); }
[Test] public void CompleteAll_AllComplete() { var m=M(); Assert.IsTrue(m.Complete("a")); Assert.IsTrue(m.Complete("b")); Assert.IsTrue(m.AllComplete); }
[Test] public void UnknownId_ReturnsFalse_NoChange() { var m=M(); Assert.IsFalse(m.Complete("zzz")); Assert.AreEqual(2,m.RemainingCount); }
[Test] public void DoubleComplete_ReturnsFalse_EventOnce() { var m=M(); int n=0; m.TaskCompleted+=_=>n++; m.Complete("a"); Assert.IsFalse(m.Complete("a")); Assert.AreEqual(1,n); }
[Test] public void Load_ResetsState() { var m=M(); m.Complete("a"); m.Load(new[]{ new ChecklistTask("a","A") }); Assert.IsFalse(m.IsDone("a")); Assert.AreEqual(1,m.RemainingCount); }

[Test] public void Lap_StartsAtLap1() => Assert.AreEqual(LapPhase.Lap1, new LapModel().Phase);
[Test] public void Lap_IncompleteChecklist_DoesNotAdvance() { var l=new LapModel(); Assert.IsFalse(l.TryAdvance(false)); Assert.AreEqual(LapPhase.Lap1,l.Phase); }
[Test] public void Lap_AdvancesInOrder() { var l=new LapModel(); var seen=new List<LapPhase>(); l.PhaseChanged+=seen.Add;
    l.TryAdvance(true); l.TryAdvance(true); l.TryAdvance(true);
    CollectionAssert.AreEqual(new[]{LapPhase.Lap2,LapPhase.Lap3,LapPhase.Ending}, seen); }
[Test] public void Lap_EndingIsTerminal() { var l=new LapModel(); for(int i=0;i<3;i++) l.TryAdvance(true); Assert.IsFalse(l.TryAdvance(true)); Assert.AreEqual(LapPhase.Ending,l.Phase); }
```

- [ ] **Step 2: Run — expect FAIL** (types not defined). Same test command as Task 1 Step 4.
- [ ] **Step 3: Implement `ChecklistModel` and `LapModel`** per Interfaces (no `UnityEngine` usage).
- [ ] **Step 4: Run — expect PASS.**
- [ ] **Step 5: Commit** — `feat: checklist and lap state logic`

---

### Task 3: Textures and material library

**Files:**
- Create: `Tools/textures/fetch_polyhaven.py`, `Tools/textures/make_iris.py`
- Create: `Assets/ModelHome/Editor/AssetImportRules.cs` (AssetPostprocessor for textures; audio rules added in Task 6), `Assets/ModelHome/Editor/MaterialLibrary.cs`
- Test: `Assets/ModelHome/Tests/EditMode/MaterialLibraryTests.cs`

**Interfaces:**
- Produces: URP Lit materials in `Assets/ModelHome/Materials/` whose **names match Blender material names** (the remap key): `M_Floor_Wood`, `M_Wall_Plaster`, `M_Ceiling`, `M_Fabric_Sofa`, `M_Rug`, `M_Cabinet_Wood`, `M_Counter_Marble`, `M_Ground_Dirt`, `M_Door_Paint`, `M_Skirting_Paint`, `M_Metal`, `M_Glass`, `M_Lampshade`, `M_Paper`, `M_Photo_Family`, `M_Skin_Dad`, `M_Skin_Mom`, `M_Skin_Daughter`, `M_Skin_Baby`, `M_Sclera`, `M_Iris_GreyBlue`, `M_Iris_Brown`, `M_Iris_BlueGrey`, `M_Iris_DarkGrey`, `M_Cornea`, `M_Hair_Dad`, `M_Hair_Mom`, `M_Hair_Daughter`, `M_Cloth_Shirt`, `M_Cloth_Trousers`, `M_Cloth_Knit`, `M_Cloth_Denim`, `M_Cloth_Tee`, `M_Cloth_Blanket`, `M_Newspaper`, `M_Mug`, `M_Ink` (`#141414`, sign text / clock hands), `M_Tape` (`#E8C320`, stair warning tape).
- Produces: `MaterialLibrary.BuildAll()` (`[MenuItem("Model Home/2. Build Materials")]`), idempotent.

- [ ] **Step 1: Write failing test** — `AllContractMaterialsExist`: for each name above, `AssetDatabase.LoadAssetAtPath<Material>($"Assets/ModelHome/Materials/{n}.mat")` is not null and `shader.name == "Universal Render Pipeline/Lit"`. `NormalMapsImportedAsNormal`: every texture path containing `_nor_gl_` has `TextureImporter.textureType == NormalMap`. `MaskMapsAreLinear`: every `_mask` texture has `sRGBTexture == false`. `CorneaIsTransparent`: `M_Cornea` has `_Surface == 1`.
- [ ] **Step 2: Run — expect FAIL.**
- [ ] **Step 3: Implement `fetch_polyhaven.py`** — for each id, GET `https://api.polyhaven.com/files/<id>` (User-Agent `model-home-devtool`), download 2K jpg `Diffuse`, `nor_gl`, `Rough`, `AO` to `Assets/ModelHome/Textures/PolyHaven/<id>/`; then with Pillow write `<id>_mask_2k.png` (RGBA: R=0 metallic, G=AO, B=0, A=255−rough → URP smoothness). Skip files already present. Set mapping:

  | Material | Poly Haven id | Tiling (m per repeat) |
  |---|---|---|
  | M_Floor_Wood | `laminate_floor_02` | 2.0 |
  | M_Wall_Plaster | `plaster_grey_04` (tint `#E9E4DA`) | 2.0 |
  | M_Ceiling | `ceiling_interior` | 2.0 |
  | M_Fabric_Sofa | `fabric_pattern_05` (tint `#9AA3A8`) | 0.6 |
  | M_Rug | `dirty_carpet` (tint `#C9C2B5`) | 1.5 |
  | M_Cabinet_Wood | `kitchen_wood` | 1.0 |
  | M_Counter_Marble | `marble_01` | 1.5 |
  | M_Ground_Dirt | `dirt` | 4.0 |
  | M_Cloth_Denim | `denim_fabric` | 0.4 |
  | M_Cloth_Shirt / Knit / Trousers / Tee / Blanket | `fabric_pattern_07` tinted `#AFC4D9` / `#E8E0D0` / `#3A3D42` / `#F2F2F0` / `#F4F1EA` | 0.4 |

  Tiling is applied in Blender UVs (world-scale, Task 5), so Unity materials keep tiling 1.
- [ ] **Step 4: Implement `make_iris.py`** — numpy radial-fiber iris (1024², dark limbal ring, pupil radius 0.18) for the four iris colours → `Assets/ModelHome/Textures/Eyes/iris_<colour>.png`.
- [ ] **Step 5: Implement `AssetImportRules`** (normal maps by `_nor_gl_`, masks linear, max size 2048) and `MaterialLibrary.BuildAll()` — Lit materials with base/normal/mask maps; non-textured values: skin base `#E8D3C4` (Dad `#E2C8B6`, Baby `#F0DCD0`), smoothness **0.55** (waxy display-figure sheen); sclera `#EEEAE2` smoothness 0.6; cornea transparent alpha 0.08 smoothness 0.97; hair smoothness 0.7 (moulded, glossy) colours Dad `#3B2E26`, Mom `#8C7355`, Daughter `#6B5440`; metal metallic 1 smoothness 0.6; glass transparent alpha 0.15; lampshade `#F3E9D2` with emission off; `M_Photo_Family` base map `Assets/ModelHome/Textures/Photos/photo_family.png` if present (Task 4 creates it).
- [ ] **Step 6: Run `python3 -I Tools/textures/fetch_polyhaven.py && python3 -I Tools/textures/make_iris.py`, then `MaterialLibrary.BuildAll()` (MCP `script-execute`), then tests — expect PASS.**
- [ ] **Step 7: Commit** — `feat: Poly Haven textures, iris textures and material library` (jpg/png payloads are gitignored; metas + mats + scripts committed).

---

### Task 4: Figure generator (Blender) + family photo

**Files:**
- Create: `Tools/blender/common.py`, `Tools/blender/figure_gen.py`, `Tools/blender/verify_figures.py`, `Tools/blender/photo_render.py`
- Create: `Assets/ModelHome/Editor/ModelImportRules.cs`
- Test: `Assets/ModelHome/Tests/EditMode/FigureImportContractTests.cs`

**Interfaces:**
- Consumes: material names from Task 3.
- Produces: six FBX at `Assets/ModelHome/Models/Figures/Figure_<Variant>.fbx` honouring the object-name and blend-shape contracts (Global Constraints); `Assets/ModelHome/Textures/Photos/photo_family.png`; previews `Tools/blender/out/preview_<Variant>.png`.
- `common.py`: `reset_scene()`, `export_fbx(root_obj, path)` (selection = root + descendants; `apply_scale_options='FBX_SCALE_ALL'`, `axis_forward='-Z'`, `axis_up='Y'`, `bake_space_transform=True`, `use_mesh_modifiers=False`, `add_leaf_bones=False`, `mesh_smooth_type='FACE'`), `get_or_make_material(name, rgba)`.

- [ ] **Step 1: Write `verify_figures.py` (the test)** — imports each FBX into an empty scene and asserts: root named `Figure_<Variant>`; required children exist with correct parents; shape keys present per contract on each mesh; `Body` tri count ≤ 30 000, `Head` ≤ 12 000; eyes' world centres are symmetric (|x_L + x_R| < 2 mm) and the iris faces the figure front (iris material centroid is on the −Y side of each eyeball in figure space); bounding-box height within ±3 % of the variant target. Exits with code 1 and a list of failures otherwise.
- [ ] **Step 2: Run — expect FAIL** (no FBX): `Blender --background --factory-startup --python Tools/blender/verify_figures.py`.
- [ ] **Step 3: Implement `figure_gen.py`** with CLI `-- --variant <name|all> --out <dir>`:
  - **Figure table:** height Dad 1.83, Mom 1.68, Daughter 1.32, Baby 0.62 (length); head-height ratio adult 1/7.5, child 1/6, baby 1/4; build (shoulder width) Dad 0.46, Mom 0.38, Daughter 0.30, Baby 0.17; iris Dad GreyBlue, Mom Brown, Daughter BlueGrey, Baby DarkGrey; hair Dad short cap, Mom shoulder-length shell, Daughter chin-length bob, Baby none; clothes Dad shirt+trousers, Mom knit+trousers, Daughter denim pinafore over tee, Baby blanket (as `Prop`, covers to chin); props Dad newspaper on lap (seated only), Mom mug in right hand.
  - **Skeleton/pose:** joints `pelvis, spine, chest, neck, head_base, shoulder_L/R, elbow_L/R, wrist_L/R, hand_L/R, hip_L/R, knee_L/R, ankle_L/R, toe_L/R`, positions for a 1.75 m reference figure in figure space (origin on floor, **front = −Y**, Z up), per pose `standing`, `seated` (pelvis at Z 0.47, thighs horizontal, knees 90°, hands on thighs; Daughter's forearms forward onto table height 0.74), `lying` (on back, face +Z, head toward +X); scaled by height and build.
  - **Body:** metaball capsules along each bone (radii table per segment, scaled by build) → convert to mesh → voxel remesh 0.008 m → Corrective Smooth (factor 0.5, 10 iterations) → decimate to ≤ 30k tris. **Apply all modifiers before adding shape keys.**
  - **Clothes:** copy body faces inside region masks (shirt/knit/tee = chest+arms to elbow/wrist, trousers = pelvis+legs, pinafore = chest+pelvis to knee) → push along normals 6 mm → assign cloth material; double-sided in Unity.
  - **Head:** subdivided UV sphere scaled to head proportions (w 0.15 / h 0.22 / d 0.19 for adults, scaled per figure) with Gaussian displacement features in head space: brow ridge, eye sockets (inward 9 mm), nose ridge + tip (out 22 mm adult), cheekbones, upper/lower lip, chin, jaw taper; pivot (origin) at `head_base`; overlaps neck 20 mm.
  - **Eyes:** spheres r = 12 mm adult (scaled), origin at centre, three material slots (sclera, iris on −Y cap, cornea bulge shell); **Lids:** shell segment r + 1 mm, open = covers top 25 %, `Blink` key = covers front fully.
  - **Shape keys** as functions of figure-space position, applied identically to `Body` and `Clothes`: `Breathe` = chest/upper-belly Gaussian, outward 12 mm max; `NeckLong` = vertices between neck base and head_base lifted linearly 0→0.08 m, above head_base fully 0.08 m. `SmileWide` on `Head` = mouth corners out 8 mm and up 4 mm, lips stretched thin.
  - **Hair:** head-surface shell offset 6 mm with shape mask per style + low-frequency noise; glossy material.
  - Writes previews (Eevee, 3/4 front, 1024²) to `Tools/blender/out/`.
- [ ] **Step 4: Run generator then verifier — expect PASS:** `Blender --background --factory-startup --python Tools/blender/figure_gen.py -- --variant all --out Assets/ModelHome/Models/Figures` then the verify command.
- [ ] **Step 5: Look at the six previews** (Read the PNGs). Pass bar: reads as a human display figure with glass eyes, no holes/spikes, clothes not intersecting skin at rest. Iterate generator values if not.
- [ ] **Step 6: Implement `photo_render.py`** — imports `Figure_Dad_Standing`, `Figure_Mom_Standing`, `Figure_Daughter_Standing`, poses them side by side against a mottled grey studio backdrop, soft key light, 85 mm, renders 1024×768 to `Assets/ModelHome/Textures/Photos/photo_family.png`. Run it.
- [ ] **Step 7: Write Unity contract test** `FigureImportContractTests`: for each variant FBX, `AssetDatabase.LoadAssetAtPath<GameObject>` has children per contract; `Body`/`Clothes`/`Head`/`Lid_*` have `SkinnedMeshRenderer` whose `sharedMesh.GetBlendShapeIndex(<name>) >= 0` per contract. `ModelImportRules`: figure FBX → `importBlendShapes = true`, `materialImportMode = ImportViaMaterialDescription`, then after import `SearchAndRemapMaterials(BasedOnMaterialName, Everywhere)`.
- [ ] **Step 8: Run Unity tests — expect PASS.**
- [ ] **Step 9: Commit** — `feat: procedural display-figure generator, six figure variants, family photo`

---

### Task 5: House generator (Blender)

**Files:**
- Create: `Tools/blender/house_gen.py`, `Tools/blender/verify_house.py`
- Modify: `Assets/ModelHome/Editor/ModelImportRules.cs` (house rules)
- Test: `Assets/ModelHome/Tests/EditMode/HouseImportContractTests.cs`

**Interfaces:**
- Produces: `Assets/ModelHome/Models/House/ModelHome_House.fbx` with: geometry; empties `Anchor_*`, `Light_*`, `Anchor_PlayerSpawn`; interactable meshes `INT_*`. Anchor facing = the empty's **−Y** axis (same convention as figure front, so `figure.rotation = anchor.rotation` aligns them).

Floor plan (Blender metres; X right, Y into house, Z up; walls 0.12 thick; ceiling 2.6; doors 0.9×2.05; windows sill 0.9, height 1.2 unless noted):

| Room | X | Y | Openings |
|---|---|---|---|
| Foyer / guard post | 0–3 | 0–4 | Front door wall Y=0 X 1.0–2.0; to living: wall X=3, Y 1.2–2.8 (1.6 wide, 2.1 high); to back hall: wall Y=4, X 1.0–2.0 |
| Living | 3–8 | 0–5 | Front window Y=0 X 4.5–6.5; to kitchen: wall Y=5, X 4.0–7.0 arch 2.1 high; study door: wall X=8, Y 1.5–2.4 |
| Study | 8–12 | 0–5 | Window Y=0 X 9.5–11.0 |
| Kitchen + dining | 3–12 | 5–10 | Kitchen window wall Y=10 X 9.4–10.6 (sill 1.05, height 0.9); to back hall: wall X=3, Y 6.0–7.0 |
| Back hall | 0–3 | 4–10 | Back door wall Y=10 X 1.0–2.0; utility window wall X=0 Y 7.5–8.5 (sill 1.1, height 0.8) |

Stairs: five steps in foyer X 0.1–1.0, Y 1.2→3.0 rising +Y, ending at a dark boxed landing; tape across (`M_Paper` yellow tint) + sign mesh "UPSTAIRS NOT STAGED" (text object → mesh); blocking collider mesh `COL_StairsBlock`.

Furniture/objects (named meshes; materials from Task 3): guard table + folding chair + `INT_Clipboard` (2.2, 3.2, table top 0.74) + `Light_FoyerLamp` (desk lamp, 2.5, 3.4); sofa (5.0, 0.9) facing +Y; armchair (7.3, 3.8); coffee table (5.2, 2.3); rug under coffee table; `INT_LivingLamp` floor lamp (7.6, 0.6) with `Light_LivingLamp` at shade; photo frame with `M_Photo_Family` on wall X=3 at (3.06, 3.8, 1.55) and a second one in the study; study desk (10.25, 0.45) + chair + `INT_StudyLamp` desk lamp with `Light_StudyLamp`; `INT_StudyWindow`, `INT_KitchenWindow`, `INT_UtilityWindow` = window latch meshes at window centres; counter run along Y=10 X 8.0–11.8 depth 0.6 height 0.92 with sink + upper cabinets; fridge on wall X=12 at Y 6.0–6.8 (`SND_Fridge` empty at its top); dining table centre (5.5, 7.5) 1.6×0.9×0.74 = `INT_DiningTable`, chairs at Y 6.85 (face +Y), Y 8.15 (face −Y), X 4.5 (face +X); `Light_KitchenPendant` above table at Z 2.1; bassinet (7.2, 8.9) long axis X; wall clock on wall X=3 kitchen side at (3.06, 8.0, 2.0) (`SND_Clock` empty); `INT_FrontDoor`, `INT_BackDoor` = door-lock meshes on door leaves; skirting boards; outside ground plane 40×40 (`M_Ground_Dirt`) and three unlit house-frame silhouettes 25–35 m away (studs, `M_Cabinet_Wood` dark tint) seen through windows.

Anchors (position; facing direction):

| Anchor | Position (X,Y,Z) | Facing |
|---|---|---|
| `Anchor_PlayerSpawn` | (1.8, 2.4, 0) | +X |
| `Anchor_Dad_Armchair` | (7.3, 3.8, 0) | (−1, −0.5) normalized |
| `Anchor_Dad_LivingDoorway` | (5.5, 4.6, 0) | −Y |
| `Anchor_Mom_Counter` | (9.8, 9.3, 0) | +Y |
| `Anchor_Mom_FacingHall` | (9.8, 9.3, 0) | −X |
| `Anchor_Mom_BackHallDoor` | (3.6, 6.5, 0) | +X |
| `Anchor_Daughter_Chair` | (5.5, 8.15, 0) | −Y |
| `Anchor_Baby_Bassinet` | (7.2, 8.9, 0.55) | lying: front (face) up, head toward +X |

- [ ] **Step 1: Write `verify_house.py`** — asserts every name in the anchor table, `Light_FoyerLamp`, `Light_LivingLamp`, `Light_StudyLamp`, `Light_KitchenPendant`, the nine `INT_*` (`INT_Clipboard`, `INT_FrontDoor`, `INT_BackDoor`, `INT_StudyWindow`, `INT_KitchenWindow`, `INT_UtilityWindow`, `INT_LivingLamp`, `INT_StudyLamp`, `INT_DiningTable`), `SND_Fridge`, `SND_Clock`, `COL_StairsBlock` exist; anchor −Y axes match the table (dot > 0.99); all mesh materials are in the Task 3 name list; floor area of walkable rooms ≥ 110 m².
- [ ] **Step 2: Run — expect FAIL.**
- [ ] **Step 3: Implement `house_gen.py`** — walls as boxes with boolean-free construction (split wall segments around openings), world-scale box-projected UVs (1 UV unit = 1 m, then scaled by the material's tiling from Task 3), door leaves hinged and open 80° except front/back doors (closed); exports with `common.export_fbx`; writes top-down and foyer-eye-level previews to `Tools/blender/out/`.
- [ ] **Step 4: Run generator + verifier — expect PASS**; look at previews.
- [ ] **Step 5: Unity rules + contract test** — house FBX: `addCollider = true`, `generateSecondaryUV = true`, material remap as figures. `HouseImportContractTests`: prefab contains all names from Step 1 and at least one `MeshCollider`.
- [ ] **Step 6: Run Unity tests — expect PASS.**
- [ ] **Step 7: Commit** — `feat: procedural show-home ground floor with anchors and interactables`

---

### Task 6: Synthesised audio

**Files:**
- Create: `Tools/audio/synth.py`, `Tools/audio/test_synth.py`
- Modify: `Assets/ModelHome/Editor/AssetImportRules.cs` (add audio rules)

**Interfaces:**
- Produces (`Assets/ModelHome/Audio/`, 48 kHz mono 16-bit): `hum_roomtone.wav` (loop 10 s), `fridge_buzz.wav` (loop 6 s), `clock_tick.wav` (loop 2 s, tick at 0 s, tock at 1 s), `step_wood_01..04.wav`, `step_carpet_01..04.wav`, `inhale.wav` (1.6 s), `breath_slow.wav` (loop 4.5 s inhale+exhale), `click_flashlight.wav`, `click_switch.wav`, `latch.wav`, `lock_clunk.wav`, `drone_ending.wav` (12 s).

- [ ] **Step 1: Write `test_synth.py`** — for every file above: exists; mono; 48 kHz; duration within ±2 % of spec; peak ≤ 0.9 FS; files named `*loop*`-type (`hum_roomtone`, `fridge_buzz`, `clock_tick`, `breath_slow`) have |first − last sample| < 0.01 FS; footsteps are distinct (pairwise correlation < 0.9).
- [ ] **Step 2: Run `python3 -I Tools/audio/test_synth.py` — expect FAIL.**
- [ ] **Step 3: Implement `synth.py`** (numpy + `wave`): hum = 50 Hz + 100/150 Hz harmonics at −30 dB under brown noise; fridge = 120 Hz buzz with slow AM + low rumble; clock = short band-passed noise clicks, tock pitched lower; steps = filtered noise transients (wood brighter, carpet low-passed at 900 Hz) with random seeds; inhale/breath = band-passed noise with breath envelopes; clicks/latch/clunk = transients + short resonant decays; drone = detuned 41/43.5/55 Hz sines + rising filtered noise swell.
- [ ] **Step 4: Run synth then test — expect PASS.** Listen check is not possible headlessly; rely on tests + in-game check in Task 12.
- [ ] **Step 5: Unity audio rules** — loops: `loadType = CompressedInMemory`, others `DecompressOnLoad`; all `forceToMono`.
- [ ] **Step 6: Commit** — `feat: synthesised ambience and foley`

---

### Task 7: Player, flashlight, interaction

**Files:**
- Create: `Assets/ModelHome/Scripts/Player/PlayerController.cs`, `Flashlight.cs`, `FlashlightMath.cs`, `Assets/ModelHome/Scripts/Interaction/Interactable.cs`, `PlayerInteractor.cs`, `Assets/ModelHome/Scripts/UI/Hud.cs`
- Test: `Assets/ModelHome/Tests/EditMode/FlashlightMathTests.cs`

**Interfaces:**
- Produces:
  - `static class FlashlightMath { static bool IsInCone(Vector3 origin, Vector3 forward, float halfAngleDeg, float range, Vector3 point); }`
  - `Flashlight : MonoBehaviour { bool IsOn; void Toggle(); IEnumerator Sputter(float seconds, bool endOn); bool IsLit(Vector3 point); }` — Spot light, range 18, spot angle 55 (inner 30), intensity 6, soft shadows, cookie none; F toggles; plays `click_flashlight`.
  - `Interactable : MonoBehaviour { string Prompt; string TaskId; bool Enabled; event Action<Interactable> Interacted; event Action<string> TaskDone; void Interact(); void ReportTaskDone(); }` — 0.35 s cooldown. `Interacted` fires on every use; `TaskDone(TaskId)` fires only when the owning world component calls `ReportTaskDone()` (i.e. the *correct* action happened).
  - `PlayerInteractor : MonoBehaviour` — ray from camera, max 2.0 m, layer mask "Default"; shows `Hud.SetPrompt("E — " + Prompt)` or clears.
  - `Hud : MonoBehaviour { void SetPrompt(string); void Toast(string text, float seconds = 2.5f); CanvasGroup Fade; Text Clock; GameObject Clipboard; Text ClipboardText; }`
  - `PlayerController : MonoBehaviour { bool InputEnabled; }` — CharacterController (height 1.75, radius 0.3), walk 1.6 m/s, mouse sensitivity 2.0, pitch clamp ±80°, head bob 0.025 m at 1.8 Hz, footstep every 0.55 m chosen by ground collider name (`Rug` → carpet set, else wood), cursor locked; **Esc unlocks + suspends look; left click in Game view re-locks**.

- [ ] **Step 1: Write failing tests**

```csharp
[Test] public void PointStraightAhead_InCone() => Assert.IsTrue(FlashlightMath.IsInCone(Vector3.zero, Vector3.forward, 27.5f, 18f, new Vector3(0,0,5)));
[Test] public void PointBehind_NotInCone() => Assert.IsFalse(FlashlightMath.IsInCone(Vector3.zero, Vector3.forward, 27.5f, 18f, new Vector3(0,0,-5)));
[Test] public void PointBeyondRange_NotInCone() => Assert.IsFalse(FlashlightMath.IsInCone(Vector3.zero, Vector3.forward, 27.5f, 18f, new Vector3(0,0,19)));
[Test] public void PointAtEdge_InsideHalfAngle() => Assert.IsTrue(FlashlightMath.IsInCone(Vector3.zero, Vector3.forward, 27.5f, 18f, Quaternion.Euler(0,27f,0)*Vector3.forward*5));
[Test] public void PointAtOrigin_InCone() => Assert.IsTrue(FlashlightMath.IsInCone(Vector3.zero, Vector3.forward, 27.5f, 18f, Vector3.zero));
```

- [ ] **Step 2: Run — expect FAIL.**
- [ ] **Step 3: Implement all files in this task** per Interfaces. `Flashlight.IsLit` = `IsOn && IsInCone(...) && !Physics.Linecast(origin, point, occluders)` ignoring the target's own colliders.
- [ ] **Step 4: Run — expect PASS.**
- [ ] **Step 5: Commit** — `feat: player controller, flashlight and interaction`

(Play-mode check of movement, Esc/click cursor behaviour and prompts happens in Task 10 Step 6 once a scene exists.)

---

### Task 8: Display-figure behaviour and visibility

**Files:**
- Create: `Assets/ModelHome/Scripts/Figures/DisplayFigure.cs`, `GazeMath.cs`, `GlanceAwayDetector.cs`, `Assets/ModelHome/Scripts/Figures/Visibility.cs`, `FigureId.cs`
- Test: `Assets/ModelHome/Tests/EditMode/GazeMathTests.cs`, `GlanceAwayDetectorTests.cs`

**Interfaces:**
- Consumes: figure contract (Global Constraints); `Flashlight.IsLit(Vector3)`.
- Produces:
  - `enum FigureId { Dad, Mom, Daughter, Baby }`
  - `static class GazeMath { static Vector3 ClampToCone(Vector3 restForward, Vector3 desired, float maxAngleDeg); }`
  - `class GlanceAwayDetector { GlanceAwayDetector(float gazeAngleDeg = 25f, float cooldown = 5f); bool Update(bool onScreen, float angleFromViewCentreDeg, float time); }` — returns true once when, while on screen, the angle crosses from < gaze to ≥ gaze, and cooldown has elapsed.
  - `static class Visibility { static bool IsSeen(Renderer r, Camera cam, int occluderMask); }` — frustum AABB test, then linecasts from camera to bounds centre + 4 top/bottom corners; seen if any reaches without hitting a collider that isn't a child of the renderer's root.
  - `DisplayFigure : MonoBehaviour { FigureId Id; string Variant; bool EyeTracking; bool Breathing; bool BlinkOnGlanceAway; float HeadTiltDeg; float NeckLong01; float SmileWide01; void PlaceAt(Transform anchor); void PlaceFacing(Vector3 feet, Vector3 lookTarget); IEnumerator Blink(float closeSeconds = 0.12f); bool IsSeenBy(Camera cam); Transform Head; }`
- Behaviour values: eye tracking active when `Flashlight.IsLit(head)` or player within 4 m; SmoothDamp time 0.35 s; clamp 30° from face-forward; when inactive, eyes **hold** their last direction. Breathing: `Breathe` = 50 + 50·sin(2π·0.22·t + phase) with per-figure random phase. Head tilt: `head.localRotation = restLocal * AngleAxis(HeadTiltDeg, head.InverseTransformDirection(faceForward))`. Neck: `NeckLong` blend weight = 100·NeckLong01 on Body+Clothes and `Head` local position += up(root)·0.08·NeckLong01. Smile: `SmileWide` = 100·SmileWide01. Face-forward is computed at `Awake` as the horizontal direction from `Head` to the eye midpoint, **not** `root.forward` (removes axis-convention dependency). Eye rotation: `eye.rotation = FromToRotation(currentFaceFwd, smoothedDir) * head.rotation * restEyeRelHead`. The spec's "relocate only when unseen" rule (`TryRelocate`) is satisfied without a dedicated method: `LapDirector` calls `PlaceAt` only at full-black fade (Task 9) and `EndingSequence` re-places only while `!IsSeenBy` (Task 11). Missing contract children → one `Debug.LogError` naming figure + child, that feature disabled.

- [ ] **Step 1: Write failing tests**

```csharp
[Test] public void Clamp_WithinCone_Unchanged() { var d = Quaternion.Euler(0,20,0)*Vector3.forward;
    Assert.That(Vector3.Angle(d, GazeMath.ClampToCone(Vector3.forward, d, 30)), Is.LessThan(0.01f)); }
[Test] public void Clamp_Behind_LimitedTo30() { var r = GazeMath.ClampToCone(Vector3.forward, Vector3.back + Vector3.right*0.01f, 30);
    Assert.That(Vector3.Angle(Vector3.forward, r), Is.EqualTo(30).Within(0.1f)); }
[Test] public void Clamp_StraightUp_LimitedTo30() => Assert.That(Vector3.Angle(Vector3.forward, GazeMath.ClampToCone(Vector3.forward, Vector3.up, 30)), Is.EqualTo(30).Within(0.1f));
[Test] public void Clamp_ExactlyBehind_ReturnsValidDirection() { var r = GazeMath.ClampToCone(Vector3.forward, Vector3.back, 30);
    Assert.That(r.magnitude, Is.EqualTo(1).Within(1e-4)); Assert.That(Vector3.Angle(Vector3.forward, r), Is.EqualTo(30).Within(0.1f)); }

[Test] public void Glance_FiresWhenGazeLeaves() { var g=new GlanceAwayDetector(25,5); Assert.IsFalse(g.Update(true,5,0)); Assert.IsTrue(g.Update(true,30,0.1f)); }
[Test] public void Glance_RespectsCooldown() { var g=new GlanceAwayDetector(25,5); g.Update(true,5,0); g.Update(true,30,0.1f); g.Update(true,5,1); Assert.IsFalse(g.Update(true,30,2)); g.Update(true,5,6); Assert.IsTrue(g.Update(true,30,6.1f)); }
[Test] public void Glance_OffScreen_NeverFires() { var g=new GlanceAwayDetector(25,5); g.Update(true,5,0); Assert.IsFalse(g.Update(false,30,0.1f)); }
```

- [ ] **Step 2: Run — expect FAIL.**
- [ ] **Step 3: Implement** per Interfaces.
- [ ] **Step 4: Run — expect PASS.**
- [ ] **Step 5: Commit** — `feat: display figure behaviours (gaze, blink, breathing, proportions) and visibility`

---

### Task 9: Lap staging, director, checklist UI, house lights, world interactables

**Files:**
- Create: `Assets/ModelHome/Scripts/Laps/LapStaging.cs` (ScriptableObject), `LapDirector.cs`, `Checklist.cs`, `Assets/ModelHome/Scripts/World/HouseLights.cs`, `DoorLock.cs`, `LampSwitch.cs`, `WindowLatch.cs`, `FigureCount.cs`, `GuardClipboard.cs`, `IResettable.cs`, `ProximityCue.cs`
- Create: `Assets/ModelHome/Editor/LapStagingFactory.cs`
- Test: `Assets/ModelHome/Tests/EditMode/LapStagingAssetsTests.cs`

**Interfaces:**
- Consumes: `ChecklistModel`, `LapModel` (Task 2); `DisplayFigure`, `FigureId` (Task 8); `Interactable`, `Hud`, `PlayerController`, `Flashlight` (Task 7).
- Produces:
  - `[Serializable] class FigureStaging { FigureId figure; string variant; string anchor; bool visible; bool eyeTracking; bool breathing; bool blinkOnGlanceAway; float headTiltDeg; float neckLong01; float smileWide01; }`
  - `LapStaging : ScriptableObject { string clockText; ChecklistTaskDef[] tasks; FigureStaging[] figures; bool inhaleCueNearDaughter; }` with `[Serializable] struct ChecklistTaskDef { string id; string label; }`
  - `LapStagingFactory.CreateAll()` → `Assets/ModelHome/Staging/Lap1.asset`, `Lap2.asset`, `Lap3.asset`.
  - `Checklist : MonoBehaviour { ChecklistModel Model; void Load(LapStaging); }` — subscribes to every `Interactable.TaskDone`; ticks that id; Tab held shows clipboard text `"[x] label"` (grey) / `"[ ] label"`.
  - `LapDirector : MonoBehaviour { LapModel Model; LapStaging[] laps; DisplayFigure[] figures; Transform anchorsRoot; event Action EndingRequested; void TryTurnIn(); }` — transition: `PlayerController.InputEnabled = false` → fade to black 1.2 s → **only when alpha == 1**: apply next staging (`PlaceAt` / hide / flags), reset all `IResettable`, `Checklist.Load`, show clock text 1.5 s → fade in 1.2 s → input on. Lap 3 complete → `EndingRequested` (no fade).
  - `GuardClipboard` (on `INT_Clipboard`): calls `LapDirector.TryTurnIn()`; if checklist incomplete → `Hud.Toast($"Tasks remaining: {n}")`.
  - `FigureCount` (on `INT_DiningTable`): toast `"{visibleFigureCount} present."` (counts distinct `FigureId`s with a visible variant).
  - `DoorLock`, `LampSwitch`, `WindowLatch`: toggle state, sound, toast ("Locked."/"Unlocked.", "Latched."); call `ReportTaskDone()` only on lock / switch-off / latch; `IResettable.ResetForLap()` → unlocked / lamp on / unlatched (the house "resets itself" between laps). `FigureCount` and `GuardClipboard` report on every use.
  - `HouseLights : MonoBehaviour { void Flicker(Light l, float seconds); void PowerCut(); }` — idle random flicker of the kitchen pendant every 20–45 s (0.4 s).
  - `ProximityCue`: plays `inhale.wav` once from Daughter when player first comes within 2.0 m, only if the current staging has `inhaleCueNearDaughter`.

Staging values (exact, from spec):

| Lap | clock | tasks (id — label) | figures (variant @ anchor; flags) |
|---|---|---|---|
| 1 | `01:00` | `lock_front` — Lock front door; `latch_study_window` — Check study window latch; `lamp_living_off` — Turn off living-room lamp; `lock_back` — Lock back door | Dad_Seated@Anchor_Dad_Armchair; Mom_Standing@Anchor_Mom_Counter; Daughter_Seated@Anchor_Daughter_Chair; Baby_Lying@Anchor_Baby_Bassinet; all flags off, tilt 0 |
| 2 | `02:00` | `latch_kitchen_window` — Check kitchen window latch; `lamp_study_off` — Turn off study lamp; `latch_utility_window` — Check utility window latch; `lock_back` — Lock back door | as Lap 1 but Mom@Anchor_Mom_FacingHall; eyeTracking all; breathing Dad; blinkOnGlanceAway all; Daughter headTilt 8; inhaleCueNearDaughter true |
| 3 | `03:00` | `latch_kitchen_window` — Check kitchen window latch; `count_figures` — Confirm display figures present (4); `lamp_study_off` — Turn off study lamp; `lock_back` — Lock back door | Dad_Standing@Anchor_Dad_LivingDoorway neckLong 0.35, breathing, eyes, blink; Mom_Standing@Anchor_Mom_BackHallDoor smileWide 0.40, eyes, blink; Daughter (all variants) hidden; Baby_Lying@Anchor_Baby_Bassinet eyes, blink |

Lap 1 starts without a fade (game start shows clock `01:00` over black, then fades in). `count_figures` ticks on interaction regardless of count (spec).

- [ ] **Step 1: Write failing test** `LapStagingAssetsTests`: loads the three assets; asserts clock texts; task id sequences exactly as table; Lap2 Daughter `headTiltDeg == 8`, Mom anchor `Anchor_Mom_FacingHall`; Lap3 Dad `neckLong01 == 0.35f` variant `Dad_Standing`, Mom `smileWide01 == 0.40f`, no visible Daughter entry; every `anchor` string is a name present in `ModelHome_House.fbx`; every `variant` is one of the six.
- [ ] **Step 2: Run — expect FAIL.**
- [ ] **Step 3: Implement** all files per Interfaces; run `LapStagingFactory.CreateAll()` via MCP `script-execute`.
- [ ] **Step 4: Run — expect PASS.**
- [ ] **Step 5: Commit** — `feat: lap staging data, lap director, checklist UI and world interactables`

---

### Task 10: Scene builder

**Files:**
- Create: `Assets/ModelHome/Editor/ModelHomeSceneBuilder.cs`
- Create (generated): `Assets/ModelHome/Scenes/ModelHome_Demo.unity`
- Test: `Assets/ModelHome/Tests/EditMode/SceneContractTests.cs`

**Interfaces:**
- Consumes: everything above.
- Produces: `ModelHomeSceneBuilder.Build()` (`[MenuItem("Model Home/3. Build Demo Scene")]`, idempotent: overwrites the scene) and `BuildCli()` (batchmode: `Build()` then `EditorApplication.Exit(0)`); adds scene as build index 0.

- [ ] **Step 1: Write failing test** `SceneContractTests` (opens the scene with `EditorSceneManager.OpenScene`): exactly one `PlayerController` positioned at `Anchor_PlayerSpawn` (±0.05 m); six `DisplayFigure`s with distinct variants; nine `Interactable`s whose `TaskId`s cover every task id in the three staging assets; one `LapDirector` with 3 laps and 6 figures assigned; a global `Volume` using `MH_PostProfile`; main camera has post-processing enabled and AA = SMAA High; `RenderSettings.fog` true, mode Exponential, density 0.03; audio sources on `SND_Fridge` (fridge_buzz loop) and `SND_Clock` (clock_tick loop) with spatialBlend 1; `HouseLights` references the four `Light_*` lights; `EndingSequence` present (Task 11 adds the component — leave this assertion in, it goes green after Task 11).
- [ ] **Step 2: Run — expect FAIL.**
- [ ] **Step 3: Implement `Build()`**: new empty scene → instantiate house prefab → materials remapped (Task 3/4 rules) → add `MeshCollider`s present from import; set `Rug` objects' names intact → instantiate six figure prefabs, add `DisplayFigure` (Id/Variant from name), parent under `Figures` → player rig (Capsule `CharacterController`, camera at 1.62 m, FOV 60, URP camera data post-processing on, SMAA High; child `Flashlight` spot) at spawn anchor → lights: convert each `Light_*` empty into a Point light (warm 2700 K; FoyerLamp 1.2 intensity range 4; Living 1.8/6; Study 1.4/5; KitchenPendant 1.6/6; soft shadows on Living + KitchenPendant only), Realtime; (lamps are Realtime rather than the spec's Mixed: they are switched off during play and Mixed would leave their baked bounce light behind); moonlight Directional (colour `#9FB4D6`, intensity 0.08, rotation (35, −30, 0), Mixed); ambient colour `#0C0F14`; fog `#0B0F14` density 0.03 → global Volume with `MH_PostProfile` → HUD canvas (prompt bottom-centre 22 pt, toast above it, crosshair 4 px dot 30 % white, clipboard panel paper `#EFE9DC` with dark text, full-screen black fade `CanvasGroup`, clock text centre 48 pt) → interactables: add `BoxCollider` + `Interactable` + the right world component to each `INT_*` with prompts ("Lock front door", "Check window latch", "Turn off lamp", "Count display figures", "Sign the round sheet") and TaskIds (`INT_FrontDoor`→`lock_front`, `INT_BackDoor`→`lock_back`, `INT_StudyWindow`→`latch_study_window`, `INT_KitchenWindow`→`latch_kitchen_window`, `INT_UtilityWindow`→`latch_utility_window`, `INT_LivingLamp`→`lamp_living_off`, `INT_StudyLamp`→`lamp_study_off`, `INT_DiningTable`→`count_figures`, `INT_Clipboard`→none) → ambient audio (hum on player, 2D, volume 0.35; fridge, clock 3D) → `LapDirector`, `Checklist`, `HouseLights`, `ProximityCue` wired → light probes grid (0.75 m spacing, Y 0.3/1.2/2.2) inside rooms → bake lightmaps (GPU progressive, 12 texels/m, 512 indirect samples) → save scene → add to build settings.
- [ ] **Step 4: Run `Build()` via MCP `script-execute`** (fallback: close editor, `-batchmode -quit -executeMethod ModelHome.EditorTools.ModelHomeSceneBuilder.BuildCli -logFile Logs/build-scene.log`). Then tests — expect PASS except the `EndingSequence` assertion.
- [ ] **Step 5: Look at it** — MCP `screenshot-scene-view` from the foyer and the living room; check figures face out of their anchors (Mom faces the kitchen window in Lap 1), no missing (pink) materials.
- [ ] **Step 6: Play-mode smoke** — MCP enter Play, `screenshot-game-view`, `console-get-logs` (no errors); verify WASD/mouse/F/E/Tab and **Esc releases cursor, click re-locks** (Review Focus 5); clipboard early turn-in shows "Tasks remaining: 4" (Review Focus 1).
- [ ] **Step 7: Commit** — `feat: reproducible demo scene builder`

---

### Task 11: Ending sequence

**Files:**
- Create: `Assets/ModelHome/Scripts/Ending/EndingSequence.cs`, `EndingPlacement.cs`
- Modify: `Assets/ModelHome/Editor/ModelHomeSceneBuilder.cs` (add + wire `EndingSequence`)
- Test: `Assets/ModelHome/Tests/EditMode/EndingPlacementTests.cs`

**Interfaces:**
- Consumes: `LapDirector.EndingRequested`, `DisplayFigure` (Daughter_Standing), `Flashlight.Sputter`, `HouseLights.PowerCut`, `Hud`, `Visibility`.
- Produces:
  - `static class EndingPlacement { static IReadOnlyList<Vector3> Candidates(Vector3 playerFeet, Vector3 playerForward); }` — order: 0.6 m directly behind; 0.6 m at ±45° behind; 0.45 m directly behind; 0.45 m at ±90° (beside). All on the player's floor height; `playerForward` flattened.
  - `EndingSequence : MonoBehaviour { void Play(); }`

Sequence (exact): `PowerCut()` + start `drone_ending` (volume 0 → 0.6 over 8 s) → `Sputter(2f, endOn: true)` → while flashlight is in an *off* blink, `PlaceFacing` Daughter_Standing at the first candidate whose capsule (r 0.25, h 1.3) `Physics.CheckCapsule` is clear, eyes tracking on → each frame while she's not seen, re-place her behind the player (same candidate rule) → when `IsSeenBy(cam)` continuously ≥ 0.8 s: `Blink()`; at full closure: fade alpha = 1 instantly, `AudioListener.volume = 0` → 2 s silence → title text `MODEL HOME` (Hud clock text slot, 64 pt) fades in over 2 s, `AudioListener.volume` restored with only `hum_roomtone` very low → Esc quits play mode / application. **Fallback:** if not seen within 20 s, play `breath_slow` from her position for 3 s, then do the cut/title anyway. If no candidate is clear, use the last candidate anyway but log a warning.

- [ ] **Step 1: Write failing tests**

```csharp
[Test] public void FirstCandidate_IsDirectlyBehind_AtPoint6() { var c = EndingPlacement.Candidates(Vector3.zero, Vector3.forward);
    Assert.That(Vector3.Distance(c[0], new Vector3(0,0,-0.6f)), Is.LessThan(1e-4)); }
[Test] public void Candidates_AllOnPlayerFloor() { foreach (var p in EndingPlacement.Candidates(new Vector3(1,2,3), Vector3.right)) Assert.That(p.y, Is.EqualTo(2).Within(1e-4)); }
[Test] public void Candidates_NoneInFront() { foreach (var p in EndingPlacement.Candidates(Vector3.zero, Vector3.forward)) Assert.That(Vector3.Dot(p, Vector3.forward), Is.LessThanOrEqualTo(1e-4)); }
[Test] public void Candidates_IgnoreLookPitch() { var c = EndingPlacement.Candidates(Vector3.zero, new Vector3(0,-0.9f,0.1f));
    Assert.That(Vector3.Distance(c[0], new Vector3(0,0,-0.6f)), Is.LessThan(1e-3)); }
[Test] public void Candidates_Count6() => Assert.AreEqual(6, EndingPlacement.Candidates(Vector3.zero, Vector3.forward).Count);
```

- [ ] **Step 2: Run — expect FAIL.**
- [ ] **Step 3: Implement** `EndingPlacement`, `EndingSequence`; builder adds it and subscribes it to `EndingRequested`. Rebuild scene.
- [ ] **Step 4: Run all EditMode tests — expect PASS** (including Task 10's `EndingSequence` assertion).
- [ ] **Step 5: Commit** — `feat: ending sequence with safe placement and fallback`

---

### Task 12: Full play-through verification and tuning

**Files:**
- Modify (tuning only): values in `MH_PostProfile`, lights in `ModelHomeSceneBuilder`, `DisplayFigure` serialized defaults
- Create: `docs/verification/2026-11-08-playthrough.md` (screenshots list + results)

- [ ] **Step 1: Debug lap skip for testing** — add `[MenuItem("Model Home/Debug/Complete Current Lap")]` (editor-only, play mode) that completes all current tasks; used only for verification.
- [ ] **Step 2: Play through via MCP** — for each lap: `screenshot-game-view` facing each figure (with flashlight on), save to `docs/verification/img/lap<N>_<figure>.png`; confirm per spec: Lap 1 still; Lap 2 Mom faces hall, Daughter tilt visible, eyes follow while lit; Lap 3 Dad in arch with longer neck, Mom at back-hall door with wide smile, empty Daughter chair, "3 present." toast; Ending places Daughter behind (test with a wall behind the player at the guard post — Review Focus 2), blink, cut, title.
- [ ] **Step 3: Console + performance** — `console-get-logs`: zero errors, zero warnings from `ModelHome`; Game view 1920×1080 Stats ≥ 45 fps on the M3 with flashlight on in the kitchen.
- [ ] **Step 4: Tune** only if a check fails or a screenshot reads wrong (too dark to see figures → raise moonlight to ≤ 0.15 or lamp intensity; figures look plastic → skin smoothness 0.45–0.6 range); re-run Steps 2–3.
- [ ] **Step 5: Write `docs/verification/2026-11-08-playthrough.md`** with results and the screenshot list; commit — `test: play-through verification of the Model Home demo`.

---

### Task 13: Windows PC build on the GPU (user requirement, 2026-11-08)

**Files:**
- Create: `Assets/ModelHome/Editor/BuildWindows.cs`
- Test: `Assets/ModelHome/Tests/EditMode/WindowsBuildSettingsTests.cs`

**Interfaces:**
- Produces: `ModelHome.EditorTools.BuildWindows.ApplySettings()` (idempotent) and `BuildWindows.Build()` (`[MenuItem("Model Home/4. Build Windows x64")]`, batchmode-callable) → `Builds/Windows/ModelHome.exe`.

- [ ] **Step 0: Prerequisite** — Unity "Windows Build Support (Mono)" module for 6000.3.28f1 installed (user approves the download; Unity Hub → Installs → Add modules, or `"/Applications/Unity Hub.app/Contents/MacOS/Unity Hub" -- --headless install-modules --version 6000.3.28f1 --module windows-mono`).
- [ ] **Step 1: Write failing tests** — `GraphicsApis_D3D12ThenD3D11`: `PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64) == false` and `GetGraphicsAPIs(StandaloneWindows64)` equals `{Direct3D12, Direct3D11}`; `Lightmapper_IsGpu`: the scene's `LightingSettings.lightmapper == ProgressiveGPU`; `Windows_Arch_x64_Il2CppNotRequired`: scripting backend Mono for Standalone; `Fullscreen_Default`: `PlayerSettings.fullScreenMode == FullScreenWindow`, default 1920×1080; `VSync_On`: every quality level `vSyncCount == 1`.
- [ ] **Step 2: Run — expect FAIL.**
- [ ] **Step 3: Implement `ApplySettings()`** per the tests (also `PlayerSettings.productName = "Model Home"`, `companyName = "FohYooo"`) and `Build()` (`BuildPipeline.BuildPlayer` with the demo scene, `BuildTarget.StandaloneWindows64`, `BuildOptions.None`; throws if `report.summary.result != Succeeded`).
- [ ] **Step 4: Run — expect PASS.**
- [ ] **Step 5: Build** — `Build()` via MCP or batchmode `-buildTarget Win64 -executeMethod ModelHome.EditorTools.BuildWindows.Build`. Expected: `Builds/Windows/ModelHome.exe`, `UnityPlayer.dll`, `ModelHome_Data/` exist; build log has 0 errors.
- [ ] **Step 6: Dedicated-GPU check** — `strings Builds/Windows/UnityPlayer.dll | grep -E "NvOptimusEnablement|AmdPowerXpressRequestHighPerformance"` shows both exports (hybrid laptops then pick the discrete GPU). If absent, record it and tell the user (fix needs a tiny native plugin built on Windows).
- [ ] **Step 7: Hand-off** — the exe cannot run on the Mac; the user runs it on their PC. Commit — `feat: Windows x64 GPU build settings and build script` (`Builds/` stays gitignored).
