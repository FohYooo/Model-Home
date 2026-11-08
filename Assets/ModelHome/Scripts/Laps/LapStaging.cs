using System;
using UnityEngine;

namespace ModelHome
{
    [Serializable]
    public struct ChecklistTaskDef
    {
        public string id;
        public string label;
    }

    /// <summary>Where one figure variant stands this lap and what it does. Variants not listed are hidden.</summary>
    [Serializable]
    public class FigureStaging
    {
        public FigureId figure;
        public string variant;
        public string anchor;
        public bool eyeTracking;
        public bool breathing;
        public bool blinkOnGlanceAway;
        public float headTiltDeg;
        [Range(0, 1)] public float neckLong01;
        [Range(0, 1)] public float smileWide01;
    }

    /// <summary>Data for one lap: the clock shown on the cut, the round sheet, and the figures' staging.</summary>
    [CreateAssetMenu(menuName = "Model Home/Lap Staging")]
    public class LapStaging : ScriptableObject
    {
        public string clockText;
        public ChecklistTaskDef[] tasks;
        public FigureStaging[] figures;
        public bool inhaleCueNearDaughter;
    }
}
