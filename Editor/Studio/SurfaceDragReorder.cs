using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SurfaceSystem
{
    /// <summary>
    /// Drag-to-reorder for UI Toolkit lists built by hand (replaces the old IMGUI <c>GroupDragReorder</c>, which only reordered whole groups). A drag handle moves
    /// one item of a container: while the pointer is held a line shows where the item will land, and on release the callback
    /// gets the source and the final index. The container must hold the items first and the marker element last.
    /// </summary>
    internal static class SurfaceDragReorder
    {
        /// <summary>Creates the line that marks the drop position; add it as the LAST child of the container.</summary>
        public static VisualElement CreateMarker()
        {
            var marker = new VisualElement();
            marker.AddToClassList("surf-drop-marker");
            marker.pickingMode = PickingMode.Ignore;
            return marker;
        }

        /// <summary>Makes <paramref name="handle"/> drag <paramref name="item"/> inside <paramref name="container"/>.</summary>
        /// <param name="handle">The element the user grabs.</param>
        /// <param name="item">The item that moves; a direct child of <paramref name="container"/>.</param>
        /// <param name="container">Holds the items followed by <paramref name="marker"/>.</param>
        /// <param name="marker">The drop line made by <see cref="CreateMarker"/>.</param>
        /// <param name="moved">Called with (from, to) where <c>to</c> is the item's index after the move; only when it changed.</param>
        public static void Attach(VisualElement handle, VisualElement item, VisualElement container, VisualElement marker, Action<int, int> moved)
        {
            int dragged = -1;
            int pointer = -1;

            void Cancel()
            {
                if (dragged >= 0)
                {
                    item.RemoveFromClassList("surf-dragging");
                    marker.style.display = DisplayStyle.None;
                    dragged = -1;
                }
            }

            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                dragged = container.IndexOf(item);
                if (dragged < 0)
                {
                    return;
                }

                pointer = evt.pointerId;
                handle.CapturePointer(pointer);
                item.AddToClassList("surf-dragging");
                ShowMarker(container, marker, dragged, evt.position.y);
                evt.StopPropagation();
            });

            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (dragged >= 0 && handle.HasPointerCapture(pointer))
                {
                    ShowMarker(container, marker, dragged, evt.position.y);
                    evt.StopPropagation();
                }
            });

            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (dragged < 0 || evt.button != 0)
                {
                    return;
                }

                int from = dragged;
                int to = TargetIndex(container, marker, from, evt.position.y);
                Cancel();
                if (handle.HasPointerCapture(pointer))
                {
                    handle.ReleasePointer(pointer);
                }

                evt.StopPropagation();
                if (to != from)
                {
                    moved(from, to);
                }
            });

            handle.RegisterCallback<PointerCaptureOutEvent>(evt => Cancel());
        }

        // The index the dragged item ends up at: how many other items have their centre above the pointer.
        private static int TargetIndex(VisualElement container, VisualElement marker, int dragged, float pointerY)
        {
            int target = 0;
            int count = container.childCount;
            for (int i = 0; i < count; i++)
            {
                VisualElement child = container[i];
                if (i != dragged && child != marker && pointerY > child.worldBound.center.y)
                {
                    target++;
                }
            }

            return target;
        }

        private static void ShowMarker(VisualElement container, VisualElement marker, int dragged, float pointerY)
        {
            int target = TargetIndex(container, marker, dragged, pointerY);
            int seen = 0;
            float? worldY = null;
            Rect last = default;
            int count = container.childCount;
            for (int i = 0; i < count; i++)
            {
                VisualElement child = container[i];
                if (i == dragged || child == marker)
                {
                    continue;
                }

                if (seen == target)
                {
                    worldY = child.worldBound.yMin - 3f;
                    break;
                }

                last = child.worldBound;
                seen++;
            }

            if (worldY == null && seen > 0)
            {
                worldY = last.yMax + 3f;
            }

            if (worldY == null)
            {
                return;
            }

            marker.style.top = container.WorldToLocal(new Vector2(0f, worldY.Value)).y - 1f;
            marker.style.display = DisplayStyle.Flex;
        }
    }
}
