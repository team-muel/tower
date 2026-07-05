using System;
using System.Collections.Generic;
using Tower.Core;
using UnityEngine;

namespace Tower.UI
{
    public sealed class CombatHudController : MonoBehaviour
    {
        [Serializable]
        public struct OrderReference
        {
            public UnitToken OrderSource;
            public UnitToken OrderTarget;
        }

        [SerializeField] private Transform _orderButtonContainer;
        [SerializeField] private OrderButton _orderButtonTemplate;
        [SerializeField] private Transform _turnOrderContainer;
        [SerializeField] private TurnOrderSlot _turnSlotTemplate;
        [SerializeField] private SkipButton _skipButtonTemplate;
        [SerializeField] private CommandLogPresenter _commandLogPresenter;
        [SerializeField] private Tower.Core.CombatDemoBootstrap _demoBootstrap;

        private readonly List<OrderButton> _orderButtons = new();
        private readonly List<TurnOrderSlot> _turnSlots = new();
        private readonly List<SkipButton> _skipButtons = new();
        private IReadOnlyDictionary<string, OrderBoard.OrderRecord> _activeOrders = Array.Empty<KeyValuePair<string, OrderBoard.OrderRecord>>();

        public void Initialize(CombatDemoBootstrap demoBootstrap)
        {
            _demoBootstrap = demoBootstrap;
            RefreshOrderButtons();
            CreateTurnSlots(Array.Empty<string>());
        }

        public void ShowOrderSlots(Tower.Core.CombatDemoBootstrap bootstrap, IReadOnlyDictionary<string, OrderBoard.OrderRecord> orderRegistry)
        {
            _activeOrders = orderRegistry ?? Array.Empty<KeyValuePair<string, OrderBoard.OrderRecord>>();
            RefreshOrderButtons();
        }

        public void ShowTurnOrder(IReadOnlyList<string> order)
        {
            CreateTurnSlots(order);
        }

        public void Refresh()
        {
            RefreshOrderButtons();
        }

        private void RefreshOrderButtons()
        {
            for (int index = _orderButtons.Count - 1; index >= 0; index--)
            {
                Destroy(_orderButtons[index].gameObject);
            }

            _orderButtons.Clear();
            if (_orderButtonTemplate == null)
            {
                return;
            }

            foreach (var entry in _activeOrders)
            {
                OrderButton button = Instantiate(_orderButtonTemplate, _orderButtonContainer);
                button.Initialize(entry.Key, entry.Value, OnOrderSent);
                button.gameObject.SetActive(true);
                _orderButtons.Add(button);
            }
        }

        private void CreateTurnSlots(IReadOnlyList<string> order)
        {
            for (int index = _turnSlots.Count - 1; index >= 0; index--)
            {
                Destroy(_turnSlots[index].gameObject);
            }

            _turnSlots.Clear();
            if (_turnSlotTemplate == null)
            {
                return;
            }

            if (_skipButtonTemplate != null)
            {
                SkipButton skipButton = Instantiate(_skipButtonTemplate, _turnOrderContainer);
                skipButton.Initialize(OnSkipRequested);

            }

            int slotIndex = _orderButtons.Count;
            for (int index = 0; index < order.Count; index++)
            {
                TurnOrderSlot slot = Instantiate(_turnSlotTemplate, _turnOrderContainer);
                slot.Initialize(order[index], false);
                slot.gameObject.SetActive(true);
                _turnSlots.Add(slot);
            }
        }

        private void OnOrderSent(string targetUnitId, OrderBoard.OrderRecord record)
        {
            _demoBootstrap.IssueOrderToCompanions(targetUnitId);
        }

        private void OnSkipRequested()
        {
            _demoBootstrap.SkipHighlighting();
        }
    }
}
