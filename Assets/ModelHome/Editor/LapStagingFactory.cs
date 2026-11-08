using UnityEditor;
using UnityEngine;

namespace ModelHome.EditorTools
{
    /// <summary>Writes the three lap staging assets with the spec's exact values (plan Task 9).</summary>
    public static class LapStagingFactory
    {
        public const string Dir = "Assets/ModelHome/Staging";

        [MenuItem("Model Home/Create Lap Staging")]
        public static void CreateAll()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/ModelHome", "Staging");

            Write(1, "01:00", new[]
            {
                T("lock_front", "Lock front door"), T("latch_study_window", "Check study window latch"),
                T("lamp_living_off", "Turn off living-room lamp"), T("lock_back", "Lock back door"),
            }, new[]
            {
                F(FigureId.Dad, "Dad_Seated", "Anchor_Dad_Armchair"),
                F(FigureId.Mom, "Mom_Standing", "Anchor_Mom_Counter"),
                F(FigureId.Daughter, "Daughter_Seated", "Anchor_Daughter_Chair"),
                F(FigureId.Baby, "Baby_Lying", "Anchor_Baby_Bassinet"),
            }, false);

            Write(2, "02:00", new[]
            {
                T("latch_kitchen_window", "Check kitchen window latch"), T("lamp_study_off", "Turn off study lamp"),
                T("latch_utility_window", "Check utility window latch"), T("lock_back", "Lock back door"),
            }, new[]
            {
                F(FigureId.Dad, "Dad_Seated", "Anchor_Dad_Armchair", eyes: true, breathe: true, blink: true),
                F(FigureId.Mom, "Mom_Standing", "Anchor_Mom_FacingHall", eyes: true, blink: true),
                F(FigureId.Daughter, "Daughter_Seated", "Anchor_Daughter_Chair", eyes: true, blink: true, tilt: 8f),
                F(FigureId.Baby, "Baby_Lying", "Anchor_Baby_Bassinet", eyes: true, blink: true),
            }, true);

            Write(3, "03:00", new[]
            {
                T("latch_kitchen_window", "Check kitchen window latch"), T("count_figures", "Confirm display figures present (4)"),
                T("lamp_study_off", "Turn off study lamp"), T("lock_back", "Lock back door"),
            }, new[]
            {
                F(FigureId.Dad, "Dad_Standing", "Anchor_Dad_LivingDoorway", eyes: true, breathe: true, blink: true, neck: 0.35f),
                F(FigureId.Mom, "Mom_Standing", "Anchor_Mom_BackHallDoor", eyes: true, blink: true, smile: 0.40f),
                F(FigureId.Baby, "Baby_Lying", "Anchor_Baby_Bassinet", eyes: true, blink: true),
            }, false);

            AssetDatabase.SaveAssets();
            Debug.Log("[ModelHome] Lap staging assets written.");
        }

        static ChecklistTaskDef T(string id, string label) => new ChecklistTaskDef { id = id, label = label };

        static FigureStaging F(FigureId id, string variant, string anchor, bool eyes = false, bool breathe = false,
            bool blink = false, float tilt = 0f, float neck = 0f, float smile = 0f) => new FigureStaging
        {
            figure = id, variant = variant, anchor = anchor, eyeTracking = eyes, breathing = breathe,
            blinkOnGlanceAway = blink, headTiltDeg = tilt, neckLong01 = neck, smileWide01 = smile,
        };

        static void Write(int n, string clock, ChecklistTaskDef[] tasks, FigureStaging[] figures, bool inhale)
        {
            var path = $"{Dir}/Lap{n}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<LapStaging>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<LapStaging>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.clockText = clock;
            asset.tasks = tasks;
            asset.figures = figures;
            asset.inhaleCueNearDaughter = inhale;
            EditorUtility.SetDirty(asset);
        }
    }
}
