using System.Linq;
using System.Text;
using UnityEngine;

namespace ModelHome
{
    /// <summary>The guard's round sheet in the scene: ticks tasks reported by world objects; Tab shows it.</summary>
    public class Checklist : MonoBehaviour
    {
        public Hud hud;
        public PlayerController player;

        public ChecklistModel Model { get; } = new ChecklistModel(null);
        string clock = "";

        void Start()
        {
            foreach (var it in FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                it.TaskDone += OnTaskDone;
        }

        public void Load(LapStaging staging)
        {
            clock = staging.clockText;
            Model.Load(staging.tasks.Select(t => new ChecklistTask(t.id, t.label)));
            Refresh();
        }

        void OnTaskDone(string id)
        {
            if (!Model.Complete(id)) return;
            var label = Model.Tasks.First(t => t.Id == id).Label;
            if (hud) hud.Toast("Done: " + label, 2f);
            Refresh();
        }

        void Update()
        {
            if (hud == null) return;
            hud.ShowClipboard(Input.GetKey(KeyCode.Tab) && (player == null || player.InputEnabled));
        }

        void Refresh()
        {
            if (hud == null || hud.ClipboardText == null) return;
            var sb = new StringBuilder();
            sb.AppendLine("LOT 14 — NIGHT ROUND " + clock).AppendLine();
            foreach (var t in Model.Tasks)
                sb.AppendLine(Model.IsDone(t.Id) ? $"<color=#8a8a8a>[x] {t.Label}</color>" : $"[  ] {t.Label}");
            sb.AppendLine().Append(Model.AllComplete ? "Sign the sheet at the guard post." : "");
            hud.ClipboardText.text = sb.ToString();
        }
    }
}
