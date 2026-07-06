using System;
using System.Collections.Generic;

namespace Tower.Core
{
    // Registry of camp interaction zones. Ids are unique (Ordinal) so the QA
    // harness and prompt UI can address zones unambiguously.
    public sealed class CampZoneRegistry
    {
        private readonly List<CampZoneDef> zones = new List<CampZoneDef>();

        public IReadOnlyList<CampZoneDef> Zones => zones;

        public Result Add(CampZoneDef zone)
        {
            if (zone == null)
            {
                return Result.Failure("Zone is required.");
            }

            foreach (var existing in zones)
            {
                if (StringComparer.Ordinal.Equals(existing.Id, zone.Id))
                {
                    return Result.Failure($"Zone '{zone.Id}' is already registered.");
                }
            }

            zones.Add(zone);
            return Result.Success();
        }

        // Returns the containing zone whose center is closest to the point, or
        // null when the point is in no zone. Overlaps resolve to the nearest.
        public CampZoneDef FindAt(float x, float z)
        {
            CampZoneDef best = null;
            float bestDistance = float.MaxValue;
            foreach (var zone in zones)
            {
                if (!zone.Contains(x, z))
                {
                    continue;
                }

                float distance = zone.SquaredDistanceTo(x, z);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = zone;
                }
            }

            return best;
        }
    }
}
