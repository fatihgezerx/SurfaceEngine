namespace SurfaceSystem
{
    /// <summary>
    /// What kind of thing caused an Impact, so the same surface can sound/look different for a sword hit than
    /// for a gunshot. A plain hand-written enum, not Compile-generated: add to it directly as new weapon/damage
    /// categories come up.
    /// </summary>
    public enum ImpactType
    {
        /// <summary>No specific type, or a type the surface doesn't define its own entry for - see <see cref="SurfaceGroup.ResolveImpact"/>.</summary>
        Generic,
        Sword,
        Axe,
        Blunt,
        Bow,
        Gun,
        Explosive,
    }
}
