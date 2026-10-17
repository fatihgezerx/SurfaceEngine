using System;
using Veneer;

namespace SurfaceSystem
{
    /// <summary>
    /// The design of the <see cref="SurfaceData"/> Inspector that Surface Engine ships: two tabs. <b>Surfaces</b> holds the explanation
    /// and the list of surfaces (one custom widget); <b>Settings</b> has the Ground detection, Sound playback and Decals cards. The
    /// state pill, the Compile row and the validation hints are the system's own items. With Veneer installed a project can copy it in
    /// the Inspector Designer and change the copy; this default is never saved as an asset.
    /// </summary>
    internal sealed class SurfaceDataDefaultLayout : IVeneerDefaultLayoutProvider
    {
        private const string GroupsInfo =
            "Every group is a surface (e.g. \"Grass\", \"Wood\", \"Snow\"). Its name becomes its SurfaceGroups member on Compile, so names " +
            "must be unique. Its Textures must be the exact same Texture2D asset used by the surface's material (or Terrain layer) - " +
            "SurfaceEngine matches a hit against them to identify the surface, so they aren't just previews. Press Compile after any " +
            "change - and after renaming or reordering groups.";

        // The segmented Playback buttons have icons only, so the explanation of both modes goes on the row.
        private const string PlaybackTooltip =
            "Instantiate: a temporary AudioSource per sound.\n" +
            "Pool: reuse instances from a Pool System pool.";

        private const string Settings = "settings.";
        private const string Mode = "settings.audioPlaybackMode";

        /// <inheritdoc />
        public Type TargetType => typeof(SurfaceData);

        /// <inheritdoc />
        public VeneerLayout CreateLayout()
        {
            // The first card has no visible title: it only holds the explanation and the list, drawn like loose tab content. The
            // header pill and the after-header items need an entry in some card, so they sit here too.
            VeneerLayout layout = new VeneerLayoutBuilder(typeof(SurfaceData), "Surface Data", "Surfaces, footsteps and impacts")
                .Page("Surfaces", "layers")
                .Card("Surfaces", null, false)
                .Custom(SurfaceDataItems.StateId)
                .Custom(SurfaceDataItems.ActionsId)
                .Custom(SurfaceDataItems.ChecksId)
                .Note(GroupsInfo)
                .Field("groups")
                .Page("Settings", "settings")
                .Card("Ground detection", "target")
                .Field(Settings + "groundLayers", "Ground layers")
                .Slider(Settings + "defaultRayDistance", "Footstep ray distance", "Short", "Long", "m")
                .Card("Sound playback", "sound")
                .Enum(Mode, "Playback", "play,layers")
                .Field(Settings + "poolFootstepTypeName", "Footstep pool type")
                .ShowIf(Mode, VeneerCondition.Equals, "Pool")
                .Field(Settings + "poolImpactTypeName", "Impact pool type")
                .ShowIf(Mode, VeneerCondition.Equals, "Pool")
                .Custom(SurfaceDataItems.PoolHintId)
                .ShowIf(Mode, VeneerCondition.Equals, "Pool")
                .Card("Decals", "image")
                .Slider(Settings + "effectLifetime", "Decal lifetime", "Brief", "Lasting", "s")
                .Slider(Settings + "decalFadeTime", "Fade out time", "Instant", "Slow", "s")
                .Field(Settings + "decalSize", "Default size (m)", widget: VeneerWidget.Vector)
                .Slider(Settings + "decalDepth", "Projection depth", "Shallow", "Deep", "m")
                .Build();

            VeneerLayoutEntry playback = layout.FindEntry(Mode, out _, out _);
            if (playback != null)
            {
                playback.Tooltip = PlaybackTooltip;
            }

            // The look of the system (accent, style sheet). UseThemeLogo stays off on purpose: the Inspector's builder already carries the
            // brand and shows its logo in the header, and the renderer would put a second logo next to it.
            layout.ThemeBrand = SurfaceBrand.Value.Name;
            return layout;
        }

        /// <inheritdoc />
        public void RegisterItems()
        {
            SurfaceDataItems.Register();
        }
    }
}
