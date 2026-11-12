namespace SurfaceSystem
{
    /// <summary>
    /// Texels per side of the trail maps (<see cref="SurfaceTrailSettings.Resolution"/>). With a 64 m area, Medium gives
    /// 12.5 cm texels - enough for a footprint; High halves that for close-up snow at four times the memory and fill cost.
    /// </summary>
    public enum SurfaceTrailResolution
    {
        Low = 256,
        Medium = 512,
        High = 1024,
        Ultra = 2048,
    }
}
