using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ModelHome.EditorTools
{
    /// <summary>Play-mode verification helpers (plan Task 12). They act through the real world objects.</summary>
    public static class DebugTools
    {
        [MenuItem("Model Home/Debug/Complete Current Lap")]
        public static void CompleteCurrentLapMenu() => Debug.Log(CompleteCurrentLap());

        public static string CompleteCurrentLap()
        {
            if (!Application.isPlaying) return "play mode only";
            var d = Object.FindAnyObjectByType<LapDirector>();
            var its = Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None);
            string toasts = "";
            foreach (var task in d.Current.tasks)
            {
                var it = its.FirstOrDefault(i => i.TaskId == task.id);
                if (it == null) return $"no interactable for {task.id}";
                var door = it.GetComponent<DoorLock>();
                var lamp = it.GetComponent<LampSwitch>();
                if (door != null && door.Locked) continue;
                if (lamp != null && !lamp.On) continue;
                it.Interact();
                var hud = Object.FindAnyObjectByType<Hud>();
                if (hud && hud.toast) toasts += $"[{task.id}: {hud.toast.text}] ";
            }
            return $"{d.Model.Phase}: remaining {d.checklist.Model.RemainingCount} {toasts}";
        }

        public static string TurnIn()
        {
            var clip = Object.FindObjectsByType<GuardClipboard>(FindObjectsSortMode.None).Single();
            clip.GetComponent<Interactable>().Interact();
            var d = Object.FindAnyObjectByType<LapDirector>();
            return $"phase {d.Model.Phase} transitioning {d.Transitioning}";
        }
    }
}
