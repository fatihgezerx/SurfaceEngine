using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Veneer;
using Object = UnityEngine.Object;

namespace SurfaceSystem
{
    /// <summary>
    /// Tools &gt; Surface Engine &gt; Trail Monitor: a live look at the trail system in Play Mode. A KPI row (sources, pending pushes,
    /// origin, area, state), the Bend, Ripple and Deform maps drawn as pictures, the current wind, and a toolbar with the Rain slider,
    /// a Push here button and a Clear button. Everything refreshes ten times a second through the kit's <c>Every</c>, only while the
    /// window is on screen and the maps exist; the pictures are small display textures the window owns (made by the "Trail View"
    /// shader) and releases in <c>OnDisable</c>. The refresh keeps what it last showed and sets a text only when it changed.
    /// </summary>
    internal sealed class SurfaceTrailMonitorWindow : VeneerWindow
    {
        private const string ViewShaderName = "Hidden/SurfaceEngine/Trail View";
        private const float PreviewSize = 256f;
        private const long RefreshMilliseconds = 100;
        private const int MapCount = 3;

        private static readonly string[] MapTitles = { "Bend", "Ripple", "Deform" };
        private static readonly string[] MapIcons = { "move", "water", "layers" };
        private static readonly string[] MapHints =
        {
            "Where grass is pushed: hue is the direction, brightness the strength.",
            "Water surface: mid grey is flat, bright a crest, dark a trough.",
            "Snow and sand prints: black untouched, white the deepest print.",
        };

        private readonly RenderTexture[] displays = new RenderTexture[MapCount];
        private readonly Image[] images = new Image[MapCount];
        private readonly Label[] notes = new Label[MapCount];
        private readonly int[] noteStates = new int[MapCount];

        private Material viewMaterial;
        private bool shaderMissing;

        private Slider rainSlider;
        private Label sourcesLabel;
        private Label pendingLabel;
        private Label originLabel;
        private Label areaLabel;
        private Label windLabel;

        private int shownState = -1;
        private int shownSources = -1;
        private int shownPending = -1;
        private int shownOriginX = int.MinValue;
        private int shownOriginZ = int.MinValue;
        private int shownAreaSize = -1;
        private int shownResolution = -1;
        private int shownTexelTenths = -1;
        private bool shownRunning;
        private bool shownPillSet;
        private int shownWindState = -1;
        private int shownWindX;
        private int shownWindZ;
        private int shownWindStrength;
        private int shownWindGusts;

        /// <inheritdoc />
        protected override VeneerBrand Brand => SurfaceBrand.Value;

        /// <inheritdoc />
        protected override string WindowTitle => "Trail Monitor";

        [MenuItem("Tools/Surface Engine/Trail Monitor")]
        private static void Open()
        {
            GetWindow<SurfaceTrailMonitorWindow>().Show();
        }

        private void OnDisable()
        {
            for (int i = 0; i < MapCount; i++)
            {
                ReleaseDisplay(i);
            }

            if (viewMaterial != null)
            {
                DestroyImmediate(viewMaterial);
            }

            viewMaterial = null;
        }

        /// <inheritdoc />
        protected override void BuildToolbar(VisualElement toolbar)
        {
            Label rainLabel = VeneerStyle.MutedLabel("Rain");
            rainLabel.style.marginRight = 4f;
            toolbar.Add(rainLabel);

            rainSlider = new Slider(0f, 1f);
            rainSlider.style.width = 120f;
            rainSlider.tooltip = "Rain on the water, 0 (none) to 1 (the most drops a second).";
            rainSlider.RegisterValueChangedCallback(OnRainChanged);
            toolbar.Add(rainSlider);

            toolbar.Add(VeneerStyle.Button("Push here", "target", "Stamp one strong push at the focus (or the first Surface Trail Source).", PushHere, false, Brand));
            toolbar.Add(VeneerStyle.Button("Clear", "refresh", "Wipe all three trail maps.", ClearTrails, false, Brand));
        }

        /// <inheritdoc />
        protected override void BuildContent(VisualElement content)
        {
            // Pictures from an earlier tree are gone with it: forget them so the first refresh binds the new ones.
            for (int i = 0; i < MapCount; i++)
            {
                images[i] = null;
                notes[i] = null;
                noteStates[i] = -1;
            }

            shownState = -1;
            shownSources = -1;
            shownPending = -1;
            shownOriginX = int.MinValue;
            shownOriginZ = int.MinValue;
            shownAreaSize = -1;
            shownResolution = -1;
            shownTexelTenths = -1;
            shownPillSet = false;
            shownWindState = -1;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1f;
            content.Add(scroll);

            VisualElement kpis = VeneerStyle.Row(true);
            scroll.Add(kpis);
            kpis.Add(GrowKpi(VeneerStyle.Kpi("Sources", "users", out sourcesLabel, Brand)));
            kpis.Add(GrowKpi(VeneerStyle.Kpi("Pending pushes", "list", out pendingLabel, Brand)));
            kpis.Add(GrowKpi(VeneerStyle.Kpi("Origin (x, z)", "target", out originLabel, Brand)));
            kpis.Add(GrowKpi(VeneerStyle.Kpi("Area", "layers", out areaLabel, Brand)));

            VisualElement maps = VeneerStyle.Row(true);
            scroll.Add(maps);
            for (int i = 0; i < MapCount; i++)
            {
                maps.Add(BuildMapCard(i));
            }

            VisualElement windCard = VeneerStyle.Card("Wind", "water", Brand);
            windLabel = VeneerStyle.MutedLabel(string.Empty);
            windCard.Add(windLabel);
            scroll.Add(windCard);

            Every(RefreshMilliseconds, Refresh);
            Refresh();
        }

        private static VisualElement GrowKpi(VisualElement kpi)
        {
            kpi.style.flexGrow = 1f;
            return kpi;
        }

        private VisualElement BuildMapCard(int index)
        {
            VisualElement card = VeneerStyle.Card(MapTitles[index], MapIcons[index], Brand);
            card.style.flexGrow = 1f;
            card.style.minWidth = PreviewSize + 40f;

            var image = new Image { scaleMode = ScaleMode.ScaleToFit };
            image.style.width = PreviewSize;
            image.style.height = PreviewSize;
            image.style.alignSelf = Align.Center;
            images[index] = image;
            card.Add(image);

            Label note = VeneerStyle.MutedLabel(MapHints[index]);
            note.style.whiteSpace = WhiteSpace.Normal;
            notes[index] = note;
            card.Add(note);
            return card;
        }

        private void OnRainChanged(ChangeEvent<float> evt)
        {
            if (SurfaceTrails.IsInitialized)
            {
                SurfaceTrails.Rain = evt.newValue;
            }
        }

        private static void PushHere()
        {
            if (!SurfaceTrails.IsInitialized)
            {
                return;
            }

            Transform focus = SurfaceTrails.Focus;
            Vector3 position;
            if (focus != null)
            {
                position = focus.position;
            }
            else
            {
                SurfaceTrailSource source = Object.FindFirstObjectByType<SurfaceTrailSource>();
                if (source == null)
                {
                    Debug.LogWarning("Trail Monitor: no trail focus and no Surface Trail Source in the scene to push at.");
                    return;
                }

                position = source.transform.position;
            }

            SurfaceTrails.Push(position, 2f, 1f, 3f, 0.5f);
        }

        private static void ClearTrails()
        {
            if (SurfaceTrails.IsInitialized)
            {
                SurfaceTrails.Clear();
            }
        }

        // Runs ten times a second while the tree is on a panel. Allocation-free: texts are set only when a value changed.
        private void Refresh()
        {
            if (RootElement == null || RootElement.panel == null)
            {
                return;
            }

            bool ready = Application.isPlaying && SurfaceTrails.IsInitialized;
            int state = ready ? 1 : 0;
            if (state != shownState)
            {
                shownState = state;
                if (ready)
                {
                    HideEmptyState();
                }
                else
                {
                    ShowEmptyState(
                        "info",
                        "Trails are not running",
                        "Enter Play Mode with Trails enabled in Surface Data (Settings > Trails) and a Surface Trail Source in the scene.");
                    shownPillSet = false;
                }
            }

            if (!ready)
            {
                return;
            }

            RefreshNumbers();
            RefreshMaps();
            RefreshWind();

            float rain = SurfaceTrails.Rain;
            if (rainSlider != null && rainSlider.value != rain)
            {
                rainSlider.SetValueWithoutNotify(rain);
            }
        }

        private void RefreshNumbers()
        {
            bool running = SurfaceTrails.IsRunning;
            if (!shownPillSet || running != shownRunning)
            {
                shownPillSet = true;
                shownRunning = running;
                SetPill(running ? "Running" : "Idle", running ? "ok" : "idle");
            }

            int sources = SurfaceTrails.SourceCount;
            if (sources != shownSources)
            {
                shownSources = sources;
                sourcesLabel.text = sources.ToString(CultureInfo.InvariantCulture);
            }

            int pending = SurfaceTrails.PendingPushes;
            if (pending != shownPending)
            {
                shownPending = pending;
                pendingLabel.text = pending.ToString(CultureInfo.InvariantCulture);
            }

            Vector2 origin = SurfaceTrails.Origin;
            int originX = Mathf.RoundToInt(origin.x * 10f);
            int originZ = Mathf.RoundToInt(origin.y * 10f);
            if (originX != shownOriginX || originZ != shownOriginZ)
            {
                shownOriginX = originX;
                shownOriginZ = originZ;
                originLabel.text = Tenths(originX) + ", " + Tenths(originZ);
            }

            int areaSize = Mathf.RoundToInt(SurfaceTrails.AreaSize);
            int resolution = SurfaceTrails.Resolution;
            int texelTenths = Mathf.RoundToInt(SurfaceTrails.TexelSize * 1000f);
            if (areaSize != shownAreaSize || resolution != shownResolution || texelTenths != shownTexelTenths)
            {
                shownAreaSize = areaSize;
                shownResolution = resolution;
                shownTexelTenths = texelTenths;
                areaLabel.text = areaSize.ToString(CultureInfo.InvariantCulture) + " m / " + resolution.ToString(CultureInfo.InvariantCulture)
                    + " px / " + Tenths(texelTenths) + " cm per texel";
            }
        }

        private void RefreshMaps()
        {
            if (viewMaterial == null && !shaderMissing)
            {
                Shader shader = Shader.Find(ViewShaderName);
                if (shader == null)
                {
                    shaderMissing = true;
                }
                else
                {
                    viewMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
            }

            UpdateMap(0, SurfaceTrails.BendMap);
            UpdateMap(1, SurfaceTrails.RippleMap);
            UpdateMap(2, SurfaceTrails.DeformMap);
        }

        private void UpdateMap(int index, RenderTexture map)
        {
            // Note state: 0 = showing the map, 1 = this map is off, 2 = the view shader was not found.
            int noteState = map == null ? 1 : (viewMaterial == null ? 2 : 0);
            if (noteState != noteStates[index])
            {
                noteStates[index] = noteState;
                if (noteState == 0)
                {
                    notes[index].text = MapHints[index];
                }
                else if (noteState == 1)
                {
                    notes[index].text = "This map is off in Surface Data (Settings > Trails).";
                }
                else
                {
                    notes[index].text = "The Trail View shader was not found.";
                }

                if (noteState != 0)
                {
                    images[index].image = null;
                }
            }

            if (noteState != 0)
            {
                return;
            }

            RenderTexture display = displays[index];
            if (display == null || display.width != map.width || display.height != map.height)
            {
                ReleaseDisplay(index);
                display = new RenderTexture(map.width, map.height, 0, RenderTextureFormat.ARGB32)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                };
                display.Create();
                displays[index] = display;
                images[index].image = display;
            }
            else if (images[index].image != display)
            {
                images[index].image = display;
            }

            Graphics.Blit(map, display, viewMaterial, index);
        }

        private void RefreshWind()
        {
            bool active = SurfaceWind.IsActive;
            Vector4 wind = SurfaceWind.Current;
            int x = Mathf.RoundToInt(wind.x * 100f);
            int z = Mathf.RoundToInt(wind.y * 100f);
            int strength = Mathf.RoundToInt(wind.z * 100f);
            int gusts = Mathf.RoundToInt(wind.w * 100f);
            int state = active ? 1 : 0;
            if (state == shownWindState && (!active || (x == shownWindX && z == shownWindZ && strength == shownWindStrength && gusts == shownWindGusts)))
            {
                return;
            }

            shownWindState = state;
            shownWindX = x;
            shownWindZ = z;
            shownWindStrength = strength;
            shownWindGusts = gusts;
            windLabel.text = active
                ? "dir " + Hundredths(x) + ", " + Hundredths(z) + "  /  strength " + Hundredths(strength) + "  /  gusts " + Hundredths(gusts)
                : "no SurfaceWind in the scene";
        }

        private void ReleaseDisplay(int index)
        {
            RenderTexture display = displays[index];
            if (display == null)
            {
                return;
            }

            if (images[index] != null)
            {
                images[index].image = null;
            }

            display.Release();
            DestroyImmediate(display);
            displays[index] = null;
        }

        private static string Tenths(int tenths)
        {
            return (tenths / 10f).ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static string Hundredths(int hundredths)
        {
            return (hundredths / 100f).ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}
