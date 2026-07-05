using System;
using System.Collections.Generic;
using Tower.Combat;
using Tower.Core;
using UnityEngine;

namespace Tower.UI
{
    // v0 HUD: order buttons for active orders, turn-order strip, skip button.
    // Templates are optional; missing templates simply render nothing.
    public sealed class CombatHudController : MonoBehaviour
    {
        [SerializeField] private Transform _orderButtonContainer;
        [SerializeField] private OrderButton _orderButtonTemplate;
        [SerializeField] private Transform _turnOrderContainer;
        [SerializeField] private TurnOrderSlot _turnSlotTemplate;
        [SerializeField] private SkipButton _skipButtonTemplate;
        [SerializeField] private CombatDemoBootstrap _demoBootstrap;

        private readonly List<OrderButton> _orderButtons = new();
        private readonly List<TurnOrderSlot> _turnSlots = new();
        private IReadOnlyList<OrderBoard.OrderRecord> _activeOrders = Array.Empty<OrderBoard.OrderRecord>();

        public void Initialize(CombatDemoBootstrap demoBootstrap)
        {
            _demoBootstrap = demoBootstrap;
            RefreshOrderButtons();
            CreateTurnSlots(Array.Empty<string>());
        }

        public void ShowOrderSlots(IReadOnlyList<OrderBoard.OrderRecord> activeOrders)
        {
            _activeOrders = activeOrders ?? Array.Empty<OrderBoard.OrderRecord>();
            RefreshOrderButtons();
        }

        public void ShowTurnOrder(IReadOnlyList<string> order)
        {
            CreateTurnSlots(order ?? Array.Empty<string>());
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

            foreach (var record in _activeOrders)
            {
                OrderButton button = Instantiate(_orderButtonTemplate, _orderButtonContainer);
                button.Initialize(record.OrderType, record.TargetUnitId, OnOrderSent);
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

            for (int index = 0; index < order.Count; index++)
            {
                TurnOrderSlot slot = Instantiate(_turnSlotTemplate, _turnOrderContainer);
                slot.Initialize(order[index], index == 0);
                slot.gameObject.SetActive(true);
                _turnSlots.Add(slot);
            }
        }

        private void OnOrderSent(string targetUnitId)
        {
            if (_demoBootstrap != null)
            {
                _demoBootstrap.IssueOrderToCompanions(targetUnitId);
            }
        }

        private void OnSkipRequested()
        {
            if (_demoBootstrap != null)
            {
                _demoBootstrap.SkipHighlighting();
            }
        }
    }
}
