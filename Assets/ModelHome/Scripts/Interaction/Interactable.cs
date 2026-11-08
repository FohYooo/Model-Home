using System;
using UnityEngine;

namespace ModelHome
{
    /// <summary>Something the guard can use with E. World components decide when the use counts as
    /// doing the object's checklist task and call <see cref="ReportTaskDone"/>.</summary>
    public class Interactable : MonoBehaviour
    {
        public string Prompt = "Use";
        public string TaskId;
        public bool Enabled = true;

        public event Action<Interactable> Interacted;
        public event Action<string> TaskDone;

        const float Cooldown = 0.35f;
        float lastUse = -10f;

        public void Interact()
        {
            if (!Enabled || Time.unscaledTime - lastUse < Cooldown) return;
            lastUse = Time.unscaledTime;
            Interacted?.Invoke(this);
        }

        public void ReportTaskDone()
        {
            if (!string.IsNullOrEmpty(TaskId)) TaskDone?.Invoke(TaskId);
        }
    }
}
