using System;
using Tower.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Tower.UI
{
    public sealed class TurnOrderSlot : MonoBehaviour
    {
        private Text _label;

        public void Initialize(string unitId, bool isActive)
        {
            _label = GetComponentInChildren<Text>(true);
            if (_label != null)
            {
                _label.text = (isActive ? "> " : "") + (!string.IsNullOrEmpty(unitId) ? unitId : "???");
            }
        }
    }
}
