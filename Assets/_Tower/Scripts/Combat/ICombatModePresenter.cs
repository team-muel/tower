namespace Tower.Combat
{
    public interface ICombatModePresenter
    {
        float PlaybackFactor { get; set; }
        void SetMode(string mode);
    }
}
