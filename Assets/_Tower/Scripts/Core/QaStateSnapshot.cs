using System.Collections.Generic;

namespace Tower.Core
{
    // Mutable DTO filled by registered state contributors and serialized by
    // QaStateSerializer. Public fields follow the SaveGame DTO convention.
    public sealed class QaStateSnapshot
    {
        public string sceneName = string.Empty;
        public QaCombatSnapshot combat;
        public QaExpeditionSnapshot expedition;
        public QaCampSnapshot camp;
    }

    public sealed class QaCombatSnapshot
    {
        public int round;
        public string activeUnitId = string.Empty;
        public int remainingOrders;
        // T19: bullet-time command mode overlay toggle state.
        public bool commandMode;
        // T20: active battlefield implementation ("Grid" or "Analog").
        public string spaceMode = string.Empty;
        public List<string> initiativeOrder = new List<string>();
        public List<QaUnitSnapshot> units = new List<QaUnitSnapshot>();
    }

    public sealed class QaUnitSnapshot
    {
        public string unitId = string.Empty;
        public string team = string.Empty;
        public int currentHp;
        public int maxHp;
        public bool alive;
        // T20: continuous battlefield coordinates (grid mode reports cell
        // centers); -1/-1 when the unit is not on the battlefield.
        public float x;
        public float y;
        public List<string> marks = new List<string>();
        // T19: the engine's pending ability when this unit holds the active
        // turn; empty otherwise (the engine tracks one pending pick at a time).
        public string pendingAbility = string.Empty;
    }

    public sealed class QaExpeditionSnapshot
    {
        public int stairwayIndex;
        public int stairwayCount;
        public int floorIndex;
        public int floorCount;
        public int roomIndex;
        public int roomCount;
        public int retreatCount;
        public bool isComplete;
        public string phase = string.Empty;
        public string nextRoomPreview = string.Empty;
        public string lastOutcome = string.Empty;
    }

    // Camp hub scene state: regressor ground position + active interaction zone.
    public sealed class QaCampSnapshot
    {
        public float x;
        public float z;
        public string zoneId = string.Empty;
    }
}
