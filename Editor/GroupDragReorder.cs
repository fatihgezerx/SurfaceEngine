using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// Lets layout-drawn group boxes be reordered by dragging a handle in their header, the way a
    /// <see cref="ReorderableList"/> reorders its elements. Usage per GUI pass: <see cref="Begin"/>,
    /// then for every group <see cref="DrawHandle"/> (inside its header row) and
    /// <see cref="RecordGroupRect"/> (right after its box closes), then <see cref="End"/>, which reports
    /// a finished drag.
    /// </summary>
    internal sealed class GroupDragReorder
    {
        private const float HandleWidth = 16f;

        private static readonly int DragHash = "GroupDragReorder".GetHashCode();
        private static readonly Color MarkerColor = new(0.24f, 0.49f, 0.9f);

        private readonly List<Rect> _groupRects = new();
        private int _controlId;
        private int _draggedIndex = -1;
        private float _mouseY;

        /// <summary>Call once before drawing the groups.</summary>
        public void Begin()
        {
            _controlId = GUIUtility.GetControlID(DragHash, FocusType.Passive);

            // Rects are measured on Repaint and reused by the mouse events that follow it.
            if (Event.current.type == EventType.Repaint)
            {
                _groupRects.Clear();
            }
        }

        /// <summary>Reserves and draws the drag handle for group <paramref name="index"/> in the current horizontal row.</summary>
        public void DrawHandle(int index, float height)
        {
            var rect = GUILayoutUtility.GetRect(HandleWidth, height, GUILayout.Width(HandleWidth), GUILayout.Height(height));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Pan);

            var evt = Event.current;
            if (evt.type == EventType.Repaint)
            {
                var handleRect = new Rect(rect.x + 2f, rect.center.y - 3f, rect.width - 4f, 6f);
                ReorderableList.defaultBehaviours.draggingHandle.Draw(handleRect, false, false, false, false);
            }
            else if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                _draggedIndex = index;
                _mouseY = evt.mousePosition.y;
                GUIUtility.hotControl = _controlId;
                evt.Use();
            }
        }

        /// <summary>Call right after a group's box has been closed, so its drawn rect can be recorded.</summary>
        public void RecordGroupRect()
        {
            if (Event.current.type == EventType.Repaint)
            {
                _groupRects.Add(GUILayoutUtility.GetLastRect());
            }
        }

        /// <summary>
        /// Call after all groups. Tracks the drag, draws where the group will land, and returns true
        /// once it is dropped somewhere new - with <paramref name="from"/> and <paramref name="to"/> as
        /// the source and final index.
        /// </summary>
        public bool End(out int from, out int to)
        {
            from = to = -1;

            if (_draggedIndex < 0)
            {
                return false;
            }

            if (GUIUtility.hotControl != _controlId)
            {
                _draggedIndex = -1;
                return false;
            }

            var evt = Event.current;
            switch (evt.GetTypeForControl(_controlId))
            {
                case EventType.MouseDrag:
                    _mouseY = evt.mousePosition.y;
                    evt.Use();
                    HandleUtility.Repaint();
                    break;

                case EventType.MouseUp:
                    GUIUtility.hotControl = 0;
                    evt.Use();
                    from = _draggedIndex;
                    to = GetTargetIndex();
                    _draggedIndex = -1;
                    return from != to;

                case EventType.Repaint:
                    DrawMarker();
                    break;
            }

            return false;
        }

        // The index the dragged group ends up at: how many other groups lie above the mouse.
        private int GetTargetIndex()
        {
            var target = 0;
            for (var i = 0; i < _groupRects.Count; i++)
            {
                if (i != _draggedIndex && _mouseY > _groupRects[i].center.y)
                {
                    target++;
                }
            }

            return target;
        }

        private void DrawMarker()
        {
            if (_draggedIndex >= _groupRects.Count)
            {
                return;
            }

            var dragged = _groupRects[_draggedIndex];
            EditorGUI.DrawRect(dragged, new Color(MarkerColor.r, MarkerColor.g, MarkerColor.b, 0.15f));

            // The marker sits just above the target-th of the other groups, or below the last one.
            var target = GetTargetIndex();
            var seen = 0;
            float? markerY = null;
            Rect last = default;

            for (var i = 0; i < _groupRects.Count; i++)
            {
                if (i == _draggedIndex)
                {
                    continue;
                }

                if (seen == target)
                {
                    markerY = _groupRects[i].yMin - 3f;
                    break;
                }

                last = _groupRects[i];
                seen++;
            }

            if (markerY == null && seen > 0)
            {
                markerY = last.yMax + 3f;
            }

            if (markerY != null)
            {
                EditorGUI.DrawRect(new Rect(dragged.x, markerY.Value - 1f, dragged.width, 2f), MarkerColor);
            }
        }
    }
}
