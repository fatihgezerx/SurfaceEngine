using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>What is wrong with one surface group, in the terms <see cref="SurfaceCompiler.Compile"/> uses.</summary>
    internal enum SurfaceGroupIssue : byte
    {
        /// <summary>Nothing.</summary>
        None,

        /// <summary>The name is empty or has no letter or digit: Compile aborts.</summary>
        NoName,

        /// <summary>The name is "None" (reserved for the unassigned value): Compile aborts.</summary>
        Reserved,

        /// <summary>Another group has the same generated name: Compile aborts.</summary>
        Duplicate,
    }

    /// <summary>The two lists of typed entries a group holds.</summary>
    internal enum SurfaceEntryKind : byte
    {
        /// <summary>The <c>footsteps</c> list.</summary>
        Footstep,

        /// <summary>The <c>impacts</c> list.</summary>
        Impact,
    }

    /// <summary>
    /// Validates a <see cref="SurfaceData"/> the way Compile does (same name rule, same abort cases) and says whether the generated
    /// <see cref="SurfaceGroups"/> enum is up to date, plus two warnings Compile does not give: a group without textures (a hit can never
    /// be identified as it) and a texture used by two groups (only one of them can win). The result is cached and recomputed only when a
    /// cheap signature of the data changes, so the Inspector can ask every 200 ms.
    /// </summary>
    internal sealed class SurfaceDataAnalysis
    {
        private const double MinimumInterval = 0.05;

        // The compiled enum, read once per script load: it only changes with a domain reload.
        private static readonly string[] CompiledNames = Enum.GetNames(typeof(SurfaceGroups));
        private static readonly Array CompiledValues = Enum.GetValues(typeof(SurfaceGroups));

        private readonly SurfaceData data;
        private readonly List<SurfaceGroupIssue> issues = new List<SurfaceGroupIssue>();
        private readonly List<string> members = new List<string>();
        private readonly List<bool[]> footstepDuplicates = new List<bool[]>();
        private readonly List<bool[]> impactDuplicates = new List<bool[]>();
        private readonly Dictionary<string, int> nameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<Texture2D, int> textureOwners = new Dictionary<Texture2D, int>();
        private double lastCheck = double.NegativeInfinity;
        private int signature;
        private bool hasSignature;

        /// <summary>Creates the analysis of one asset.</summary>
        public SurfaceDataAnalysis(SurfaceData data)
        {
            this.data = data;
        }

        /// <summary>Increases every time the data changed in a way the display must follow.</summary>
        public int Version { get; private set; }

        /// <summary>One line: surfaces and textures.</summary>
        public string Summary { get; private set; } = string.Empty;

        /// <summary>Text of the state pill.</summary>
        public string PillText { get; private set; } = string.Empty;

        /// <summary>Kind of the state pill (<c>ok</c>, <c>warn</c>, <c>error</c>, <c>idle</c>).</summary>
        public string PillKind { get; private set; } = "idle";

        /// <summary>Whether scripts are being compiled right now (the enum is about to change).</summary>
        public static bool IsCompiling => EditorApplication.isCompiling;

        /// <summary>A line saying Compile is needed, or null.</summary>
        public string NeedsCompileMessage { get; private set; }

        /// <summary>A line naming the groups whose name makes Compile abort, or null.</summary>
        public string NameMessage { get; private set; }

        /// <summary>A line naming the groups without textures, or null.</summary>
        public string NoTexturesMessage { get; private set; }

        /// <summary>A line naming a texture two groups share, or null.</summary>
        public string SharedTextureMessage { get; private set; }

        /// <summary>How many groups the analysis saw.</summary>
        public int GroupCount => issues.Count;

        /// <summary>What is wrong with the group at <paramref name="group"/>.</summary>
        public SurfaceGroupIssue GetIssue(int group) => group >= 0 && group < issues.Count ? issues[group] : SurfaceGroupIssue.None;

        /// <summary>Whether the entry's type is already used by an earlier entry of the same list (only the first one is played).</summary>
        public bool IsDuplicateEntry(int group, SurfaceEntryKind kind, int entry)
        {
            List<bool[]> source = kind == SurfaceEntryKind.Footstep ? footstepDuplicates : impactDuplicates;
            if (group < 0 || group >= source.Count)
            {
                return false;
            }

            bool[] flags = source[group];
            return entry >= 0 && entry < flags.Length && flags[entry];
        }

        /// <summary>Recomputes when the data (or the compiled enum, or the compile state) changed; at most every 50 ms unless forced.</summary>
        public void Refresh(bool force = false)
        {
            double now = EditorApplication.timeSinceStartup;
            if (!force && now - lastCheck < MinimumInterval)
            {
                return;
            }

            lastCheck = now;
            int current = ComputeSignature();
            if (hasSignature && current == signature)
            {
                return;
            }

            signature = current;
            hasSignature = true;
            Analyse();
            Version++;
        }

        private int ComputeSignature()
        {
            unchecked
            {
                List<SurfaceGroup> groups = data.Groups;
                int hash = groups.Count;
                for (int i = 0; i < groups.Count; i++)
                {
                    SurfaceGroup group = groups[i];
                    if (group == null)
                    {
                        hash = hash * 31 + 1;
                        continue;
                    }

                    hash = hash * 31 + (group.Header != null ? group.Header.GetHashCode() : 0);
                    hash = hash * 31 + group.Id;
                    List<Texture2D> textures = group.Textures;
                    hash = hash * 31 + textures.Count;
                    for (int t = 0; t < textures.Count; t++)
                    {
                        hash = hash * 31 + (textures[t] != null ? textures[t].GetInstanceID() : 0);
                    }

                    List<FootstepEntry> footsteps = group.Footsteps;
                    hash = hash * 31 + footsteps.Count;
                    for (int f = 0; f < footsteps.Count; f++)
                    {
                        hash = hash * 31 + (footsteps[f] != null ? (int)footsteps[f].Type + 1 : 0);
                    }

                    List<ImpactEntry> impacts = group.Impacts;
                    hash = hash * 31 + impacts.Count;
                    for (int m = 0; m < impacts.Count; m++)
                    {
                        hash = hash * 31 + (impacts[m] != null ? (int)impacts[m].Type + 1 : 0);
                    }
                }

                return hash;
            }
        }

        private void Analyse()
        {
            List<SurfaceGroup> groups = data.Groups;
            issues.Clear();
            members.Clear();
            footstepDuplicates.Clear();
            impactDuplicates.Clear();
            nameCounts.Clear();
            textureOwners.Clear();

            for (int i = 0; i < groups.Count; i++)
            {
                SurfaceGroup group = groups[i];
                string member = group != null ? SurfaceCompiler.ToIdentifier(group.Header) : null;
                members.Add(member);
                if (member != null)
                {
                    nameCounts.TryGetValue(member, out int seen);
                    nameCounts[member] = seen + 1;
                }
            }

            int totalTextures = 0;
            var noName = new List<string>();
            var noTextures = new List<string>();
            string shared = null;
            bool anyNameIssue = false;
            bool needsCompile = false;
            var compiled = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int c = 0; c < CompiledNames.Length; c++)
            {
                if (CompiledNames[c] != nameof(SurfaceGroups.None))
                {
                    compiled[CompiledNames[c]] = Convert.ToInt32(CompiledValues.GetValue(c));
                }
            }

            int expected = 0;
            for (int i = 0; i < groups.Count; i++)
            {
                SurfaceGroup group = groups[i];
                string member = members[i];
                SurfaceGroupIssue issue = SurfaceGroupIssue.None;
                if (group == null || member == null)
                {
                    issue = SurfaceGroupIssue.NoName;
                }
                else if (member == nameof(SurfaceGroups.None))
                {
                    issue = SurfaceGroupIssue.Reserved;
                }
                else if (nameCounts[member] > 1)
                {
                    issue = SurfaceGroupIssue.Duplicate;
                }

                issues.Add(issue);
                if (issue != SurfaceGroupIssue.None)
                {
                    anyNameIssue = true;
                    noName.Add(group != null && !string.IsNullOrWhiteSpace(group.Header) ? "'" + group.Header + "'" : "#" + (i + 1));
                }

                if (group == null)
                {
                    footstepDuplicates.Add(Array.Empty<bool>());
                    impactDuplicates.Add(Array.Empty<bool>());
                    continue;
                }

                expected++;
                if (member == null || !compiled.TryGetValue(member, out int value) || value != group.Id)
                {
                    needsCompile = true;
                }

                int textureCount = 0;
                foreach (Texture2D texture in group.Textures)
                {
                    if (texture == null)
                    {
                        continue;
                    }

                    textureCount++;
                    if (textureOwners.TryGetValue(texture, out int owner))
                    {
                        if (owner != i && shared == null)
                        {
                            shared = "The texture '" + texture.name + "' is in both '" + groups[owner].Header + "' and '" + group.Header +
                                     "'. A hit can match only one surface, so one of them wins.";
                        }
                    }
                    else
                    {
                        textureOwners[texture] = i;
                    }
                }

                totalTextures += textureCount;
                if (textureCount == 0)
                {
                    noTextures.Add(string.IsNullOrWhiteSpace(group.Header) ? "#" + (i + 1) : "'" + group.Header + "'");
                }

                footstepDuplicates.Add(FindDuplicates(group.Footsteps.Count, k => group.Footsteps[k] != null ? (int)group.Footsteps[k].Type : -1));
                impactDuplicates.Add(FindDuplicates(group.Impacts.Count, k => group.Impacts[k] != null ? (int)group.Impacts[k].Type : -1));
            }

            if (compiled.Count != expected)
            {
                needsCompile = true;
            }

            Summary = Plural(groups.Count, "surface") + ", " + Plural(totalTextures, "texture");
            NameMessage = anyNameIssue
                ? "Compile will abort: " + string.Join(", ", noName) + " has an empty, reserved ('None') or repeated name. Every surface needs a unique name."
                : null;
            NoTexturesMessage = noTextures.Count > 0
                ? "No textures in " + string.Join(", ", noTextures) + ": SurfaceEngine matches a hit against a surface's textures, so it can never be identified."
                : null;
            SharedTextureMessage = shared;
            NeedsCompileMessage = needsCompile && !anyNameIssue && groups.Count > 0
                ? "The SurfaceGroups enum is out of date. Press Compile so code can use the new or renamed surfaces."
                : null;

            if (groups.Count == 0)
            {
                PillText = "Empty";
                PillKind = "idle";
            }
            else if (anyNameIssue)
            {
                PillText = "Errors";
                PillKind = "error";
            }
            else if (needsCompile)
            {
                PillText = "Needs compile";
                PillKind = "warn";
            }
            else
            {
                PillText = "Compiled";
                PillKind = "ok";
            }
        }

        // Flags every entry whose type an earlier entry of the list already uses.
        private static bool[] FindDuplicates(int count, Func<int, int> typeAt)
        {
            var flags = new bool[count];
            var used = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                int type = typeAt(i);
                if (type >= 0 && !used.Add(type))
                {
                    flags[i] = true;
                }
            }

            return flags;
        }

        private static string Plural(int count, string noun) => count + " " + noun + (count == 1 ? string.Empty : "s");
    }
}
