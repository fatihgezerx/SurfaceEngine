using System;
using System.Collections.Generic;
using Veneer;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SurfaceSystem
{
    /// <summary>
    /// The list of surfaces of a <see cref="SurfaceData"/> Inspector, the widget of the <c>groups</c> property. One card per
    /// <see cref="SurfaceGroup"/> (drag handle, editable name, texture count, fold, remove) holding a wrapping grid of texture cards and
    /// the Footsteps and Impacts entries (one row per kind, opened to show its Sounds, VFX and decal material), then the Add surface button.
    /// It is one imperative widget: the cards are rebuilt when surfaces, cards or rows are added, removed or reordered. Each Inspector
    /// builds its own view (fold state is kept in EditorPrefs per asset).
    /// </summary>
    internal sealed class SurfaceGroupsView
    {
        private const string GroupsPath = "groups";
        private const string TitleRowClass = "vn-card__title";

        private readonly SurfaceDataSession session;
        private readonly SerializedObject serializedObject;
        private readonly VeneerCustomItemContext context;
        private readonly HashSet<int> collapsedGroups = new HashSet<int>();
        private readonly HashSet<string> openEntries = new HashSet<string>();
        private readonly List<GroupView> groupViews = new List<GroupView>();
        private readonly List<EntryView> entryViews = new List<EntryView>();
        private SurfaceDataAnalysis analysis;
        private VisualElement groupsHost;
        private VisualElement groupMarker;
        private int builtStructure;
        private int builtCompileCount;
        private int appliedVersion = -1;

        private struct GroupView
        {
            public int Index;
            public VisualElement Card;
            public Label TexturePill;
        }

        private struct EntryView
        {
            public int Group;
            public SurfaceEntryKind Kind;
            public int Entry;
            public VisualElement Card;
            public VisualElement Hint;
        }

        /// <summary>Makes the view for the Inspector the <paramref name="context"/> belongs to.</summary>
        public SurfaceGroupsView(VeneerCustomItemContext context)
        {
            this.context = context;
            serializedObject = context.SerializedObject;
            session = SurfaceDataSession.For(serializedObject);
        }

        private SurfaceData Target => session.Data;

        /// <summary>Builds the surface cards and the Add surface button, and starts following the data. Returns the element to add.</summary>
        public VisualElement Build()
        {
            FlattenHostCard(context.Builder.Current);

            analysis = session.Analysis;
            analysis.Refresh(true);
            LoadFolds();
            groupViews.Clear();
            entryViews.Clear();

            var root = new VisualElement();
            groupMarker = SurfaceDragReorder.CreateMarker();
            groupsHost = new VisualElement();
            root.Add(groupsHost);
            builtCompileCount = session.CompileCount;
            RebuildGroups();
            groupsHost.TrackSerializedObjectValue(serializedObject, OnDataChanged);
            context.Poll(PollAnalysis);

            Button addGroup = VeneerStyle.Button(
                "Add surface", "plus", "Adds an empty surface at the end, with a single Generic footstep and impact entry.", AddGroup, false, SurfaceBrand.Value);
            addGroup.style.alignSelf = Align.FlexStart;
            root.Add(addGroup);
            return root;
        }

        // The default layout puts the explanation and this list in a card without a title so they read as loose tab content (the old
        // Inspector had no box around them). A card the user gave a title keeps its box.
        private static void FlattenHostCard(VisualElement host)
        {
            if (host == null || !host.ClassListContains("vn-card") || (host.childCount > 0 && host[0].ClassListContains(TitleRowClass)))
            {
                return;
            }

            host.style.backgroundColor = new StyleColor(Color.clear);
            host.style.borderTopWidth = 0f;
            host.style.borderRightWidth = 0f;
            host.style.borderBottomWidth = 0f;
            host.style.borderLeftWidth = 0f;
            host.style.paddingTop = 0f;
            host.style.paddingRight = 0f;
            host.style.paddingBottom = 0f;
            host.style.paddingLeft = 0f;
        }

        // ------------------------------------------------------------------ polling

        // Runs every 200 ms: follows the analysis (cards and pills) and rebuilds the list when Compile ran from this Inspector.
        private void PollAnalysis()
        {
            if (Target == null)
            {
                return;
            }

            analysis.Refresh();
            if (session.CompileCount != builtCompileCount)
            {
                builtCompileCount = session.CompileCount;
                RebuildGroups();
                return;
            }

            if (analysis.Version != appliedVersion)
            {
                ApplyAnalysis();
            }
        }

        // Writes the analysis into the cards and pills; runs only when the data or the generated state changed.
        private void ApplyAnalysis()
        {
            appliedVersion = analysis.Version;
            List<SurfaceGroup> groups = Target.Groups;
            for (int i = 0; i < groupViews.Count; i++)
            {
                GroupView view = groupViews[i];
                int textures = 0;
                if (view.Index < groups.Count && groups[view.Index] != null)
                {
                    foreach (Texture2D texture in groups[view.Index].Textures)
                    {
                        if (texture != null)
                        {
                            textures++;
                        }
                    }
                }

                SurfaceGroupIssue issue = analysis.GetIssue(view.Index);
                view.Card.EnableInClassList("surf-group--error", issue != SurfaceGroupIssue.None);
                view.Card.tooltip = IssueText(issue);
                VeneerStyle.SetPill(view.TexturePill, textures == 0 ? "warn" : "info", textures == 1 ? "1 texture" : textures + " textures");
            }

            for (int i = 0; i < entryViews.Count; i++)
            {
                EntryView view = entryViews[i];
                bool duplicate = analysis.IsDuplicateEntry(view.Group, view.Kind, view.Entry);
                view.Card.EnableInClassList("surf-entry--warn", duplicate);
                VeneerStyle.SetDisplay(view.Hint, duplicate);
            }
        }

        private static string IssueText(SurfaceGroupIssue issue)
        {
            switch (issue)
            {
                case SurfaceGroupIssue.NoName:
                    return "This surface needs a name: it becomes a SurfaceGroups member.";
                case SurfaceGroupIssue.Reserved:
                    return "'None' is reserved for the unassigned value. Rename this surface.";
                case SurfaceGroupIssue.Duplicate:
                    return "Another surface has the same name. Every name must be unique.";
                default:
                    return string.Empty;
            }
        }

        // ------------------------------------------------------------------ structure

        // Everything that changes how many cards or rows exist, or what a summary pill says.
        private int StructureSignature()
        {
            List<SurfaceGroup> groups = Target.Groups;
            unchecked
            {
                int hash = groups.Count;
                for (int i = 0; i < groups.Count; i++)
                {
                    SurfaceGroup group = groups[i];
                    if (group == null)
                    {
                        hash = hash * 31 + 1;
                        continue;
                    }

                    hash = hash * 31 + group.Textures.Count;
                    hash = hash * 31 + group.Footsteps.Count;
                    foreach (FootstepEntry entry in group.Footsteps)
                    {
                        hash = hash * 31 + (entry != null ? entry.Sounds.Length * 7 + entry.Vfx.Length * 3 + (entry.DecalMaterial != null ? 1 : 0) : 0);
                    }

                    hash = hash * 31 + group.Impacts.Count;
                    foreach (ImpactEntry entry in group.Impacts)
                    {
                        hash = hash * 31 + (entry != null ? entry.Sounds.Length * 7 + entry.Vfx.Length * 3 + (entry.DecalMaterial != null ? 1 : 0) : 0);
                    }
                }

                return hash;
            }
        }

        // Undo, Redo and other edits can add or remove surfaces, cards and rows: rebuild when the shape changed.
        private void OnDataChanged(SerializedObject changed)
        {
            if (Target != null && StructureSignature() != builtStructure)
            {
                RebuildGroups();
            }
        }

        private void RebuildGroups()
        {
            groupsHost.Clear();
            groupViews.Clear();
            entryViews.Clear();
            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty groups = serializedObject.FindProperty(GroupsPath);
            if (groups.arraySize == 0)
            {
                groupsHost.Add(VeneerStyle.EmptyState(
                    "layers", "No surfaces yet", "Add a surface, drop its textures into it, name it and press Compile.", SurfaceBrand.Value));
            }

            for (int i = 0; i < groups.arraySize; i++)
            {
                groupsHost.Add(BuildGroup(groups.GetArrayElementAtIndex(i), i));
            }

            groupsHost.Add(groupMarker);
            builtStructure = StructureSignature();
            appliedVersion = -1;
            ApplyAnalysis();
        }

        private static int GroupKey(SerializedProperty group, int index)
        {
            // Keyed by the group's stable id (position, negative, for a not-yet-compiled group that has none), so folds survive reordering.
            int id = group.FindPropertyRelative("id").intValue;
            return id > 0 ? id : -(index + 1);
        }

        private static string ListName(SurfaceEntryKind kind) => kind == SurfaceEntryKind.Footstep ? "footsteps" : "impacts";

        private static string MakeEntryKey(int groupKey, SurfaceEntryKind kind, int typeIndex) => groupKey + "/" + ListName(kind) + "/" + typeIndex;

        private VisualElement BuildGroup(SerializedProperty group, int index)
        {
            int key = GroupKey(group, index);
            bool collapsed = collapsedGroups.Contains(key);

            VisualElement card = VeneerStyle.Card(null, null, SurfaceBrand.Value);
            card.AddToClassList("surf-group");

            var head = new VisualElement();
            head.AddToClassList("surf-group__head");
            head.EnableInClassList("surf-group__head--open", !collapsed);
            VisualElement handle = MakeHandle("Drag to reorder this surface.");
            head.Add(handle);

            var nameField = new TextField();
            nameField.AddToClassList("surf-group__name");
            nameField.textEdition.placeholder = "Surface name";
            nameField.tooltip = "Becomes the SurfaceGroups member of this surface on Compile. Must be unique.";
            nameField.BindProperty(group.FindPropertyRelative("header"));
            head.Add(nameField);

            Label texturePill = VeneerStyle.Pill("0 textures", "info");
            head.Add(texturePill);
            groupViews.Add(new GroupView { Index = index, Card = card, TexturePill = texturePill });

            Button fold = VeneerStyle.IconButton(collapsed ? "chevron-right" : "chevron-down", "Show or hide the details of this surface.", null, SurfaceBrand.Value);
            head.Add(fold);
            head.Add(VeneerStyle.IconButton("close", "Remove this surface and everything in it.", () => RemoveGroup(index), SurfaceBrand.Value));
            card.Add(head);

            var body = new VisualElement();
            body.AddToClassList("surf-group__body");
            bool built = false;
            void EnsureBody()
            {
                if (built)
                {
                    return;
                }

                built = true;
                SerializedProperty current = Groups().GetArrayElementAtIndex(index);
                body.Add(BuildTextures(current, index));
                body.Add(BuildEntries(current, index, key, SurfaceEntryKind.Footstep));
                body.Add(BuildEntries(current, index, key, SurfaceEntryKind.Impact));
            }

            if (!collapsed)
            {
                EnsureBody();
            }

            VeneerStyle.SetDisplay(body, !collapsed);
            card.Add(body);

            fold.clicked += () =>
            {
                bool nowCollapsed = collapsedGroups.Add(key);
                if (!nowCollapsed)
                {
                    collapsedGroups.Remove(key);
                    EnsureBody();
                }

                SaveFolds();
                VeneerStyle.SetDisplay(body, !nowCollapsed);
                head.EnableInClassList("surf-group__head--open", !nowCollapsed);
                VeneerStyle.SetIcon(fold[0], nowCollapsed ? "chevron-right" : "chevron-down", SurfaceBrand.Value);
            };

            SurfaceDragReorder.Attach(handle, card, groupsHost, groupMarker, MoveGroup);
            return card;
        }

        private static VisualElement MakeHandle(string tooltip)
        {
            var handle = new VisualElement();
            handle.AddToClassList("surf-handle");
            handle.tooltip = tooltip;
            handle.Add(VeneerStyle.IconElement("drag-handle", null, SurfaceBrand.Value));
            return handle;
        }

        // A nested card with a title row, plus a small grey hint after the title.
        private static VisualElement MakeSection(string title, string icon, string hint)
        {
            VisualElement section = VeneerStyle.Card(title, icon, SurfaceBrand.Value);
            section.AddToClassList("vn-card--nested");
            section.AddToClassList("surf-section");
            if (!string.IsNullOrEmpty(hint) && section.childCount > 0)
            {
                var hintLabel = new Label(hint);
                hintLabel.AddToClassList("surf-section__hint");
                section[0].Add(hintLabel);
            }

            return section;
        }

        // ------------------------------------------------------------------ textures

        private VisualElement BuildTextures(SerializedProperty group, int groupIndex)
        {
            VisualElement section = MakeSection("Textures", "image", "must be the same Texture2D as the material");
            SerializedProperty textures = group.FindPropertyRelative("textures");

            var grid = new VisualElement();
            grid.AddToClassList("surf-grid");
            for (int i = 0; i < textures.arraySize; i++)
            {
                grid.Add(BuildTextureCard(textures.GetArrayElementAtIndex(i), groupIndex, i));
            }

            Button add = VeneerStyle.IconButton("plus", "Add an empty texture slot. You can also drop Texture2D assets (several at once) anywhere on this grid.", () => AddTexture(groupIndex, null), SurfaceBrand.Value);
            add.AddToClassList("surf-tex__add");
            grid.Add(add);

            // Dropping several Texture2D assets anywhere that is not an existing card appends one new card per texture; a single asset
            // dropped on a card replaces that card's texture (the card's own object field handles it).
            grid.RegisterCallback<DragUpdatedEvent>(evt => OnTextureDrag(evt, grid, groupIndex, false), TrickleDown.TrickleDown);
            grid.RegisterCallback<DragPerformEvent>(evt => OnTextureDrag(evt, grid, groupIndex, true), TrickleDown.TrickleDown);
            section.Add(grid);
            return section;
        }

        private void OnTextureDrag(EventBase evt, VisualElement grid, int groupIndex, bool perform)
        {
            List<Texture2D> dragged = DraggedTextures();
            if (dragged.Count == 0)
            {
                return;
            }

            if (dragged.Count == 1 && IsOverCard(evt.target as VisualElement, grid))
            {
                return;
            }

            if (!perform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            }
            else
            {
                DragAndDrop.AcceptDrag();
                SerializedProperty textures = Groups().GetArrayElementAtIndex(groupIndex).FindPropertyRelative("textures");
                foreach (Texture2D texture in dragged)
                {
                    textures.arraySize++;
                    textures.GetArrayElementAtIndex(textures.arraySize - 1).objectReferenceValue = texture;
                }

                Commit("Add Surface Textures");
                RebuildGroups();
            }

            evt.StopPropagation();
        }

        private static bool IsOverCard(VisualElement element, VisualElement grid)
        {
            while (element != null && element != grid)
            {
                if (element.ClassListContains("surf-tex"))
                {
                    return true;
                }

                element = element.parent;
            }

            return false;
        }

        private static List<Texture2D> DraggedTextures()
        {
            var result = new List<Texture2D>();
            foreach (Object dragged in DragAndDrop.objectReferences)
            {
                if (dragged is Texture2D texture)
                {
                    result.Add(texture);
                }
            }

            return result;
        }

        private VisualElement BuildTextureCard(SerializedProperty texture, int groupIndex, int index)
        {
            var card = new VisualElement();
            card.AddToClassList("surf-tex");

            var thumbHost = new VisualElement();
            thumbHost.AddToClassList("surf-tex__thumb");
            thumbHost.pickingMode = PickingMode.Ignore;
            var emptyLabel = new Label("Drop\ntexture");
            emptyLabel.AddToClassList("surf-tex__empty");
            emptyLabel.pickingMode = PickingMode.Ignore;
            thumbHost.Add(emptyLabel);
            card.Add(thumbHost);

            SerializedProperty watched = texture.Copy();
            var thumbnail = new TextureThumbnail(thumbHost, emptyLabel, watched);
            var field = new ObjectField { objectType = typeof(Texture2D), allowSceneObjects = false, label = string.Empty };
            field.AddToClassList("surf-tex__field");
            field.BindProperty(texture);
            field.RegisterValueChangedCallback(evt =>
            {
                thumbnail.Refresh();
                card.tooltip = TextureTooltip(evt.newValue as Texture2D);
            });
            card.Add(field);
            thumbnail.Refresh();
            card.tooltip = TextureTooltip(watched.objectReferenceValue as Texture2D);

            Button remove = VeneerStyle.IconButton("close", "Remove this texture.", () => RemoveObjectAt(groupIndex, "textures", index, "Remove Surface Texture"), SurfaceBrand.Value);
            remove.AddToClassList("surf-tex__remove");
            card.Add(remove);
            return card;
        }

        // "<name> (<import type>)", e.g. "Dirt_1_Nor (NormalMap)": confirms the diffuse variant was dropped and not the normal map.
        private static string TextureTooltip(Texture2D texture)
        {
            if (texture == null)
            {
                return "Click the small button to pick a texture, or drop one here.";
            }

            string path = AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(path) && AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                return texture.name + " (" + importer.textureType + ")";
            }

            return texture.name;
        }

        private void AddTexture(int groupIndex, Texture2D texture)
        {
            SerializedProperty textures = Groups().GetArrayElementAtIndex(groupIndex).FindPropertyRelative("textures");
            textures.arraySize++;
            textures.GetArrayElementAtIndex(textures.arraySize - 1).objectReferenceValue = texture;
            Commit("Add Surface Texture");
            RebuildGroups();
        }

        // ------------------------------------------------------------------ footstep / impact entries

        private VisualElement BuildEntries(SerializedProperty group, int groupIndex, int groupKey, SurfaceEntryKind kind)
        {
            bool footstep = kind == SurfaceEntryKind.Footstep;
            Type enumType = footstep ? typeof(FootstepType) : typeof(ImpactType);
            VisualElement section = MakeSection(
                footstep ? "Footsteps" : "Impacts",
                footstep ? "step" : "sword",
                footstep ? "per step kind; missing ones use Generic" : "per weapon / damage type; missing ones use Generic");

            SerializedProperty entries = group.FindPropertyRelative(ListName(kind));
            var used = new HashSet<int>();
            for (int e = 0; e < entries.arraySize; e++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(e);
                int typeIndex = entry.FindPropertyRelative("type").enumValueIndex;
                used.Add(typeIndex);
                section.Add(BuildEntry(entry, groupIndex, groupKey, kind, enumType, e));
            }

            if (entries.arraySize == 0)
            {
                Label none = VeneerStyle.MutedLabel("No entries: this surface plays nothing for " + (footstep ? "steps." : "impacts."));
                section.Add(none);
            }

            Button add = VeneerStyle.Button(
                footstep ? "Add footstep" : "Add impact", "plus",
                "Adds a card for the first kind this surface does not have yet.", () => AddEntry(groupIndex, kind), false, SurfaceBrand.Value);
            add.AddToClassList("surf-group__add");
            add.SetEnabled(used.Count < Enum.GetNames(enumType).Length);
            section.Add(add);
            return section;
        }

        private VisualElement BuildEntry(SerializedProperty entry, int groupIndex, int groupKey, SurfaceEntryKind kind, Type enumType, int index)
        {
            SerializedProperty type = entry.FindPropertyRelative("type");
            SerializedProperty soundsArray = entry.FindPropertyRelative("sounds");
            SerializedProperty vfxArray = entry.FindPropertyRelative("vfx");
            SerializedProperty decal = entry.FindPropertyRelative("decalMaterial");
            string key = MakeEntryKey(groupKey, kind, type.enumValueIndex);
            bool open = openEntries.Contains(key);

            var card = new VisualElement();
            card.AddToClassList("surf-entry");

            var head = new VisualElement();
            head.AddToClassList("surf-entry__head");
            var typeField = new EnumField((Enum)Enum.ToObject(enumType, 0));
            typeField.AddToClassList("surf-entry__type");
            typeField.tooltip = "What this card is for. Each kind can appear once per surface; a repeated kind is ignored.";
            typeField.BindProperty(type);
            head.Add(typeField);

            int sounds = soundsArray.arraySize;
            int vfx = vfxArray.arraySize;
            head.Add(VeneerStyle.Pill(sounds == 1 ? "1 sound" : sounds + " sounds", sounds > 0 ? "info" : "idle"));
            head.Add(VeneerStyle.Pill(vfx + " VFX", vfx > 0 ? "info" : "idle"));
            if (decal.objectReferenceValue != null)
            {
                Label decalPill = VeneerStyle.Pill("decal", "info");
                decalPill.tooltip = decal.objectReferenceValue.name;
                head.Add(decalPill);
            }

            head.Add(VeneerStyle.Spacer());
            Button fold = VeneerStyle.IconButton(open ? "chevron-down" : "chevron-right", "Show or hide the sounds, VFX and decal of this card.", null, SurfaceBrand.Value);
            head.Add(fold);
            head.Add(VeneerStyle.IconButton("close", "Remove this card.", () => RemoveEntry(groupIndex, kind, index), SurfaceBrand.Value));
            card.Add(head);

            VisualElement hint = VeneerStyle.Hint("Another card already uses this kind; only the first one is played.", "warn", SurfaceBrand.Value);
            hint.AddToClassList("surf-entry__hint");
            VeneerStyle.SetDisplay(hint, false);
            card.Add(hint);
            entryViews.Add(new EntryView { Group = groupIndex, Kind = kind, Entry = index, Card = card, Hint = hint });

            // Keeps the remembered open state with the card when its kind is changed.
            typeField.RegisterValueChangedCallback(evt =>
            {
                string oldKey = MakeEntryKey(groupKey, kind, Convert.ToInt32(evt.previousValue));
                string newKey = MakeEntryKey(groupKey, kind, Convert.ToInt32(evt.newValue));
                if (openEntries.Remove(oldKey))
                {
                    openEntries.Add(newKey);
                    SaveFolds();
                }
            });

            var body = new VisualElement();
            body.AddToClassList("surf-entry__body");
            bool built = false;
            void EnsureBody()
            {
                if (built)
                {
                    return;
                }

                built = true;
                body.Add(BuildObjectList(
                    soundsArray.propertyPath, "Sounds", typeof(AudioClip), AcceptSound,
                    "One of these is picked at random each time. Drop several audio clips at once to add them all."));
                body.Add(BuildObjectList(
                    vfxArray.propertyPath, "VFX", typeof(ParticleSystem), AcceptEffect,
                    "One of these is picked at random each time. Drop particle system prefabs to add them."));

                var decalField = new ObjectField("Decal material") { objectType = typeof(Material), allowSceneObjects = false };
                decalField.AddToClassList("surf-decal");
                decalField.tooltip = decal.tooltip;
                decalField.BindProperty(decal);
                body.Add(decalField);
            }

            if (open)
            {
                EnsureBody();
            }

            VeneerStyle.SetDisplay(body, open);
            card.Add(body);

            fold.clicked += () =>
            {
                string current = EntryKey(groupIndex, kind, index);
                bool nowOpen = openEntries.Add(current);
                if (!nowOpen)
                {
                    openEntries.Remove(current);
                }
                else
                {
                    EnsureBody();
                }

                SaveFolds();
                VeneerStyle.SetDisplay(body, nowOpen);
                VeneerStyle.SetIcon(fold[0], nowOpen ? "chevron-down" : "chevron-right", SurfaceBrand.Value);
            };

            return card;
        }

        private string EntryKey(int groupIndex, SurfaceEntryKind kind, int entryIndex)
        {
            SerializedProperty group = Groups().GetArrayElementAtIndex(groupIndex);
            SerializedProperty entry = group.FindPropertyRelative(ListName(kind)).GetArrayElementAtIndex(entryIndex);
            return MakeEntryKey(GroupKey(group, groupIndex), kind, entry.FindPropertyRelative("type").enumValueIndex);
        }

        private static Object AcceptSound(Object dragged) => dragged as AudioClip;

        private static Object AcceptEffect(Object dragged)
        {
            if (dragged is ParticleSystem system)
            {
                return system;
            }

            return dragged is GameObject prefab && prefab.TryGetComponent<ParticleSystem>(out ParticleSystem found) ? found : null;
        }

        // ------------------------------------------------------------------ sounds / VFX lists

        // A compact list: a title with the count and a plus button, then one object field per item with a remove button. Assets dropped on
        // the list (several at once) that `accept` turns into an object are appended.
        private VisualElement BuildObjectList(string arrayPath, string title, Type objectType, Func<Object, Object> accept, string tooltip)
        {
            SerializedProperty array = serializedObject.FindProperty(arrayPath);
            var list = new VisualElement();
            list.AddToClassList("surf-list");
            list.tooltip = tooltip;

            var head = new VisualElement();
            head.AddToClassList("surf-list__head");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("surf-list__title");
            head.Add(titleLabel);
            head.Add(VeneerStyle.Pill(array.arraySize.ToString(), array.arraySize > 0 ? "info" : "idle"));
            head.Add(VeneerStyle.Spacer());
            head.Add(VeneerStyle.IconButton("plus", "Add an empty slot.", () => AddObject(arrayPath, null), SurfaceBrand.Value));
            list.Add(head);

            if (array.arraySize == 0)
            {
                var none = new Label("None. Drop assets here or press plus.");
                none.AddToClassList("surf-list__none");
                list.Add(none);
            }

            for (int i = 0; i < array.arraySize; i++)
            {
                int index = i;
                var row = new VisualElement();
                row.AddToClassList("surf-list__row");
                var field = new ObjectField { objectType = objectType, allowSceneObjects = false, label = string.Empty };
                field.BindProperty(array.GetArrayElementAtIndex(i));
                row.Add(field);
                row.Add(VeneerStyle.IconButton("close", "Remove this item.", () => RemoveObjectAtPath(arrayPath, index, "Remove " + title), SurfaceBrand.Value));
                list.Add(row);
            }

            list.RegisterCallback<DragUpdatedEvent>(evt => OnListDrag(evt, arrayPath, accept, false), TrickleDown.TrickleDown);
            list.RegisterCallback<DragPerformEvent>(evt => OnListDrag(evt, arrayPath, accept, true), TrickleDown.TrickleDown);
            return list;
        }

        private void OnListDrag(EventBase evt, string arrayPath, Func<Object, Object> accept, bool perform)
        {
            List<Object> accepted = null;
            foreach (Object dragged in DragAndDrop.objectReferences)
            {
                Object converted = accept(dragged);
                if (converted != null)
                {
                    (accepted ??= new List<Object>()).Add(converted);
                }
            }

            if (accepted == null)
            {
                return;
            }

            if (!perform)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            }
            else
            {
                DragAndDrop.AcceptDrag();
                serializedObject.UpdateIfRequiredOrScript();
                SerializedProperty array = serializedObject.FindProperty(arrayPath);
                foreach (Object item in accepted)
                {
                    array.arraySize++;
                    array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = item;
                }

                Commit("Add Dropped Assets");
                RebuildGroups();
            }

            evt.StopPropagation();
        }

        private void AddObject(string arrayPath, Object value)
        {
            serializedObject.UpdateIfRequiredOrScript();
            SerializedProperty array = serializedObject.FindProperty(arrayPath);
            array.arraySize++;
            array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = value;
            Commit("Add Slot");
            RebuildGroups();
        }

        private void RemoveObjectAtPath(string arrayPath, int index, string undoName)
        {
            serializedObject.UpdateIfRequiredOrScript();
            RemoveFromObjectArray(serializedObject.FindProperty(arrayPath), index);
            Commit(undoName);
            RebuildGroups();
        }

        private void RemoveObjectAt(int groupIndex, string listName, int index, string undoName)
        {
            SerializedProperty array = Groups().GetArrayElementAtIndex(groupIndex).FindPropertyRelative(listName);
            RemoveFromObjectArray(array, index);
            Commit(undoName);
            RebuildGroups();
        }

        // Deleting a non-null object reference only clears it; delete again to remove the slot.
        private static void RemoveFromObjectArray(SerializedProperty array, int index)
        {
            if (index < 0 || index >= array.arraySize)
            {
                return;
            }

            array.GetArrayElementAtIndex(index).objectReferenceValue = null;
            array.DeleteArrayElementAtIndex(index);
        }

        // ------------------------------------------------------------------ edits

        private SerializedProperty Groups()
        {
            serializedObject.UpdateIfRequiredOrScript();
            return serializedObject.FindProperty(GroupsPath);
        }

        private void Commit(string undoName)
        {
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName(undoName);
        }

        private void AddGroup()
        {
            SerializedProperty groups = Groups();
            // A new array element copies the last one, so start from a blank surface instead.
            groups.arraySize++;
            SerializedProperty added = groups.GetArrayElementAtIndex(groups.arraySize - 1);
            added.FindPropertyRelative("id").intValue = 0;
            added.FindPropertyRelative("header").stringValue = "New Surface";
            added.FindPropertyRelative("textures").arraySize = 0;
            ResetToGenericCard(added.FindPropertyRelative("footsteps"));
            ResetToGenericCard(added.FindPropertyRelative("impacts"));
            Commit("Add Surface");
            RebuildGroups();
        }

        // A new surface starts with a single Generic card (enum value 0), the fallback every other kind uses.
        private static void ResetToGenericCard(SerializedProperty entries)
        {
            entries.arraySize = 1;
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("type").enumValueIndex = 0;
            ClearEffects(entry);
        }

        private static void ClearEffects(SerializedProperty entry)
        {
            entry.FindPropertyRelative("sounds").arraySize = 0;
            entry.FindPropertyRelative("vfx").arraySize = 0;
            entry.FindPropertyRelative("decalMaterial").objectReferenceValue = null;
        }

        private void RemoveGroup(int index)
        {
            SerializedProperty groups = Groups();
            if (index >= groups.arraySize)
            {
                return;
            }

            groups.DeleteArrayElementAtIndex(index);
            Commit("Remove Surface");
            RebuildGroups();
        }

        private void MoveGroup(int from, int to)
        {
            SerializedProperty groups = Groups();
            if (from >= groups.arraySize || to >= groups.arraySize)
            {
                return;
            }

            groups.MoveArrayElement(from, to);
            Commit("Reorder Surfaces");
            RebuildGroups();
        }

        private void AddEntry(int groupIndex, SurfaceEntryKind kind)
        {
            SerializedProperty entries = Groups().GetArrayElementAtIndex(groupIndex).FindPropertyRelative(ListName(kind));
            Type enumType = kind == SurfaceEntryKind.Footstep ? typeof(FootstepType) : typeof(ImpactType);
            var used = new HashSet<int>();
            for (int i = 0; i < entries.arraySize; i++)
            {
                used.Add(entries.GetArrayElementAtIndex(i).FindPropertyRelative("type").enumValueIndex);
            }

            int freeType = 0;
            while (used.Contains(freeType))
            {
                freeType++;
            }

            if (freeType >= Enum.GetNames(enumType).Length)
            {
                return;
            }

            // A new array element copies the last one, so start from a blank card instead.
            entries.arraySize++;
            SerializedProperty added = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            added.FindPropertyRelative("type").enumValueIndex = freeType;
            ClearEffects(added);
            Commit("Add Surface Entry");

            // Opened right away so the sounds can be dropped in; the key is read after the commit (the group may have just got its id).
            openEntries.Add(EntryKey(groupIndex, kind, entries.arraySize - 1));
            SaveFolds();
            RebuildGroups();
        }

        private void RemoveEntry(int groupIndex, SurfaceEntryKind kind, int index)
        {
            SerializedProperty entries = Groups().GetArrayElementAtIndex(groupIndex).FindPropertyRelative(ListName(kind));
            if (index >= entries.arraySize)
            {
                return;
            }

            entries.DeleteArrayElementAtIndex(index);
            Commit("Remove Surface Entry");
            RebuildGroups();
        }

        // ------------------------------------------------------------------ fold state

        // Open/closed state lives in EditorPrefs per asset, so the Inspector comes back exactly as it was left. Surfaces keep their old key.
        private string FoldsKey(string what) =>
            "SurfaceSystem." + what + "." + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(Target));

        private void LoadFolds()
        {
            collapsedGroups.Clear();
            foreach (string part in EditorPrefs.GetString(FoldsKey("Groups"), string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part, out int id))
                {
                    collapsedGroups.Add(id);
                }
            }

            openEntries.Clear();
            foreach (string path in EditorPrefs.GetString(FoldsKey("OpenEntries"), string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                openEntries.Add(path);
            }
        }

        // Only surfaces with a real id are remembered; a not-yet-compiled one's key is just its position (negative).
        private void SaveFolds()
        {
            var ids = new List<string>();
            foreach (int id in collapsedGroups)
            {
                if (id > 0)
                {
                    ids.Add(id.ToString());
                }
            }

            var entries = new List<string>();
            foreach (string key in openEntries)
            {
                if (!key.StartsWith("-", StringComparison.Ordinal))
                {
                    entries.Add(key);
                }
            }

            EditorPrefs.SetString(FoldsKey("Groups"), string.Join(",", ids));
            EditorPrefs.SetString(FoldsKey("OpenEntries"), string.Join("|", entries));
        }

        // ------------------------------------------------------------------ thumbnail

        /// <summary>Shows a texture's asset preview in a picture element, looking again a few times while Unity renders it.</summary>
        private sealed class TextureThumbnail
        {
            private readonly VisualElement thumb;
            private readonly Label emptyLabel;
            private readonly SerializedProperty texture;
            private int attempts;

            public TextureThumbnail(VisualElement thumb, Label emptyLabel, SerializedProperty texture)
            {
                this.thumb = thumb;
                this.emptyLabel = emptyLabel;
                this.texture = texture;
            }

            public void Refresh()
            {
                attempts = 0;
                Update();
            }

            private void Update()
            {
                Object current = texture.objectReferenceValue;
                VeneerStyle.SetDisplay(emptyLabel, current == null);
                if (current == null)
                {
                    thumb.style.backgroundImage = StyleKeyword.None;
                    return;
                }

                // The preview ignores a useless alpha channel the way the surface's own shader does; until it is ready show the texture itself.
                Texture preview = AssetPreview.GetAssetPreview(current);
                if (preview == null)
                {
                    preview = current as Texture;
                    if (attempts < 12 && AssetPreview.IsLoadingAssetPreview(current.GetInstanceID()))
                    {
                        attempts++;
                        thumb.schedule.Execute(Update).StartingIn(150);
                    }
                }

                thumb.style.backgroundImage = preview is Texture2D image ? new StyleBackground(image) : StyleKeyword.None;
            }
        }
    }
}
