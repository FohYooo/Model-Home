namespace ModelHome
{
    /// <summary>World state the house "resets" between laps (doors unlocked, lamps back on...).</summary>
    public interface IResettable
    {
        void ResetForLap();
    }
}
