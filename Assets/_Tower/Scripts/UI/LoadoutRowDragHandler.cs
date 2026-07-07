using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Tower.UI
{
    // uGUI drag-to-reorder handler attached to a single Loadout party row.
    //
    // Replaces the old ▲▼ arrow buttons: the player grabs a row and drags it up
    // or down, and the row snaps between slots as the pointer crosses the
    // neighbouring rows' centres. The live sibling reorder gives immediate
    // visual feedback under the VerticalLayoutGroup; the controller commits the
    // new order (save) on drag end.
    //
    // Rows whose member is chainLocked are never given a handler (the controller
    // simply does not attach this component), so locked members are not
    // draggable and stay excluded from the chain.
    public sealed class LoadoutRowDragHandler : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform rowRect;
        private RectTransform contentRect;
        private Func<bool> canReorder;
        private Action onPreview;
        private Action onCommit;
        private bool dragging;

        // Configure is called by the controller right after AddComponent so the
        // handler never runs with half-set fields.
        public void Configure(
            RectTransform content,
            Func<bool> canReorder,
            Action onPreview,
            Action onCommit)
        {
            rowRect = (RectTransform)transform;
            contentRect = content;
            this.canReorder = canReorder;
            this.onPreview = onPreview;
            this.onCommit = onCommit;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!Allowed())
            {
                return;
            }

            dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || contentRect == null || rowRect == null)
            {
                return;
            }

            int target = ComputeTargetIndex(eventData);
            if (target < 0)
            {
                return;
            }

            if (target != rowRect.GetSiblingIndex())
            {
                rowRect.SetSiblingIndex(target);
                onPreview?.Invoke();
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            onCommit?.Invoke();
        }

        private bool Allowed()
        {
            return canReorder == null || canReorder();
        }

        // Find the sibling slot the pointer currently sits over by comparing the
        // pointer's screen Y against each child's world-space vertical centre.
        // The list runs top-to-bottom (higher screen Y = earlier index).
        private int ComputeTargetIndex(PointerEventData eventData)
        {
            int count = contentRect.childCount;
            if (count == 0)
            {
                return -1;
            }

            var corners = new Vector3[4];
            for (int i = 0; i < count; i++)
            {
                var child = (RectTransform)contentRect.GetChild(i);
                child.GetWorldCorners(corners);
                // corners[0] = bottom-left, corners[1] = top-left.
                float centerY = (corners[0].y + corners[1].y) * 0.5f;
                if (eventData.position.y > centerY)
                {
                    return i;
                }
            }

            return count - 1;
        }
    }
}
