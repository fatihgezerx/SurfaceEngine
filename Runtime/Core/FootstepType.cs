namespace SurfaceSystem
{
    /// <summary>
    /// What kind of step is being taken, so the same surface can sound/look different for a crouching step than
    /// for a sprint or a landing. A plain hand-written enum, not Compile-generated: add to it directly as new
    /// movement kinds come up.
    /// </summary>
    public enum FootstepType
    {
        /// <summary>No specific type, or a type the surface doesn't define its own entry for - see <see cref="SurfaceGroup.ResolveFootstep"/>.</summary>
        Generic,
        Crouch,
        Walk,
        Run,
        JumpStart,
        JumpLand,
    }
}
