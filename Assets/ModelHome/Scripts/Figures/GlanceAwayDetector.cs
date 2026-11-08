namespace ModelHome
{
    /// <summary>Fires once when the player's gaze moves off a figure that is still on screen
    /// (its angle from the view centre crosses gazeAngle), at most once per cooldown.</summary>
    public class GlanceAwayDetector
    {
        readonly float gazeAngle;
        readonly float cooldown;
        bool wasCentred;
        float lastFire = float.NegativeInfinity;

        public GlanceAwayDetector(float gazeAngleDeg = 25f, float cooldown = 5f)
        {
            gazeAngle = gazeAngleDeg;
            this.cooldown = cooldown;
        }

        public bool Update(bool onScreen, float angleFromViewCentreDeg, float time)
        {
            if (!onScreen)
            {
                wasCentred = false;
                return false;
            }
            bool centred = angleFromViewCentreDeg < gazeAngle;
            bool fire = wasCentred && !centred && time - lastFire >= cooldown;
            wasCentred = centred;
            if (fire) lastFire = time;
            return fire;
        }
    }
}
