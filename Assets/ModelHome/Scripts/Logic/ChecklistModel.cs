using System;
using System.Collections.Generic;

namespace ModelHome
{
    public readonly struct ChecklistTask
    {
        public readonly string Id;
        public readonly string Label;

        public ChecklistTask(string id, string label)
        {
            Id = id;
            Label = label;
        }
    }

    /// <summary>The guard's round sheet for one lap: which tasks exist and which are done. Pure C#.</summary>
    public class ChecklistModel
    {
        readonly List<ChecklistTask> tasks = new List<ChecklistTask>();
        readonly HashSet<string> done = new HashSet<string>();

        public event Action<string> TaskCompleted;

        public ChecklistModel(IEnumerable<ChecklistTask> initial) => Load(initial);

        public IReadOnlyList<ChecklistTask> Tasks => tasks;
        public int RemainingCount => tasks.Count - done.Count;
        public bool AllComplete => RemainingCount == 0;

        public bool IsDone(string id) => done.Contains(id);

        /// <summary>Marks a task done. False if the id is not on this lap's sheet or is already done.</summary>
        public bool Complete(string id)
        {
            if (id == null || !tasks.Exists(t => t.Id == id) || !done.Add(id)) return false;
            TaskCompleted?.Invoke(id);
            return true;
        }

        public void Load(IEnumerable<ChecklistTask> next)
        {
            tasks.Clear();
            done.Clear();
            if (next != null) tasks.AddRange(next);
        }
    }
}
