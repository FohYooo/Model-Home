using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ModelHome.EditorTools
{
    /// <summary>Builds Assets/ModelHome/Scenes/ModelHome_Demo.unity from the generated assets (plan Task 10).
    /// Idempotent: every run rebuilds the scene from scratch.</summary>
    public static class ModelHomeSceneBuilder
    {
        public const string ScenePath = "Assets/ModelHome/Scenes/ModelHome_Demo.unity";
        const string Models = "Assets/ModelHome/Models";
        const string Audio = "Assets/ModelHome/Audio";
        const int IgnoreRaycast = 2;

        static readonly Dictionary<string, string> TaskIds = new Dictionary<string, string>
        {
            { "INT_FrontDoor", "lock_front" }, { "INT_BackDoor", "lock_back" },
            { "INT_StudyWindow", "latch_study_window" }, { "INT_KitchenWindow", "latch_kitchen_window" },
            { "INT_UtilityWindow", "latch_utility_window" }, { "INT_LivingLamp", "lamp_living_off" },
            { "INT_StudyLamp", "lamp_study_off" }, { "INT_DiningTable", "count_figures" }, { "INT_Clipboard", null },
        };

        [MenuItem("Model Home/3. Build Demo Scene")]
        public static void Build() => Build(bake: true);

        public static void Build(bool bake)
        {
            if (!AssetDatabase.IsValidFolder("Assets/ModelHome/Scenes")) AssetDatabase.CreateFolder("Assets/ModelHome", "Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var house = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>($"{Models}/House/ModelHome_House.fbx"));
            house.name = "House";
            PrepareHouse(house);

            var hud = BuildHud();
            var player = BuildPlayer(house, hud, out var cam, out var torch);
            var figures = BuildFigures();
            var lights = BuildLights(house);
            BuildEnvironment(cam);
            BuildAmbientAudio(house, player);

            var game = new GameObject("Game");
            var checklist = game.AddComponent<Checklist>();
            checklist.hud = hud;
            checklist.player = player;
            var director = game.AddComponent<LapDirector>();
            director.laps = Enumerable.Range(1, 3).Select(n => Load<LapStaging>($"Assets/ModelHome/Staging/Lap{n}.asset")).ToArray();
            director.figures = figures;
            director.anchorsRoot = house.transform;
            director.checklist = checklist;
            director.hud = hud;
            director.player = player;
            var houseLights = game.AddComponent<HouseLights>();
            houseLights.lights = lights;
            houseLights.pendant = lights.First(l => l.name == "Light_KitchenPendant");
            houseLights.shades = house.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.sharedMaterials.Any(m => m != null && m.name == "M_Lampshade")).ToArray();
            var cue = game.AddComponent<ProximityCue>();
            cue.director = director;
            cue.daughter = figures.First(f => f.Variant == "Daughter_Seated");
            cue.player = player.transform;
            cue.inhale = Clip("inhale");

            BuildInteractables(house, hud, director);
            BuildProbes();
            director.ApplyLap(director.laps[0]);   // the saved scene shows Lap 1's staging in the editor too
            var ending = game.AddComponent<EndingSequence>();
            ending.director = director;
            ending.daughter = figures.First(f => f.Variant == "Daughter_Standing");
            ending.torch = torch;
            ending.houseLights = houseLights;
            ending.hud = hud;
            ending.player = player;
            ending.drone = Clip("drone_ending");
            ending.breath = Clip("breath_slow");

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            if (bake) Lightmapping.BakeAsync();
            Debug.Log("[ModelHome] Demo scene built" + (bake ? " (lightmap bake started)" : ""));
        }

        // ------------------------------------------------------------------ house

        static void PrepareHouse(GameObject house)
        {
            foreach (var r in house.GetComponentsInChildren<MeshRenderer>(true))
            {
                var go = r.gameObject;
                GameObjectUtility.SetStaticEditorFlags(go,
                    StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
                r.shadowCastingMode = ShadowCastingMode.TwoSided;
                if (go.name == "Ground" || go.name == "EstateFrames") r.scaleInLightmap = 0.05f;
                if (go.name == "COL_StairsBlock") r.enabled = false;
                if (go.name == "INT_LivingLamp" || go.name == "KitchenPendant") r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // ------------------------------------------------------------------ player

        static PlayerController BuildPlayer(GameObject house, Hud hud, out Camera cam, out Flashlight torch)
        {
            var spawn = Find(house, "Anchor_PlayerSpawn");
            var go = new GameObject("Player");
            go.transform.SetPositionAndRotation(spawn.position + Vector3.up * 0.02f, Quaternion.LookRotation(Facing.Flat(spawn), Vector3.up));
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.75f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0, 0.875f, 0);
            cc.stepOffset = 0.25f;
            var pc = go.AddComponent<PlayerController>();
            pc.woodSteps = Enumerable.Range(1, 4).Select(i => Clip($"step_wood_{i:00}")).ToArray();
            pc.carpetSteps = Enumerable.Range(1, 4).Select(i => Clip($"step_carpet_{i:00}")).ToArray();
            pc.stepSource = go.AddComponent<AudioSource>();
            pc.stepSource.playOnAwake = false;
            pc.stepSource.spatialBlend = 0f;

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(0, 1.62f, 0);
            pc.cameraPivot = pivot;
            cam = pivot.gameObject.AddComponent<Camera>();
            pivot.gameObject.tag = "MainCamera";
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 80f;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            pivot.gameObject.AddComponent<AudioListener>();
            var interactor = pivot.gameObject.AddComponent<PlayerInteractor>();
            interactor.hud = hud;
            interactor.player = pc;

            var torchGo = new GameObject("Flashlight");
            torchGo.transform.SetParent(pivot, false);
            torchGo.transform.localPosition = new Vector3(0.16f, -0.14f, 0.12f);
            torchGo.AddComponent<Light>();
            torch = torchGo.AddComponent<Flashlight>();
            torch.player = pc;
            torch.clickClip = Clip("click_flashlight");
            torch.Configure();
            return pc;
        }

        // ------------------------------------------------------------------ figures

        static DisplayFigure[] BuildFigures()
        {
            var root = new GameObject("Figures").transform;
            var list = new List<DisplayFigure>();
            foreach (var v in new[] { "Dad_Seated", "Dad_Standing", "Mom_Standing", "Daughter_Seated", "Daughter_Standing", "Baby_Lying" })
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>($"{Models}/Figures/Figure_{v}.fbx"), root);
                go.name = $"Figure_{v}";
                var fig = go.AddComponent<DisplayFigure>();
                fig.Variant = v;
                fig.Id = (FigureId)System.Enum.Parse(typeof(FigureId), v.Split('_')[0]);
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.updateWhenOffscreen = true;  // blend shapes change the bounds
                    smr.shadowCastingMode = ShadowCastingMode.On;
                    smr.lightProbeUsage = LightProbeUsage.BlendProbes;
                    smr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                }
                if (fig.Id != FigureId.Baby) AddBodyCollider(go);
                list.Add(fig);
            }
            return list.ToArray();
        }

        static void AddBodyCollider(GameObject go)
        {
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            var cap = go.AddComponent<CapsuleCollider>();
            cap.direction = 2; // root local Z is world up (see Facing)
            cap.center = go.transform.InverseTransformPoint(b.center);
            cap.height = b.size.y;
            cap.radius = Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.z) * 0.6f, 0.15f, 0.28f);
            go.layer = IgnoreRaycast;
        }

        // ------------------------------------------------------------------ lights and environment

        static Light[] BuildLights(GameObject house)
        {
            GraphicsSettings.lightsUseLinearIntensity = true;
            GraphicsSettings.lightsUseColorTemperature = true;
            var specs = new (string name, float intensity, float range, bool shadows)[]
            {
                ("Light_FoyerLamp", 1.2f, 4f, false), ("Light_LivingLamp", 1.8f, 6f, true),
                ("Light_StudyLamp", 1.4f, 5f, false), ("Light_KitchenPendant", 1.6f, 6f, true),
            };
            var lights = new List<Light>();
            foreach (var s in specs)
            {
                var go = Find(house, s.name).gameObject;
                var l = go.GetComponent<Light>();
                if (l == null) l = go.AddComponent<Light>(); // not '??': Unity's fake-null defeats it
                l.type = LightType.Point;
                l.useColorTemperature = true;
                l.colorTemperature = 2700f;
                l.color = Color.white;
                l.intensity = s.intensity;
                l.range = s.range;
                l.shadows = s.shadows ? LightShadows.Soft : LightShadows.None;
                l.lightmapBakeType = LightmapBakeType.Realtime;
                lights.Add(l);
            }
            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            ColorUtility.TryParseHtmlString("#9FB4D6", out var mc);
            moon.color = mc;
            moon.intensity = 0.08f;
            moon.shadows = LightShadows.Soft;
            moon.lightmapBakeType = LightmapBakeType.Mixed;
            moon.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            return lights.ToArray();
        }

        static void BuildEnvironment(Camera cam)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            ColorUtility.TryParseHtmlString("#0C0F14", out var amb);
            RenderSettings.ambientLight = amb;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.03f;
            ColorUtility.TryParseHtmlString("#0B0F14", out var fog);
            RenderSettings.fogColor = fog;
            cam.clearFlags = CameraClearFlags.SolidColor;
            ColorUtility.TryParseHtmlString("#070A10", out var sky);
            cam.backgroundColor = sky;

            var post = new GameObject("PostFX").AddComponent<Volume>();
            post.isGlobal = true;
            post.sharedProfile = Load<VolumeProfile>(ProjectSetup.ProfilePath);

            foreach (var (n, pos, size) in new[] {
                         ("Reflections_Living", new Vector3(-5.5f, 1.3f, -2.5f), new Vector3(5f, 2.6f, 5f)),
                         ("Reflections_Kitchen", new Vector3(-7.5f, 1.3f, -7.5f), new Vector3(9f, 2.6f, 5f)) })
            {
                var probe = new GameObject(n).AddComponent<ReflectionProbe>();
                probe.transform.position = pos;
                probe.size = size;
                probe.mode = ReflectionProbeMode.Baked;
                probe.boxProjection = true;
            }

            var ls = new LightingSettings
            {
                name = "MH_Lighting",
                lightmapper = LightingSettings.Lightmapper.ProgressiveGPU,
                lightmapResolution = 12f,
                indirectSampleCount = 512,
                directionalityMode = LightmapsMode.CombinedDirectional,
                ao = true,
                bakedGI = true,
                realtimeGI = false,
                mixedBakeMode = MixedLightingMode.IndirectOnly,
            };
            var lsPath = "Assets/ModelHome/Settings/MH_Lighting.lighting";
            AssetDatabase.DeleteAsset(lsPath);
            AssetDatabase.CreateAsset(ls, lsPath);
            Lightmapping.lightingSettings = ls;
        }

        static void BuildAmbientAudio(GameObject house, PlayerController player)
        {
            Loop(Find(house, "SND_Fridge").gameObject, "fridge_buzz", 0.5f, 1f, 8f);
            Loop(Find(house, "SND_Clock").gameObject, "clock_tick", 0.6f, 0.5f, 7f);
            var hum = player.gameObject.AddComponent<AudioSource>();
            hum.clip = Clip("hum_roomtone");
            hum.loop = true;
            hum.playOnAwake = true;
            hum.volume = 0.35f;
            hum.spatialBlend = 0f;
        }

        static void Loop(GameObject go, string clip, float volume, float min, float max)
        {
            var a = go.AddComponent<AudioSource>();
            a.clip = Clip(clip);
            a.loop = true;
            a.playOnAwake = true;
            a.volume = volume;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Logarithmic;
            a.minDistance = min;
            a.maxDistance = max;
        }

        static void BuildProbes()
        {
            var group = new GameObject("LightProbes").AddComponent<LightProbeGroup>();
            var pts = new List<Vector3>();
            for (float x = -11.7f; x <= -0.2f; x += 0.75f)
                for (float z = -9.7f; z <= -0.2f; z += 0.75f)
                    foreach (var y in new[] { 0.3f, 1.2f, 2.2f })
                        pts.Add(new Vector3(x, y, z));
            group.probePositions = pts.ToArray();
        }

        // ------------------------------------------------------------------ interactables

        static void BuildInteractables(GameObject house, Hud hud, LapDirector director)
        {
            foreach (var kv in TaskIds)
            {
                var go = Find(house, kv.Key).gameObject;
                WorldSwitch sw;
                switch (kv.Key)
                {
                    case "INT_FrontDoor":
                    case "INT_BackDoor":
                        var dl = go.AddComponent<DoorLock>();
                        dl.doorName = kv.Key == "INT_FrontDoor" ? "front door" : "back door";
                        dl.clip = Clip("lock_clunk");
                        sw = dl;
                        break;
                    case "INT_LivingLamp":
                    case "INT_StudyLamp":
                        var ls = go.AddComponent<LampSwitch>();
                        ls.lamp = Find(house, kv.Key == "INT_LivingLamp" ? "Light_LivingLamp" : "Light_StudyLamp").GetComponent<Light>();
                        ls.shade = go.GetComponent<Renderer>();
                        ls.clip = Clip("click_switch");
                        sw = ls;
                        break;
                    case "INT_DiningTable":
                        var fc = go.AddComponent<FigureCount>();
                        fc.director = director;
                        sw = fc;
                        break;
                    case "INT_Clipboard":
                        var gc = go.AddComponent<GuardClipboard>();
                        gc.director = director;
                        gc.clip = Clip("click_switch");
                        sw = gc;
                        break;
                    default:
                        var wl = go.AddComponent<WindowLatch>();
                        wl.clip = Clip("latch");
                        sw = wl;
                        break;
                }
                sw.hud = hud;
                go.GetComponent<Interactable>().TaskId = kv.Value;
                AddAimTrigger(go);
            }
        }

        static void AddAimTrigger(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            if (r == null) return;
            var local = r.localBounds;
            var minSize = go.transform.InverseTransformVector(Vector3.one * 0.25f);
            box.center = local.center;
            box.size = new Vector3(Mathf.Max(local.size.x, Mathf.Abs(minSize.x)), Mathf.Max(local.size.y, Mathf.Abs(minSize.y)),
                Mathf.Max(local.size.z, Mathf.Abs(minSize.z)));
        }

        // ------------------------------------------------------------------ HUD

        static Hud BuildHud()
        {
            var canvasGo = new GameObject("HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = canvasGo.AddComponent<Hud>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var cross = Rect("Crosshair", canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(4, 4), Vector2.zero);
            cross.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.3f);

            hud.prompt = Label("Prompt", canvasGo.transform, font, 22, new Vector2(0.5f, 0f), new Vector2(900, 40), new Vector2(0, 120));
            hud.toast = Label("Toast", canvasGo.transform, font, 20, new Vector2(0.5f, 0f), new Vector2(900, 40), new Vector2(0, 170));
            hud.toast.color = new Color(0.85f, 0.85f, 0.8f, 0.9f);
            hud.prompt.enabled = hud.toast.enabled = false;

            var board = Rect("Clipboard", canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(560, 420), new Vector2(-520, 0));
            ColorUtility.TryParseHtmlString("#EFE9DC", out var paper);
            board.gameObject.AddComponent<Image>().color = paper;
            hud.Clipboard = board.gameObject;
            hud.ClipboardText = Label("Text", board, font, 22, new Vector2(0.5f, 0.5f), new Vector2(500, 380), Vector2.zero);
            hud.ClipboardText.alignment = TextAnchor.UpperLeft;
            hud.ClipboardText.color = new Color(0.12f, 0.12f, 0.14f);
            hud.ClipboardText.supportRichText = true;
            board.gameObject.SetActive(false);

            var fade = Rect("Fade", canvasGo.transform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            fade.anchorMin = Vector2.zero;
            fade.anchorMax = Vector2.one;
            fade.gameObject.AddComponent<Image>().color = Color.black;
            hud.Fade = fade.gameObject.AddComponent<CanvasGroup>();
            hud.Fade.alpha = 1f;
            hud.Fade.blocksRaycasts = false;

            hud.Clock = Label("Clock", canvasGo.transform, font, 48, new Vector2(0.5f, 0.5f), new Vector2(900, 140), Vector2.zero);
            hud.Clock.color = new Color(0.82f, 0.82f, 0.78f);
            hud.Clock.enabled = false;
            return hud;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        static Text Label(string name, Transform parent, Font font, int size, Vector2 anchor, Vector2 box, Vector2 pos)
        {
            var t = Rect(name, parent, anchor, box, pos).gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.9f, 0.9f, 0.86f);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // ------------------------------------------------------------------ helpers

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) throw new System.IO.FileNotFoundException($"[ModelHome] missing {path}");
            return a;
        }

        static AudioClip Clip(string n) => Load<AudioClip>($"{Audio}/{n}.wav");

        static Transform Find(GameObject root, string n)
        {
            var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == n);
            if (t == null) throw new System.Exception($"[ModelHome] house has no {n}");
            return t;
        }

        /// <summary>Batchmode entry point.</summary>
        public static void BuildCli()
        {
            Build(bake: false);
            EditorApplication.Exit(0);
        }
    }
}
