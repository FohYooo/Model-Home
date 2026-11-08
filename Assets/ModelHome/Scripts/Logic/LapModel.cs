using System;

namespace ModelHome
{
    public enum LapPhase { Lap1, Lap2, Lap3, Ending }

    /// <summary>Lap state machine: Lap1 -> Lap2 -> Lap3 -> Ending (terminal). Pure C#.</summary>
    public class LapModel
    {
        public LapPhase Phase { get; private set; } = LapPhase.Lap1;
        public event Action<LapPhase> PhaseChanged;

        /// <summary>Advances one phase when the current lap's checklist is complete. False otherwise or at Ending.</summary>
        public bool TryAdvance(bool checklistComplete)
        {
            if (!checklistComplete || Phase == LapPhase.Ending) return false;
            Phase = Phase + 1;
            PhaseChanged?.Invoke(Phase);
            return true;
        }
    }
}
