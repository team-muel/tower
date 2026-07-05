using UnityEngine;

namespace Tower.Core
{
    [CreateAssetMenu(menuName = "Tower/Core/Character", fileName = "CharacterDef")]
    public sealed class CharacterDef : ScriptableObject
    {
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
    }
}
