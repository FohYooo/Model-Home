using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ModelHome.Tests
{
    public class SceneContractTests
    {
        public const string ScenePath = "Assets/ModelHome/Scenes/ModelHome_Demo.unity";
        Scene scene;

        [OneTimeSetUp] public void Open()
        {
            Assert.IsTrue(System.IO.File.Exists(ScenePath), "scene not built");
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        [OneTimeTearDown] public void Close()
        {
            if (scene.IsValid() && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
        }

        T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
        Transform Named(string n) => All<Transform>().First(t => t.name == n);

        [Test] public void OnePlayer_AtSpawn_FacingIntoTheHouse()
        {
            var players = All<PlayerController>();
            Assert.AreEqual(1, players.Length);
            var spawn = Named("Anchor_PlayerSpawn");
            var p = players[0].transform.position;
            Assert.AreEqual(0f, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(spawn.position.x, spawn.position.z)), 0.05f);
            Assert.Greater(Vector3.Dot(players[0].transform.forward, Facing.Flat(spawn)), 0.99f);
        }

        [Test] public void SixFigures_DistinctVariants_WithBehaviour()
        {
            var figs = All<DisplayFigure>();
            Assert.AreEqual(6, figs.Length);
            CollectionAssert.AreEquivalent(FigureImportContractTests.Variants, figs.Select(f => f.Variant));
        }

        [Test] public void Interactables_CoverEveryTask()
        {
            var its = All<Interactable>();
            Assert.AreEqual(9, its.Length);
            var ids = its.Select(i => i.TaskId).Where(s => !string.IsNullOrEmpty(s)).ToHashSet();
            for (int n = 1; n <= 3; n++)
                foreach (var t in AssetDatabase.LoadAssetAtPath<LapStaging>($"Assets/ModelHome/Staging/Lap{n}.asset").tasks)
                    Assert.IsTrue(ids.Contains(t.id), t.id);
            Assert.IsTrue(its.All(i => i.GetComponents<Collider>().Any(c => c.isTrigger)), "each interactable needs a trigger to aim at");
        }

        [Test] public void LapDirector_Wired()
        {
            var d = All<LapDirector>().Single();
            Assert.AreEqual(3, d.laps.Length);
            Assert.AreEqual(6, d.figures.Length);
            Assert.IsNotNull(d.checklist);
            Assert.IsNotNull(d.hud);
            Assert.IsNotNull(d.player);
            Assert.IsNotNull(d.anchorsRoot);
        }

        [Test] public void GlobalVolume_UsesPostProfile()
        {
            var v = All<Volume>().Single(x => x.isGlobal);
            Assert.AreEqual("MH_PostProfile", v.sharedProfile.name);
        }

        [Test] public void Camera_PostProcessing_SmaaHigh()
        {
            var cam = All<Camera>().Single(c => c.CompareTag("MainCamera"));
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            Assert.IsTrue(data.renderPostProcessing);
            Assert.AreEqual(AntialiasingMode.SubpixelMorphologicalAntiAliasing, data.antialiasing);
            Assert.AreEqual(AntialiasingQuality.High, data.antialiasingQuality);
        }

        [Test] public void Fog_ExponentialLow()
        {
            EditorSceneManager.SetActiveScene(scene);
            Assert.IsTrue(RenderSettings.fog);
            Assert.AreEqual(FogMode.Exponential, RenderSettings.fogMode);
            Assert.AreEqual(0.03f, RenderSettings.fogDensity, 1e-4);
        }

        [Test] public void AmbientSounds_Spatial()
        {
            var fridge = Named("SND_Fridge").GetComponent<AudioSource>();
            var clock = Named("SND_Clock").GetComponent<AudioSource>();
            Assert.AreEqual("fridge_buzz", fridge.clip.name);
            Assert.AreEqual("clock_tick", clock.clip.name);
            Assert.IsTrue(fridge.loop && clock.loop);
            Assert.AreEqual(1f, fridge.spatialBlend);
            Assert.AreEqual(1f, clock.spatialBlend);
        }

        [Test] public void HouseLights_ReferenceFourLamps()
        {
            var hl = All<HouseLights>().Single();
            CollectionAssert.AreEquivalent(new[] { "Light_FoyerLamp", "Light_LivingLamp", "Light_StudyLamp", "Light_KitchenPendant" },
                hl.lights.Select(l => l.name));
        }

        [Test] public void StairBlocker_Invisible_ButSolid()
        {
            var col = Named("COL_StairsBlock");
            Assert.IsFalse(col.GetComponent<Renderer>().enabled);
            Assert.IsNotNull(col.GetComponent<Collider>());
        }

        [Test] public void ShadowedLamps_OwnMeshesCastNoShadow()
        {
            foreach (var n in new[] { "INT_LivingLamp", "KitchenPendant" })
                Assert.AreEqual(ShadowCastingMode.Off, Named(n).GetComponent<Renderer>().shadowCastingMode, n);
        }

        [Test] public void LampSwitches_KnowTheirShade()
        {
            foreach (var sw in All<LampSwitch>())
                Assert.IsNotNull(sw.shade, sw.name);
        }

        [Test] public void EndingSequence_Wired()
        {
            var e = All<EndingSequence>().Single();
            Assert.AreSame(All<LapDirector>().Single(), e.director);
            Assert.AreEqual("Daughter_Standing", e.daughter.Variant);
            Assert.IsNotNull(e.torch);
            Assert.IsNotNull(e.houseLights);
            Assert.IsNotNull(e.hud);
            Assert.IsNotNull(e.player);
            Assert.AreEqual("drone_ending", e.drone.name);
            Assert.AreEqual("breath_slow", e.breath.name);
        }

        [Test] public void InBuildSettings_AsFirstScene()
        {
            Assert.Greater(EditorBuildSettings.scenes.Length, 0);
            Assert.AreEqual(ScenePath, EditorBuildSettings.scenes[0].path);
        }
    }
}
