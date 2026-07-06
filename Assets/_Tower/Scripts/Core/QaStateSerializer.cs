using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Tower.Core
{
    // Hand-rolled single-line JSON writer so Tower.Core stays engine-free
    // (JsonUtility lives in UnityEngine) and the output is deterministic.
    public static class QaStateSerializer
    {
        public static string ToJson(QaStateSnapshot snapshot)
        {
            snapshot = snapshot ?? new QaStateSnapshot();
            var builder = new StringBuilder(256);
            builder.Append("{\"sceneName\":");
            WriteString(builder, snapshot.sceneName);
            builder.Append(",\"combat\":");
            WriteCombat(builder, snapshot.combat);
            builder.Append(",\"expedition\":");
            WriteExpedition(builder, snapshot.expedition);
            builder.Append(",\"camp\":");
            WriteCamp(builder, snapshot.camp);
            builder.Append('}');
            return builder.ToString();
        }

        private static void WriteCombat(StringBuilder builder, QaCombatSnapshot combat)
        {
            if (combat == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append("{\"round\":").Append(combat.round.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"activeUnitId\":");
            WriteString(builder, combat.activeUnitId);
            builder.Append(",\"remainingOrders\":").Append(combat.remainingOrders.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"commandMode\":").Append(combat.commandMode ? "true" : "false");
            builder.Append(",\"initiativeOrder\":");
            WriteStringArray(builder, combat.initiativeOrder);
            builder.Append(",\"units\":[");
            var units = combat.units ?? new List<QaUnitSnapshot>();
            for (var index = 0; index < units.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                WriteUnit(builder, units[index]);
            }

            builder.Append("]}");
        }

        private static void WriteUnit(StringBuilder builder, QaUnitSnapshot unit)
        {
            if (unit == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append("{\"unitId\":");
            WriteString(builder, unit.unitId);
            builder.Append(",\"team\":");
            WriteString(builder, unit.team);
            builder.Append(",\"currentHp\":").Append(unit.currentHp.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"maxHp\":").Append(unit.maxHp.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"alive\":").Append(unit.alive ? "true" : "false");
            builder.Append(",\"x\":").Append(unit.x.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"y\":").Append(unit.y.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"marks\":");
            WriteStringArray(builder, unit.marks);
            builder.Append(",\"pendingAbility\":");
            WriteString(builder, unit.pendingAbility);
            builder.Append('}');
        }

        private static void WriteExpedition(StringBuilder builder, QaExpeditionSnapshot expedition)
        {
            if (expedition == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append("{\"stairwayIndex\":").Append(expedition.stairwayIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"stairwayCount\":").Append(expedition.stairwayCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"floorIndex\":").Append(expedition.floorIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"floorCount\":").Append(expedition.floorCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"roomIndex\":").Append(expedition.roomIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"roomCount\":").Append(expedition.roomCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"retreatCount\":").Append(expedition.retreatCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\"isComplete\":").Append(expedition.isComplete ? "true" : "false");
            builder.Append(",\"phase\":");
            WriteString(builder, expedition.phase);
            builder.Append(",\"nextRoomPreview\":");
            WriteString(builder, expedition.nextRoomPreview);
            builder.Append(",\"lastOutcome\":");
            WriteString(builder, expedition.lastOutcome);
            builder.Append('}');
        }

        private static void WriteCamp(StringBuilder builder, QaCampSnapshot camp)
        {
            if (camp == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append("{\"x\":").Append(camp.x.ToString("0.##", CultureInfo.InvariantCulture));
            builder.Append(",\"z\":").Append(camp.z.ToString("0.##", CultureInfo.InvariantCulture));
            builder.Append(",\"zoneId\":");
            WriteString(builder, camp.zoneId);
            builder.Append('}');
        }

        private static void WriteStringArray(StringBuilder builder, List<string> values)
        {
            builder.Append('[');
            if (values != null)
            {
                for (var index = 0; index < values.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(',');
                    }

                    WriteString(builder, values[index]);
                }
            }

            builder.Append(']');
        }

        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            if (!string.IsNullOrEmpty(value))
            {
                foreach (var character in value)
                {
                    switch (character)
                    {
                        case '"':
                            builder.Append("\\\"");
                            break;
                        case '\\':
                            builder.Append("\\\\");
                            break;
                        case '\n':
                            builder.Append("\\n");
                            break;
                        case '\r':
                            builder.Append("\\r");
                            break;
                        case '\t':
                            builder.Append("\\t");
                            break;
                        default:
                            if (character < ' ')
                            {
                                builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                            }
                            else
                            {
                                builder.Append(character);
                            }

                            break;
                    }
                }
            }

            builder.Append('"');
        }
    }
}
