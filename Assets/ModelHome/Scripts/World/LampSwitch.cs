using UnityEngine;

namespace ModelHome
{
    public class LampSwitch : WorldSwitch, IResettable
    {
        public Light lamp;
        public bool On => lamp != null && lamp.enabled;
        public bool PoweredDown { get; set; }

        protected override void Awake()
        {
            base.Awake();
            UpdatePrompt();
        }

        protected override void OnUse()
        {
            if (lamp == null || PoweredDown) { Say("Nothing happens."); return; }
            lamp.enabled = !lamp.enabled;
            Play(0.6f);
            UpdatePrompt();
            if (!lamp.enabled) it.ReportTaskDone();
        }

        public void ResetForLap()
        {
            if (lamp && !PoweredDown) lamp.enabled = true;
            UpdatePrompt();
        }

        void UpdatePrompt() => it.Prompt = On ? "Turn off lamp" : "Turn on lamp";
    }
}
