using System;
using System.Collections.Generic;

namespace Tower.Gen
{
    // T25: the set of portals a room presents when its doors open. Lightweight
    // wrapper around the ordered PortalDef list (one per door) so callers can
    // query offers by door index without touching generation internals.
    public sealed class PortalOffer
    {
        private static readonly PortalDef[] EmptyPortals = new PortalDef[0];

        public PortalOffer(int roomId, IReadOnlyList<PortalDef> portals)
        {
            if (roomId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(roomId), "Room id cannot be negative.");
            }

            if (portals == null)
            {
                throw new ArgumentNullException(nameof(portals));
            }

            for (int i = 0; i < portals.Count; i++)
            {
                if (portals[i] == null)
                {
                    throw new ArgumentException("Portal offers cannot contain null portals.", nameof(portals));
                }

                if (portals[i].FromRoomId != roomId)
                {
                    throw new ArgumentException("All portals in an offer must originate from the same room.", nameof(portals));
                }
            }

            RoomId = roomId;
            Portals = new List<PortalDef>(portals);
        }

        public int RoomId { get; }

        public IReadOnlyList<PortalDef> Portals { get; }

        public int Count => Portals.Count;

        public static PortalOffer Empty(int roomId)
        {
            return new PortalOffer(roomId, EmptyPortals);
        }

        public PortalDef ForDoor(int doorIndex)
        {
            for (int i = 0; i < Portals.Count; i++)
            {
                if (Portals[i].DoorIndex == doorIndex)
                {
                    return Portals[i];
                }
            }

            return null;
        }
    }
}
