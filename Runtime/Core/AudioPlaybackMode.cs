namespace SurfaceSystem
{
    /// <summary>
    /// How <see cref="SurfaceEngine"/> plays a Footstep/Impact sound - see <see cref="SurfaceSettings.PoolAudioTypeName"/>
    /// for the field Pool mode uses.
    /// </summary>
    public enum AudioPlaybackMode
    {
        /// <summary>Always <c>AudioSource.PlayClipAtPoint</c>s - no configuration needed, a fresh temporary AudioSource is created and destroyed per call.</summary>
        Instantiate,

        /// <summary>Gets an instance from the PoolSystem pool named <see cref="SurfaceSettings.PoolAudioTypeName"/> and releases it back once the clip ends - falls back to Instantiate (with a warning) if the name isn't a registered pool.</summary>
        Pool,
    }
}
