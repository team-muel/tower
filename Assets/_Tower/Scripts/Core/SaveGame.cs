using System;

namespace Tower.Core
{
    // v0 checkpoint save DTO, serialized with JsonUtility (public fields,
    // [Serializable]). Character definitions are referenced by id and
    // resolved on load, so the file stays plain data.
    //
    // T12 adds hiddenMissingIds. The version stays 1: older saves simply
    // deserialize with an empty array, which is the correct default
    // (no hidden-missing presets recorded yet).
    [Serializable]
    public sealed class SaveGame
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int stairwayCount;
        public int stairwayIndex;
        public int floorCount;
        public int floorIndex;
        public int retreatCount;
        public bool isComplete;
        public SaveMember[] roster = new SaveMember[0];
        public SaveMember[] initialRoster = new SaveMember[0];
        public string[] missingIds = new string[0];
        public string[] hiddenMissingIds = new string[0];
        public string[] fallenIds = new string[0];
        public int[] shortcutStairways = new int[0];
    }

    [Serializable]
    public sealed class SaveMember
    {
        public string unitId;
        public string characterId;
        public int currentHp;
        public int deathCount;
        public int slotCount;
    }
}
