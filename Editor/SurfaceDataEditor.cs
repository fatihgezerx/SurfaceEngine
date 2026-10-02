using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// Custom Inspector for <see cref="SurfaceData"/>: general raycast settings on top, then a SURFACES box of
    /// named groups (reordered by dragging their handle). Each group is three boxes: TEXTURES (a horizontal,
    /// wrapping 50x50 card grid - not Unity's usual vertical list, so every variant of a surface is visible at
    /// a glance), FOOTSTEPS (one card per step kind) and IMPACTS (one card per weapon/damage type). Every card
    /// shares one compact layout: Sounds and VFX as two single-line lists side by side, then the decal field.
    /// "Compile" hands off to <see cref="SurfaceCompiler"/>.
    /// </summary>
    [CustomEditor(typeof(SurfaceData))]
    internal sealed class SurfaceDataEditor : Editor
    {
        private const float HeaderHeight = 28f;
        private const float GroupHeaderHeight = 22f;
        private const float TextureSize = 50f;
        private const float TextureGap = 8f;
        private const float TextureCardWidth = TextureSize + TextureGap;
        private const float TextureRowHeight = TextureSize + TextureGap;
        private const float FadeHeight = 14f;
        private const float DeleteBadgeSize = 14f;

        private const string GroupsInfo =
            "Every group is a surface (e.g. \"Grass\", \"Wood\", \"Snow\"). Its name becomes its SurfaceGroups " +
            "member on Compile, so names must be unique. Its Textures must be the exact same Texture2D asset " +
            "used by the surface's material (or Terrain layer) - SurfaceEngine matches a hit against them to " +
            "identify the surface, so they aren't just previews. Press Compile after any change - and after " +
            "renaming or reordering groups.";

        private static readonly Color CompileButtonColor = new(0.4f, 0.75f, 0.4f);

        private SerializedProperty _settings;
        private SerializedProperty _groups;
        private readonly GroupDragReorder _groupReorder = new();
        private readonly Dictionary<string, Vector2> _scrollPositions = new();
        private readonly Dictionary<string, ReorderableList> _lists = new();
        private readonly HashSet<string> _collapsed = new();
        private readonly HashSet<int> _collapsedGroups = new();
        private int? _pendingGroupToggle;
        private int _currentGroupKey;
        private string _headerFoldKey;
        private float _cachedViewWidth = 400f;

        // Lists address their array by path, so they're dropped whenever a group or impact is added, removed or moved.
        private bool _structureChanged;

        private GUIStyle _headerStyle;
        private GUIStyle _groupHeaderStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _deleteBadgeStyle;
        private GUIStyle _dropHintStyle;

        private GUIStyle HeaderStyle => _headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 20,
            fixedHeight = HeaderHeight
        };

        private GUIStyle GroupHeaderStyle => _groupHeaderStyle ??= new GUIStyle(EditorStyles.textField)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            fixedHeight = GroupHeaderHeight,
            alignment = TextAnchor.MiddleLeft
        };

        private GUIStyle HintStyle => _hintStyle ??= new GUIStyle(EditorStyles.label)
        {
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(6, 0, 0, 0),
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
        };

        private GUIStyle DeleteBadgeStyle => _deleteBadgeStyle ??= new GUIStyle(EditorStyles.miniButton)
        {
            fontSize = 9,
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0)
        };

        private GUIStyle DropHintStyle => _dropHintStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
        };

        private static Color PreviewBackgroundColor =>
            EditorGUIUtility.isProSkin ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.5f, 0.5f, 0.5f);

        private void OnEnable()
        {
            _settings = serializedObject.FindProperty("settings");
            _groups = serializedObject.FindProperty("groups");
            LoadFolds();
        }

        // Open/closed state lives in EditorPrefs per asset, so the Inspector comes back exactly as it was left.
        private string FoldsKey(string what) =>
            $"SurfaceSystem.{what}.{AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target))}";

        private void LoadFolds()
        {
            _collapsedGroups.Clear();
            foreach (var part in EditorPrefs.GetString(FoldsKey("Groups"), string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part, out var id))
                {
                    _collapsedGroups.Add(id);
                }
            }

            _collapsed.Clear();
            foreach (var path in EditorPrefs.GetString(FoldsKey("Lists"), string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                _collapsed.Add(path);
            }
        }

        // Only groups with a real id are remembered; a not-yet-compiled new group's key is just its position
        // (negative), so neither it nor its lists are saved.
        private void SaveFolds()
        {
            var ids = new List<string>();
            foreach (var id in _collapsedGroups)
            {
                if (id > 0)
                {
                    ids.Add(id.ToString());
                }
            }

            var lists = new List<string>();
            foreach (var key in _collapsed)
            {
                if (!key.StartsWith("-"))
                {
                    lists.Add(key);
                }
            }

            EditorPrefs.SetString(FoldsKey("Groups"), string.Join(",", ids));
            EditorPrefs.SetString(FoldsKey("Lists"), string.Join("|", lists));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawSettings();
            EditorGUILayout.Space(10);
            DrawGroups();

            serializedObject.ApplyModifiedProperties();

            if (_structureChanged)
            {
                _lists.Clear();
                _structureChanged = false;
            }

            EditorGUILayout.Space(14);

            var buttonColor = GUI.backgroundColor;
            GUI.backgroundColor = CompileButtonColor;
            if (GUILayout.Button("Compile", GUILayout.Height(34)))
            {
                SurfaceCompiler.Compile((SurfaceData)target);
            }
            GUI.backgroundColor = buttonColor;
        }

        private void DrawSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("GENERAL SETTINGS", HeaderStyle, GUILayout.Height(HeaderHeight));
            EditorGUILayout.PropertyField(_settings.FindPropertyRelative("groundLayers"));

            var audioPlaybackMode = _settings.FindPropertyRelative("audioPlaybackMode");
            EditorGUILayout.PropertyField(audioPlaybackMode);
            if ((AudioPlaybackMode)audioPlaybackMode.enumValueIndex == AudioPlaybackMode.Pool)
            {
                EditorGUILayout.PropertyField(_settings.FindPropertyRelative("poolAudioTypeName"), new GUIContent("Pool Type Name"));
            }

            EditorGUILayout.PropertyField(_settings.FindPropertyRelative("defaultRayDistance"));
            EditorGUILayout.PropertyField(_settings.FindPropertyRelative("effectLifetime"));
            EditorGUILayout.EndVertical();
        }

        private void DrawGroups()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("SURFACES", HeaderStyle, GUILayout.Height(HeaderHeight));
            EditorGUILayout.HelpBox(GroupsInfo, MessageType.Info);
            EditorGUILayout.Space(6);

            var groupPendingRemoval = -1;
            _groupReorder.Begin();
            for (var i = 0; i < _groups.arraySize; i++)
            {
                if (DrawGroup(i))
                {
                    groupPendingRemoval = i;
                }

                _groupReorder.RecordGroupRect();
                EditorGUILayout.Space(8);
            }

            if (_pendingGroupToggle is { } toggled)
            {
                if (!_collapsedGroups.Remove(toggled))
                {
                    _collapsedGroups.Add(toggled);
                }

                _pendingGroupToggle = null;
                SaveFolds();
                Repaint();
            }

            if (_groupReorder.End(out var from, out var to))
            {
                _groups.MoveArrayElement(from, to);
                _structureChanged = true;
            }
            else if (groupPendingRemoval >= 0)
            {
                _groups.DeleteArrayElementAtIndex(groupPendingRemoval);
                _structureChanged = true;
            }

            if (GUILayout.Button("+ Add Surface", GUILayout.Height(28)))
            {
                // A new array element copies the last one, so start from a blank surface instead.
                _groups.arraySize++;
                var group = _groups.GetArrayElementAtIndex(_groups.arraySize - 1);
                group.FindPropertyRelative("id").intValue = 0;
                group.FindPropertyRelative("header").stringValue = "New Surface";
                group.FindPropertyRelative("textures").arraySize = 0;
                ResetToGenericCard(group.FindPropertyRelative("footsteps"));
                ResetToGenericCard(group.FindPropertyRelative("impacts"));
                _structureChanged = true;
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>Draws one group; returns true if its remove button was clicked.</summary>
        private bool DrawGroup(int index)
        {
            var group = _groups.GetArrayElementAtIndex(index);
            var header = group.FindPropertyRelative("header");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            _groupReorder.DrawHandle(index, GroupHeaderHeight);
            var headerRect = EditorGUILayout.GetControlRect(GUILayout.Height(GroupHeaderHeight));
            header.stringValue = EditorGUI.TextField(headerRect, header.stringValue, GroupHeaderStyle);
            if (string.IsNullOrEmpty(header.stringValue))
            {
                EditorGUI.LabelField(headerRect, "Surface name", HintStyle);
            }

            // Keyed by the group's stable id (index for a brand-new group that has none yet), so folding survives reordering.
            var key = group.FindPropertyRelative("id").intValue is var id && id > 0 ? id : -(index + 1);
            var collapsed = _collapsedGroups.Contains(key);
            _currentGroupKey = key;

            if (GUILayout.Button(collapsed ? "▸" : "▾", GUILayout.Width(GroupHeaderHeight), GUILayout.Height(GroupHeaderHeight)))
            {
                // Applied after the whole pass: adding controls mid-pass would desync the layout.
                _pendingGroupToggle = key;
            }

            var remove = GUILayout.Button("✕", GUILayout.Width(GroupHeaderHeight), GUILayout.Height(GroupHeaderHeight));
            EditorGUILayout.EndHorizontal();

            if (collapsed)
            {
                EditorGUILayout.EndVertical();
                return remove;
            }

            EditorGUILayout.Space(6);
            DrawSectionTitled("TEXTURES", "must be the same Texture2D as the material", () => DrawTextureGrid(group));

            EditorGUILayout.Space(4);
            DrawTypedEntries(group.FindPropertyRelative("footsteps"), typeof(FootstepType), "FOOTSTEPS", "per step kind; missing ones use Generic");

            EditorGUILayout.Space(4);
            DrawTypedEntries(group.FindPropertyRelative("impacts"), typeof(ImpactType), "IMPACTS", "per weapon/damage type; missing ones use Generic");

            EditorGUILayout.EndVertical();
            return remove;
        }

        // A helpBox with a bold title and a grey hint after it, then whatever `content` draws.
        private void DrawSectionTitled(string title, string hint, Action content)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawSectionTitle(title, hint);
            content();
            EditorGUILayout.EndVertical();
        }

        private void DrawSectionTitle(string title, string hint)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel, GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField(hint, HintStyle);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        // A new group starts with a single Generic card (enum value 0), the fallback every other type uses.
        private static void ResetToGenericCard(SerializedProperty entries)
        {
            entries.arraySize = 1;
            var entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("type").enumValueIndex = 0;
            ClearEffects(entry);
        }

        // One card per type (Walk, Run... / Sword, Gun...). Types are unique per surface; the first unused one
        // is picked for a new card, and a repeated type is flagged since only the first is used. Works for any
        // list of entries with a "type" enum plus the sounds/vfx/decalPrefab fields.
        private void DrawTypedEntries(SerializedProperty entries, Type enumType, string title, string hint)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawSectionTitle(title, hint);

            var typeCount = Enum.GetNames(enumType).Length;
            var used = new HashSet<int>();
            var removeIndex = -1;

            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var type = entry.FindPropertyRelative("type");
                var duplicate = !used.Add(type.enumValueIndex);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(type, GUIContent.none);
                if (GUILayout.Button("✕", GUILayout.Width(22f)))
                {
                    removeIndex = i;
                }
                EditorGUILayout.EndHorizontal();

                if (duplicate)
                {
                    EditorGUILayout.HelpBox("Another card already uses this type; only the first one is played.", MessageType.Warning);
                }

                // The lists' folds are remembered by group id + kind + type, so they survive reordering and removals.
                DrawEffects(entry, $"{_currentGroupKey}/{entries.name}/{type.enumValueIndex}");
                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                entries.DeleteArrayElementAtIndex(removeIndex);
                _structureChanged = true;
            }

            using (new EditorGUI.DisabledScope(used.Count >= typeCount))
            {
                if (GUILayout.Button("+ Add"))
                {
                    var freeType = 0;
                    while (used.Contains(freeType))
                    {
                        freeType++;
                    }

                    // A new array element copies the last one, so start from a blank card instead.
                    entries.arraySize++;
                    var entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                    entry.FindPropertyRelative("type").enumValueIndex = freeType;
                    ClearEffects(entry);
                    _structureChanged = true;
                }
            }

            EditorGUILayout.EndVertical();
        }

        // Sounds and VFX as two compact single-line lists side by side, then the decal/prefab field under them.
        // Several assets can be dropped on a list at once from the Project window.
        private void DrawEffects(SerializedProperty settings, string foldKey)
        {
            var soundsArray = settings.FindPropertyRelative("sounds");
            var vfxArray = settings.FindPropertyRelative("vfx");
            var soundsKey = foldKey + "/sounds";
            var vfxKey = foldKey + "/vfx";
            var sounds = GetList(soundsArray, "Sounds");
            var vfx = GetList(vfxArray, "VFX");

            var soundsHeight = ListHeight(sounds, soundsKey);
            var vfxHeight = ListHeight(vfx, vfxKey);

            const float gap = 6f;
            var rect = EditorGUILayout.GetControlRect(false, Mathf.Max(soundsHeight, vfxHeight));
            var half = (rect.width - gap) * 0.5f;
            var soundsRect = new Rect(rect.x, rect.y, half, soundsHeight);
            var vfxRect = new Rect(rect.x + half + gap, rect.y, half, vfxHeight);

            DoList(sounds, soundsArray, "Sounds", soundsKey, soundsRect);
            DoList(vfx, vfxArray, "VFX", vfxKey, vfxRect);

            HandleDrop(soundsRect, soundsArray, dragged => dragged as AudioClip);
            HandleDrop(vfxRect, vfxArray, dragged =>
                dragged is ParticleSystem system ? system :
                dragged is GameObject prefab && prefab.TryGetComponent<ParticleSystem>(out var found) ? found : null);

            EditorGUILayout.PropertyField(settings.FindPropertyRelative("decalPrefab"), new GUIContent("Decal"));
        }

        // Adds every dragged asset `accept` turns into a non-null object (e.g. all the AudioClips of a multi-selection).
        private void HandleDrop(Rect rect, SerializedProperty array, Func<UnityEngine.Object, UnityEngine.Object> accept)
        {
            var evt = Event.current;
            if ((evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) || !rect.Contains(evt.mousePosition))
            {
                return;
            }

            var accepted = new List<UnityEngine.Object>();
            foreach (var dragged in DragAndDrop.objectReferences)
            {
                var converted = accept(dragged);
                if (converted != null)
                {
                    accepted.Add(converted);
                }
            }

            if (accepted.Count == 0)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var item in accepted)
                {
                    array.arraySize++;
                    array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = item;
                }
            }

            evt.Use();
        }

        // A list's fold is keyed by "<group id>/<footsteps|impacts>/<type>/<sounds|vfx>" (see DrawTypedEntries), so it
        // doesn't depend on where the card or group currently sits.
        private float ListHeight(ReorderableList list, string foldKey) =>
            _collapsed.Contains(foldKey) ? list.headerHeight : list.GetHeight();

        // Expanded: the whole list, its header carrying the fold button. Collapsed: just that header, with the item count.
        private void DoList(ReorderableList list, SerializedProperty array, string title, string foldKey, Rect rect)
        {
            if (_collapsed.Contains(foldKey))
            {
                DrawListHeader(rect, title, foldKey, array.arraySize);
            }
            else
            {
                // The list's own header callback runs inside DoList and reads these.
                _headerFoldKey = foldKey;
                list.DoList(rect);
            }
        }

        private void DrawListHeader(Rect rect, string title, string foldKey, int count)
        {
            const float buttonWidth = 22f;
            var collapsed = _collapsed.Contains(foldKey);

            if (Event.current.type == EventType.Repaint && collapsed)
            {
                ReorderableList.defaultBehaviours.DrawHeaderBackground(rect);
            }

            var label = collapsed ? $"{title} ({count})" : title;
            EditorGUI.LabelField(new Rect(rect.x + 6f, rect.y, rect.width - buttonWidth - 6f, rect.height), label);

            var buttonRect = new Rect(rect.xMax - buttonWidth - 2f, rect.y + 1f, buttonWidth, rect.height - 2f);
            if (GUI.Button(buttonRect, collapsed ? "▸" : "▾", EditorStyles.miniButton))
            {
                if (collapsed)
                {
                    _collapsed.Remove(foldKey);
                }
                else
                {
                    _collapsed.Add(foldKey);
                }

                SaveFolds();
            }
        }

        private ReorderableList GetList(SerializedProperty array, string title)
        {
            if (_lists.TryGetValue(array.propertyPath, out var cached))
            {
                return cached;
            }

            var list = new ReorderableList(serializedObject, array, false, true, true, true)
            {
                elementHeight = EditorGUIUtility.singleLineHeight + 4f,
                drawHeaderCallback = rect => DrawListHeader(rect, title, _headerFoldKey, array.arraySize),
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    rect.y += 2f;
                    rect.height = EditorGUIUtility.singleLineHeight;
                    EditorGUI.PropertyField(rect, array.GetArrayElementAtIndex(index), GUIContent.none);
                },
                drawNoneElementCallback = rect => EditorGUI.LabelField(rect, "None", HintStyle),
                onAddCallback = l =>
                {
                    array.arraySize++;
                    array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = null;
                    l.index = array.arraySize - 1;
                }
            };

            _lists[array.propertyPath] = list;
            return list;
        }

        private static void ClearEffects(SerializedProperty settings)
        {
            settings.FindPropertyRelative("sounds").arraySize = 0;
            settings.FindPropertyRelative("vfx").arraySize = 0;
            settings.FindPropertyRelative("decalPrefab").objectReferenceValue = null;
        }

        // A horizontal, wrapping grid of 50x50 texture cards (deliberately not Unity's usual vertical list),
        // with a "+" card at the end and a scroll + fade once a group holds more than a couple of rows.
        private void DrawTextureGrid(SerializedProperty group)
        {
            var textures = group.FindPropertyRelative("textures");

            // Locked to the Layout pass's value so every event type in this GUI cycle wraps identically -
            // see PoolDataEditor.DrawEntryGrid for why a value that can drift between passes is a problem.
            if (Event.current.type == EventType.Layout)
            {
                _cachedViewWidth = EditorGUIUtility.currentViewWidth;
            }

            // Inspector margins plus the SURFACES, group and TEXTURES boxes around the grid.
            var viewWidth = _cachedViewWidth - 76f;
            var perRow = Mathf.Max(1, Mathf.FloorToInt(viewWidth / TextureCardWidth));

            var cardCount = textures.arraySize + 1; // +1 for the "add" card
            var rowCount = Mathf.CeilToInt(cardCount / (float)perRow);
            var contentHeight = rowCount * TextureRowHeight;
            var visibleHeight = Mathf.Min(contentHeight, TextureRowHeight * 2.5f);

            var scrollKey = group.propertyPath;
            _scrollPositions.TryGetValue(scrollKey, out var scroll);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(visibleHeight));

            var removeIndex = -1;
            var column = 0;

            EditorGUILayout.BeginHorizontal();
            for (var i = 0; i < textures.arraySize; i++)
            {
                if (column >= perRow)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    column = 0;
                }

                var capturedIndex = i;
                DrawTextureCard(textures.GetArrayElementAtIndex(i), () => removeIndex = capturedIndex);
                column++;
            }

            if (column >= perRow)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }

            if (GUILayout.Button("+", GUILayout.Width(TextureSize), GUILayout.Height(TextureSize)))
            {
                textures.arraySize++;
                textures.GetArrayElementAtIndex(textures.arraySize - 1).objectReferenceValue = null;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
            var scrollRect = GUILayoutUtility.GetLastRect();

            if (contentHeight > visibleHeight)
            {
                DrawBottomFade(scrollRect);
            }

            HandleGridDrop(scrollRect, textures);

            if (removeIndex >= 0)
            {
                textures.DeleteArrayElementAtIndex(removeIndex);
            }

            _scrollPositions[scrollKey] = scroll;
        }

        // Dropping several Texture2D assets at once anywhere in the grid that isn't an existing card (the gaps,
        // the "+" card, empty space) appends one new card per texture - so a whole multi-selection from the
        // Project window can be dragged in together instead of one at a time. A drop directly on an existing
        // card is already claimed by that card's own HandleDragAndDrop (which calls evt.Use()), so this only
        // fires when the drop lands outside every card.
        private static void HandleGridDrop(Rect rect, SerializedProperty textures)
        {
            var evt = Event.current;
            if ((evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) || !rect.Contains(evt.mousePosition))
            {
                return;
            }

            var accepted = new List<Texture2D>();
            foreach (var dragged in DragAndDrop.objectReferences)
            {
                if (dragged is Texture2D texture)
                {
                    accepted.Add(texture);
                }
            }

            if (accepted.Count == 0)
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var texture in accepted)
                {
                    textures.arraySize++;
                    textures.GetArrayElementAtIndex(textures.arraySize - 1).objectReferenceValue = texture;
                }
            }

            evt.Use();
        }

        private void DrawTextureCard(SerializedProperty element, Action requestRemove)
        {
            var cardRect = GUILayoutUtility.GetRect(TextureSize, TextureSize, GUILayout.Width(TextureSize), GUILayout.Height(TextureSize));

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(cardRect, PreviewBackgroundColor);
                var texture = element.objectReferenceValue as Texture2D;
                if (texture != null)
                {
                    // alphaBlend: false - otherwise an unused/near-zero alpha channel (common on diffuse maps that
                    // never touch alpha) blends toward the dark card background and the preview reads as black,
                    // even though the surface's shader ignores alpha and renders the texture correctly in-scene.
                    GUI.DrawTexture(cardRect, texture, ScaleMode.ScaleToFit, false);
                    // The 50x50 preview is too small to tell textures apart (and a Normal Map renders as a flat
                    // block here) - a hover tooltip carries the name and import type instead of growing the card.
                    GUI.Label(cardRect, new GUIContent(string.Empty, GetTextureTooltip(texture)), GUIStyle.none);
                }
                else
                {
                    EditorGUI.LabelField(cardRect, "Drop\nTexture", DropHintStyle);
                }
            }

            // Claimed before the object-picker click below, so a click on the badge deletes rather than opens the picker.
            var deleteRect = new Rect(cardRect.xMax - DeleteBadgeSize - 1f, cardRect.y + 1f, DeleteBadgeSize, DeleteBadgeSize);
            var deleteClicked = GUI.Button(deleteRect, GUIContent.none, GUIStyle.none);

            HandleDragAndDrop(cardRect, element);

            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                cardRect.Contains(Event.current.mousePosition) && !deleteRect.Contains(Event.current.mousePosition))
            {
                EditorGUIUtility.ShowObjectPicker<Texture2D>(element.objectReferenceValue, false, string.Empty, controlId);
                Event.current.Use();
            }

            if (Event.current.commandName == "ObjectSelectorUpdated" && EditorGUIUtility.GetObjectPickerControlID() == controlId)
            {
                element.objectReferenceValue = EditorGUIUtility.GetObjectPickerObject();
            }

            if (Event.current.type == EventType.Repaint)
            {
                GUI.Box(deleteRect, "✕", DeleteBadgeStyle);
            }

            if (deleteClicked)
            {
                requestRemove();
            }
        }

        // "<name> (<import type>)", e.g. "Dirt_1_Nor (NormalMap)" - lets the user confirm they dropped the
        // diffuse variant and not the normal map (which is what renders as a flat/black square at preview size).
        private static string GetTextureTooltip(Texture2D texture)
        {
            var path = AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(path) && AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                return $"{texture.name} ({importer.textureType})";
            }

            return texture.name;
        }

        private static void HandleDragAndDrop(Rect rect, SerializedProperty element)
        {
            var evt = Event.current;
            if (!rect.Contains(evt.mousePosition))
            {
                return;
            }

            if (evt.type == EventType.DragUpdated)
            {
                var dragged = DragAndDrop.objectReferences.Length > 0 ? DragAndDrop.objectReferences[0] as Texture2D : null;
                DragAndDrop.visualMode = dragged != null ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                evt.Use();
            }
            else if (evt.type == EventType.DragPerform)
            {
                var dragged = DragAndDrop.objectReferences.Length > 0 ? DragAndDrop.objectReferences[0] as Texture2D : null;
                if (dragged != null)
                {
                    element.objectReferenceValue = dragged;
                }

                DragAndDrop.AcceptDrag();
                evt.Use();
            }
        }

        private static void DrawBottomFade(Rect scrollRect)
        {
            const int steps = 8;
            var baseColor = EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.78f, 0.78f, 0.78f);
            var stepHeight = FadeHeight / steps;

            for (var i = 0; i < steps; i++)
            {
                var t = (i + 1) / (float)steps;
                var stripRect = new Rect(scrollRect.x, scrollRect.yMax - FadeHeight + i * stepHeight, scrollRect.width, stepHeight);
                var color = baseColor;
                color.a = t * 0.85f;
                EditorGUI.DrawRect(stripRect, color);
            }
        }
    }
}
