using Veneer;
using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>The Surface Engine brand for the Veneer look: orange-brown accent, logo and banner from <c>Editor/Brand</c>.</summary>
    internal static class SurfaceBrand
    {
        /// <summary>The accent class the brand style sheet answers to (the exact orange-brown, instead of a kit preset).</summary>
        internal const string AccentClass = "surface-accent";

        /// <summary>The cached brand instance; use it for every Inspector and window of the system.</summary>
        internal static readonly VeneerBrand Value = VeneerBrand.FromAssets(
            name: "Surface Engine",
            subtitle: "Footsteps and impacts by surface",
            accent: new Color32(0xD9, 0x82, 0x2B, 255),
            logoPath: "Assets/Scripts/SurfaceEngine/Editor/Brand/logo.png",
            bannerPath: "Assets/Scripts/SurfaceEngine/Editor/Brand/banner.png",
            iconFolder: null,
            styleSheetPath: "Assets/Scripts/SurfaceEngine/Editor/Brand/SurfaceBrand.uss",
            accentClass: AccentClass);
    }
}
