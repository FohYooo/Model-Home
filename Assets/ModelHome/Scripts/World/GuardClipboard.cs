namespace ModelHome
{
    public class GuardClipboard : WorldSwitch
    {
        public LapDirector director;

        protected override void Awake()
        {
            base.Awake();
            it.Prompt = "Sign the round sheet";
        }

        protected override void OnUse()
        {
            Play(0.5f);
            if (director) director.TryTurnIn();
        }
    }
}
