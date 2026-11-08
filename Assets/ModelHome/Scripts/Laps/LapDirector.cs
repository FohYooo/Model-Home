using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace ModelHome
{
    /// <summary>Runs the three laps. Every macro change (figure placement, proportions, house reset)
    /// happens only while the screen is fully black.</summary>
    public class LapDirector : MonoBehaviour
    {
        public LapStaging[] laps;
        public DisplayFigure[] figures;
        public Transform anchorsRoot;
        public Checklist checklist;
        public Hud hud;
        public PlayerController player;

        public event Action EndingRequested;
        public LapModel Model { get; } = new LapModel();
        public LapStaging Current => laps[Mathf.Min((int)Model.Phase, laps.Length - 1)];
        public bool Transitioning { get; private set; }

        IEnumerator Start()
        {
            Transitioning = true;
            if (player) player.InputEnabled = false;
            if (hud && hud.Fade) hud.Fade.alpha = 1f;
            ApplyLap(laps[0]);
            yield return ShowClock(laps[0].clockText);
            yield return hud ? hud.FadeTo(0f, 1.5f) : null;
            if (player) player.InputEnabled = true;
            Transitioning = false;
        }

        /// <summary>Called by the guard-post clipboard.</summary>
        public void TryTurnIn()
        {
            if (Transitioning || Model.Phase == LapPhase.Ending) return;
            if (!checklist.Model.AllComplete)
            {
                if (hud) hud.Toast($"Tasks remaining: {checklist.Model.RemainingCount}");
                return;
            }
            if (Model.Phase == LapPhase.Lap3)
            {
                Model.TryAdvance(true);
                EndingRequested?.Invoke();
                return;
            }
            StartCoroutine(Transition());
        }

        IEnumerator Transition()
        {
            Transitioning = true;
            if (player) player.InputEnabled = false;
            if (hud) yield return hud.FadeTo(1f, 1.2f);
            Model.TryAdvance(true);
            ApplyLap(Current);          // screen is fully black here
            yield return ShowClock(Current.clockText);
            if (hud) yield return hud.FadeTo(0f, 1.2f);
            if (player) player.InputEnabled = true;
            Transitioning = false;
        }

        IEnumerator ShowClock(string text)
        {
            if (hud == null || hud.Clock == null) yield break;
            hud.Clock.text = text;
            hud.Clock.enabled = true;
            yield return new WaitForSeconds(1.5f);
            hud.Clock.enabled = false;
        }

        public void ApplyLap(LapStaging staging)
        {
            foreach (var fig in figures)
            {
                var s = staging.figures.FirstOrDefault(f => f.variant == fig.Variant);
                fig.gameObject.SetActive(s != null);
                if (s == null) continue;
                var anchor = FindAnchor(s.anchor);
                if (anchor) fig.PlaceAt(anchor);
                else Debug.LogError($"[ModelHome] anchor {s.anchor} not found for {fig.Variant}");
                fig.EyeTracking = s.eyeTracking;
                fig.Breathing = s.breathing;
                fig.BlinkOnGlanceAway = s.blinkOnGlanceAway;
                fig.HeadTiltDeg = s.headTiltDeg;
                fig.NeckLong01 = s.neckLong01;
                fig.SmileWide01 = s.smileWide01;
                fig.ApplyPose();
            }
            foreach (var r in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).OfType<IResettable>())
                r.ResetForLap();
            checklist.Load(staging);
        }

        public int VisibleFigureCount() => figures.Where(f => f.gameObject.activeSelf).Select(f => f.Id).Distinct().Count();

        Transform FindAnchor(string n) => anchorsRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
    }
}
