using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tower.UI
{
    // T14: shared hover tooltip for runtime-generated menus. One view per
    // canvas; TooltipTrigger attaches to any raycastable Graphic and feeds
    // the view prebuilt text (built via Tower.Core.AbilityDisplayText).
    internal sealed class RuntimeTooltipView : MonoBehaviour
    {
        private const float Width = 380f;
        private const float Height = 96f;
        private const float CursorOffsetX = 18f;
        private const float CursorOffsetY = -12f;

        private RectTransform rect;
        private Text label;

        public static RuntimeTooltipView Create(Transform canvasParent)
        {
            var tooltipObject = new GameObject("Ability Tooltip");
            tooltipObject.transform.SetParent(canvasParent, false);

            var background = tooltipObject.AddComponent<Image>();
            background.color = new Color(0.07f, 0.08f, 0.10f, 0.96f);
            background.raycastTarget = false;

            var rect = tooltipObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(Width, Height);

            var label = RuntimeSceneUi.AddText(tooltipObject.transform, "Label", string.Empty, 14, TextAnchor.UpperLeft);
            label.raycastTarget = false;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 8f);
            labelRect.offsetMax = new Vector2(-12f, -8f);

            var view = tooltipObject.AddComponent<RuntimeTooltipView>();
            view.rect = rect;
            view.label = label;
            tooltipObject.SetActive(false);
            return view;
        }

        public void Show(string text, Vector2 screenPosition)
        {
            label.text = text;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Move(screenPosition);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            Move(Input.mousePosition);
        }

        private void Move(Vector2 screenPosition)
        {
            var scale = rect.lossyScale;
            var width = Width * scale.x;
            var height = Height * scale.y;
            var x = Mathf.Clamp(screenPosition.x + CursorOffsetX, 0f, Mathf.Max(0f, Screen.width - width));
            var y = Mathf.Clamp(screenPosition.y + CursorOffsetY, Mathf.Min(height, Screen.height), Screen.height);
            rect.position = new Vector3(x, y, 0f);
        }
    }

    internal sealed class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private RuntimeTooltipView view;
        private string text;

        public static void Attach(GameObject target, RuntimeTooltipView view, string text)
        {
            var trigger = target.AddComponent<TooltipTrigger>();
            trigger.view = view;
            trigger.text = text;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (view != null)
            {
                view.Show(text, eventData.position);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (view != null)
            {
                view.Hide();
            }
        }

        private void OnDisable()
        {
            if (view != null)
            {
                view.Hide();
            }
        }
    }
}
