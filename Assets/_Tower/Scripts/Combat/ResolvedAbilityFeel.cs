namespace Tower.Combat
{
    public readonly struct ResolvedAbilityFeel
    {
        public ResolvedAbilityFeel(
            int hitstopMs,
            float shakeIntensity,
            DamagePopupStyle popupStyle,
            AbilityApproachTween approachTween,
            string sfxCue)
        {
            HitstopMs = hitstopMs < 0 ? 0 : hitstopMs;
            ShakeIntensity = shakeIntensity < 0f ? 0f : shakeIntensity;
            PopupStyle = popupStyle;
            ApproachTween = approachTween;
            SfxCue = sfxCue ?? string.Empty;
        }

        public int HitstopMs { get; }
        public float ShakeIntensity { get; }
        public DamagePopupStyle PopupStyle { get; }
        public AbilityApproachTween ApproachTween { get; }
        public string SfxCue { get; }
    }
}
