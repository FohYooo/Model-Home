namespace ModelHome
{
    /// <summary>The dining table: "Confirm display figures present (4)". Counts honestly.</summary>
    public class FigureCount : WorldSwitch
    {
        public LapDirector director;

        protected override void Awake()
        {
            base.Awake();
            it.Prompt = "Count display figures";
        }

        protected override void OnUse()
        {
            Say($"{(director ? director.VisibleFigureCount() : 0)} present.");
            it.ReportTaskDone();
        }
    }
}
