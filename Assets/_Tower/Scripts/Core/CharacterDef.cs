using UnityEngine;

namespace Tower.Core
{
    [CreateAssetMenu(menuName = "Tower/Core/Character", fileName = "CharacterDef")]
    public sealed class CharacterDef : ScriptableObject
    {
        // Faction id 0 means unaffiliated; 1..3 are the v0 placeholder factions.
        public const int NoFactionId = 0;

        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private int maxHp = 1;
        [SerializeField] private int attack;
        [SerializeField] private int defense;
        [SerializeField] private int speed;
        [SerializeField] private DispositionType disposition;
        [SerializeField] private PassiveDef passive;
        [SerializeField] private AbilityDef[] defaultAbilities;
        [SerializeField] private bool isReturner;
        [SerializeField] private bool chainLocked;

        // T12: preset companions are the hand-authored origin-style members
        // (one per faction). They secretly ignore the permanent three-death
        // missing rule — hidden from the player, see ExpeditionRules.
        [SerializeField] private bool isPreset;
        [SerializeField] private int factionId = NoFactionId;

        public string Id => id;
        public string DisplayName => displayName;
        public int MaxHp => maxHp;
        public int Attack => attack;
        public int Defense => defense;
        public int Speed => speed;
        public DispositionType Disposition => disposition;
        public PassiveDef Passive => passive;
        public AbilityDef[] DefaultAbilities => defaultAbilities;
        public bool IsReturner => isReturner;
        public bool ChainLocked => chainLocked;
        public bool IsPreset => isPreset;
        public int FactionId => factionId;

        public static CharacterDef CreateRuntime(
            string id,
            string displayName,
            int maxHp,
            int attack,
            int defense,
            int speed,
            DispositionType disposition,
            AbilityDef[] defaultAbilities,
            PassiveDef passive = null,
            bool isReturner = false,
            bool isPreset = false,
            int factionId = NoFactionId,
            bool chainLocked = false)
        {
            var definition = CreateInstance<CharacterDef>();
            definition.id = id;
            definition.displayName = displayName ?? id;
            definition.maxHp = maxHp;
            definition.attack = attack;
            definition.defense = defense;
            definition.speed = speed;
            definition.disposition = disposition;
            definition.passive = passive;
            definition.defaultAbilities = defaultAbilities;
            definition.isReturner = isReturner;
            definition.isPreset = isPreset;
            definition.factionId = factionId;
            definition.chainLocked = chainLocked;
            return definition;
        }
    }
}
