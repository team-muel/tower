using System;
using Tower.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Tower.UI
{
    public sealed class SkipButton : MonoBehaviour
    {
        private Button _button;
        private Text _label;

        public void Initialize(Action skipAction)
        {
            _button = GetComponentInChildren<Button>(true);
            _label = GetComponentInChildren<Text>(true);

            if (_label != null)
            {
                _label.text = "스킵";
            }

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() =>
                {
                    if (skipAction != null)
                    {
                        skipAction();
                    }
                });
            }
        }
    }
}
