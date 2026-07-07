using UnityEngine;

namespace Tower.Combat
{
    public enum DamagePopupStyle
    {
        Normal = 0,
        Crit = 1,
        Consume = 2,
        Heal = 3
    }

    public enum AbilityApproachTween
    {
        None = 0,
        Lunge = 1,
        Projectile = 2
    }

    [CreateAssetMenu(menuName = "Tower/Combat/Ability Feel", fileName = "AbilityFeelDef")]
    public sealed class AbilityFeelDef : ScriptableObject
    {
        [SerializeField] private int hitstopMs = 70;
        [SerializeField] private float shakeIntensity = 0.18f;
        [SerializeField] private DamagePopupStyle popupStyle = DamagePopupStyle.Normal;
        [SerializeField] private AbilityApproachTween approachTween = AbilityApproachTween.None;
        [SerializeField] private string sfxCue = string.Empty;

        public int HitstopMs => Mathf.Max(0, hitstopMs);
        public float ShakeIntensity => Mathf.Max(0f, shakeIntensity);
        public DamagePopupStyle PopupStyle => popupStyle;
        public AbilityApproachTween ApproachTween => approachTween;
        public string SfxCue => sfxCue ?? string.Empty;

        public static AbilityFeelDef CreateRuntime(
            int hitstopMs = 70,
            float shakeIntensity = 0.18f,
            DamagePopupStyle popupStyle = DamagePopupStyle.Normal,
            AbilityApproachTween approachTween = AbilityApproachTween.None,
            string sfxCue = "")
        {
            var feel = CreateInstance<AbilityFeelDef>();
            feel.hitstopMs = hitstopMs;
            feel.shakeIntensity = shakeIntensity;
            feel.popupStyle = popupStyle;
            feel.approachTween = approachTween;
            feel.sfxCue = sfxCue ?? string.Empty;
            return feel;
        }
    }
}
