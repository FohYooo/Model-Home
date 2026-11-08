namespace ModelHome
{
    public class DoorLock : WorldSwitch, IResettable
    {
        public string doorName = "door";
        public bool Locked { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            UpdatePrompt();
        }

        protected override void OnUse()
        {
            Locked = !Locked;
            Play();
            Say(Locked ? "Locked." : "Unlocked.");
            UpdatePrompt();
            if (Locked) it.ReportTaskDone();
        }

        public void ResetForLap()
        {
            Locked = false;
            UpdatePrompt();
        }

        void UpdatePrompt() => it.Prompt = (Locked ? "Unlock " : "Lock ") + doorName;
    }
}
