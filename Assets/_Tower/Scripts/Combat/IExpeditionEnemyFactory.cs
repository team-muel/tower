using Tower.Core;

namespace Tower.Combat
{
    // Supplies enemy character states for encounter kind slots produced by
    // the floor generator ("melee", "ranged", "elite", "boss", ...). Data
    // lives behind the implementation (ScriptableObjects, test doubles) so
    // the runner stays free of per-kind branches.
    public interface IExpeditionEnemyFactory
    {
        Result<CharacterState> Create(string kindSlot, int stairwayIndex, int floorIndex);
    }
}
