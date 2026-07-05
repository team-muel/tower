using System;
using Tower.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Tower.UI
{
    public sealed class OrderButton : MonoBehaviour
    {
        private Text _primaryLabel;
        private Text _secondaryLabel;
        private Button _button;

        public void Initialize(string primaryLabel, string secondaryLabel, Action<string> onSelected)
        {
            _primaryLabel = GetComponentsInChildren<Text>(true)[0];
            _secondaryLabel = GetComponentsInChildren<Text>(true)[1];
            _button = GetComponentInChildren<Button>(true);

            if (_primaryLabel != null)
            {
                _primaryLabel.text = primaryLabel;
            }

            if (_secondaryLabel != null)
            {
                _secondaryLabel.text = secondaryLabel;
            }

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() =>
                {
                    if (onSelected != null)
                    {
                        onSelected(primaryLabel);
                    }
                });
            }
        }
    }
}
