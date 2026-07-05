using System;
using System.Collections.Generic;
using System.Linq;
using Tower.Core;

namespace Tower.Core
{
    public sealed class OrderBoard
    {
        public const int DefaultCombatOrders = 2;
        public const string FocusOrderType = "Focus";

        [Serializable]
        public struct OrderRecord
        {
            public string OrderType { get; }
            public string TargetUnitId { get; }
            public int ExpiresAtRound { get; }

            public OrderRecord(string orderType, string targetUnitId, int expiresAtRound)
            {
                OrderType = orderType ?? string.Empty;
                TargetUnitId = targetUnitId ?? string.Empty;
                ExpiresAtRound = expiresAtRound;
            }
        }

        public int CombatOrderLimit { get; }
        public IReadOnlyList<OrderRecord> ActiveOrders => _activeOrders;

        private readonly List<OrderRecord> _activeOrders;
        private int _remainingOrders;

        public OrderBoard(int combatOrderLimit = DefaultCombatOrders)
        {
            if (combatOrderLimit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(combatOrderLimit));
            }

            CombatOrderLimit = combatOrderLimit;
            _remainingOrders = combatOrderLimit;
            _activeOrders = new List<OrderRecord>();
        }

        public static OrderBoard CreateDefault()
        {
            return new OrderBoard(DefaultCombatOrders);
        }

        public int RemainingOrders()
        {
            return _remainingOrders;
        }

        public bool HasFocus(string targetUnitId)
        {
            if (string.IsNullOrEmpty(targetUnitId))
            {
                return false;
            }

            return _activeOrders.Any(order =>
                string.Equals(order.OrderType, FocusOrderType, StringComparison.Ordinal)
                && string.Equals(order.TargetUnitId, targetUnitId, StringComparison.Ordinal));
        }

        public bool HasActiveOrders()
        {
            return _activeOrders.Count > 0;
        }

        public IReadOnlyList<OrderRecord> GetActiveOrders()
        {
            return _activeOrders;
        }

        public Result StartNewCombat()
        {
            if (!HasActiveOrders() && _remainingOrders == CombatOrderLimit)
            {
                return Result.Success();
            }

            _activeOrders.Clear();
            _remainingOrders = CombatOrderLimit;
            return Result.Success();
        }

        public Result IssueFocus(string targetUnitId, int expiresOnRound)
        {
            if (string.IsNullOrWhiteSpace(targetUnitId))
            {
                return Result.Failure("Focus order target is required.");
            }

            if (expiresOnRound <= 0)
            {
                return Result.Failure("Expiration round must be positive.");
            }

            if (_remainingOrders <= 0)
            {
                return Result.Failure("No orders remaining this combat.");
            }

            for (int index = _activeOrders.Count - 1; index >= 0; index--)
            {
                if (string.Equals(_activeOrders[index].TargetUnitId, targetUnitId, StringComparison.Ordinal)
                    && string.Equals(_activeOrders[index].OrderType, FocusOrderType, StringComparison.Ordinal))
                {
                    _activeOrders[index] = new OrderRecord(FocusOrderType, targetUnitId, expiresOnRound);
                    return Result.Success();
                }
            }

            _activeOrders.Add(new OrderRecord(FocusOrderType, targetUnitId, expiresOnRound));
            _remainingOrders -= 1;
            return Result.Success();
        }

        public Result ConsumeActiveOrders()
        {
            if (_activeOrders.Count == 0)
            {
                return Result.Success();
            }

            _activeOrders.Clear();
            return Result.Success();
        }

        public IReadOnlyList<OrderRecord> AdvanceRound(int currentRound)
        {
            var remaining = new List<OrderRecord>();
            for (int index = 0; index < _activeOrders.Count; index++)
            {
                if (currentRound < _activeOrders[index].ExpiresAtRound)
                {
                    remaining.Add(_activeOrders[index]);
                }
            }

            _activeOrders.Clear();
            _activeOrders.AddRange(remaining);
            return _activeOrders;
        }

        public IReadOnlyList<OrderRecord> EndCombat()
        {
            var finalOrders = SnapshotActiveOrders();
            _activeOrders.Clear();
            _remainingOrders = CombatOrderLimit;
            return finalOrders;
        }

        private IReadOnlyList<OrderRecord> SnapshotActiveOrders()
        {
            return _activeOrders;
        }
    }
}
