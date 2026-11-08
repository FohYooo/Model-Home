namespace ModelHome
{
    public class WindowLatch : WorldSwitch, IResettable
    {
        public bool Latched { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            it.Prompt = "Check window latch";
        }

        protected override void OnUse()
        {
            Latched = true;
            Play();
            Say("Latched.");
            it.ReportTaskDone();
        }

        public void ResetForLap() => Latched = false;
    }
}
