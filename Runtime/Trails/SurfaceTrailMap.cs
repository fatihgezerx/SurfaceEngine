using UnityEngine;

namespace SurfaceSystem
{
    /// <summary>
    /// One world-aligned trail map: a square render texture (and one or two more of the same kind, for ping-pong passes and the
    /// ripple simulation's previous step) covering <see cref="SurfaceTrails.AreaSize"/> meters around <see cref="SurfaceTrails.Origin"/>.
    /// Knows nothing about what it stores; <see cref="SurfaceTrails"/> decides the passes.
    /// </summary>
    internal sealed class SurfaceTrailMap
    {
        private readonly RenderTexture[] buffers;
        private readonly Color clearColor;
        private int current;

        /// <summary>The buffer that holds the latest content.</summary>
        public RenderTexture Current => buffers[current];

        /// <summary>The buffer before <see cref="Current"/> (the ripple simulation's previous step; a free target for any other map).</summary>
        public RenderTexture Previous => buffers[(current + buffers.Length - 1) % buffers.Length];

        /// <summary>A buffer that is neither <see cref="Current"/> nor, with three buffers, <see cref="Previous"/>: the target of the next pass.</summary>
        public RenderTexture Next => buffers[(current + 1) % buffers.Length];

        /// <summary>Seconds (or steps) of activity left before the map counts as settled and its passes can be skipped.</summary>
        public float ActiveFor;

        public SurfaceTrailMap(string name, int resolution, RenderTextureFormat format, int bufferCount, Color clearColor)
        {
            this.clearColor = clearColor;
            buffers = new RenderTexture[bufferCount];
            for (var i = 0; i < bufferCount; i++)
            {
                var texture = new RenderTexture(resolution, resolution, 0, format, RenderTextureReadWrite.Linear)
                {
                    name = name + " " + i,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    autoGenerateMips = false,
                };
                texture.Create();
                buffers[i] = texture;
            }

            Clear();
        }

        /// <summary>Resets every buffer to the map's empty value.</summary>
        public void Clear()
        {
            var previous = RenderTexture.active;
            foreach (var buffer in buffers)
            {
                RenderTexture.active = buffer;
                GL.Clear(false, true, clearColor);
            }

            RenderTexture.active = previous;
        }

        /// <summary>Makes <see cref="Next"/> the current buffer (after a pass wrote the new content into it).</summary>
        public void Advance() => current = (current + 1) % buffers.Length;

        /// <summary>
        /// Moves every buffer's content by <paramref name="texels"/> (the map origin moved the other way), through
        /// <paramref name="shiftMaterial"/>: texels that come in from outside get the empty value. The buffer order is kept.
        /// </summary>
        public void Shift(Vector2Int texels, Material shiftMaterial, int offsetId, int clearId)
        {
            var resolution = buffers[0].width;
            shiftMaterial.SetVector(offsetId, new Vector4(texels.x / (float)resolution, texels.y / (float)resolution, 0f, 0f));
            shiftMaterial.SetColor(clearId, clearColor);

            // Each buffer is shifted into the one after it, then the ring is rotated back so every buffer keeps its role.
            var count = buffers.Length;
            if (count == 1)
            {
                return;
            }

            var spare = buffers[(current + 1) % count];
            if (count == 2)
            {
                // Two buffers: the spare is the free one; shift current into it and swap.
                Graphics.Blit(buffers[current], spare, shiftMaterial);
                current = (current + 1) % count;
                return;
            }

            // Three buffers: previous and current hold content, next is free. previous -> next, current -> old previous, then rotate so
            // that current points at the shifted current.
            var previousIndex = (current + count - 1) % count;
            Graphics.Blit(buffers[previousIndex], spare, shiftMaterial);
            Graphics.Blit(buffers[current], buffers[previousIndex], shiftMaterial);
            // Now: spare = shifted previous, buffers[previousIndex] = shifted current, buffers[current] = stale (free).
            // Rotate the array so Previous = spare, Current = buffers[previousIndex], Next = buffers[current].
            var shiftedPrevious = spare;
            var shiftedCurrent = buffers[previousIndex];
            var free = buffers[current];
            buffers[0] = shiftedPrevious;
            buffers[1] = shiftedCurrent;
            buffers[2] = free;
            current = 1;
        }

        /// <summary>Frees the render textures.</summary>
        public void Release()
        {
            foreach (var buffer in buffers)
            {
                if (buffer == null)
                {
                    continue;
                }

                buffer.Release();
                if (Application.isPlaying)
                {
                    Object.Destroy(buffer);
                }
                else
                {
                    Object.DestroyImmediate(buffer);
                }
            }
        }
    }
}
