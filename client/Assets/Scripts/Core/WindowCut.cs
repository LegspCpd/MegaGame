using UnityEngine;

namespace Megame.Client
{
    /// <summary>
    /// Cuts window openings into solid building shells.
    ///
    /// Every piece of geometry is accumulated into shared MeshBuilders rather
    /// than becoming its own GameObject. A first pass built each pane, sill and
    /// mullion as a separate primitive, which came to roughly 32 000 objects
    /// across a hundred buildings; merged, a whole city facade costs three
    /// draw calls instead.
    /// </summary>
    public static class WindowCut
    {
        private static MeshBuilder _frame;
        private static MeshBuilder _glass;

        /// <summary>Opens a window collector; call Flush once done.</summary>
        public static void Begin()
        {
            _frame = new MeshBuilder();
            _glass = new MeshBuilder();
        }

        /// <summary>
        /// Emits the pieces of one wall around a row of window openings.
        /// Geometry is skipped when the wall is too narrow or too short to hold
        /// them, so callers do not need to pre-check.
        /// </summary>
        public static void Build(Vector3 centre, Vector3 size,
                                 float windowW, float sillH, float windowH,
                                 int columns)
        {
            if (_frame == null) return;

            float gap = windowW * columns + 0.9f * (columns - 1);
            if (gap >= size.x * 0.86f) return;
            if (size.y < sillH + windowH + 0.6f) return;

            float pier = (size.x - gap) * 0.5f;
            if (pier < 0.25f) return;

            // Flanking piers.
            _frame.AddBox(centre + new Vector3(-(gap * 0.5f + pier * 0.5f), 0f, 0f),
                new Vector3(pier, size.y, size.z));
            _frame.AddBox(centre + new Vector3(gap * 0.5f + pier * 0.5f, 0f, 0f),
                new Vector3(pier, size.y, size.z));

            // Band under the sill and lintel over the head.
            float lintelH = size.y - sillH - windowH;

            _frame.AddBox(centre + new Vector3(0f, -size.y * 0.5f + sillH * 0.5f, 0f),
                new Vector3(gap, sillH, size.z));
            _frame.AddBox(centre + new Vector3(0f, sillH + windowH + lintelH * 0.5f, 0f),
                new Vector3(gap, lintelH, size.z));

            // Sill ledge, proud of the wall face.
            _frame.AddBox(centre + new Vector3(0f, sillH - 0.09f, size.z * 0.06f),
                new Vector3(gap + 0.3f, 0.14f, size.z + 0.22f));

            // Glass and mullions.
            for (int i = 0; i < columns; i++)
            {
                float offset = -gap * 0.5f + windowW * 0.5f + i * (windowW + 0.9f);

                _glass.AddBox(centre + new Vector3(offset, sillH + windowH * 0.5f, 0f),
                    new Vector3(windowW, windowH, 0.07f));

                if (i < columns - 1)
                {
                    _frame.AddBox(centre + new Vector3(offset + windowW * 0.5f + 0.45f,
                                                       sillH + windowH * 0.5f, 0f),
                        new Vector3(0.16f, windowH, size.z * 0.75f));
                }
            }
        }

        /// <summary>
        /// Emits the merged frame and glass meshes under the given parent.
        /// The frame keeps colliders so the player cannot walk through the wall;
        /// the glass does not, so shots pass through windows.
        /// </summary>
        public static void Flush(Transform parent)
        {
            if (_frame != null && _frame.VertexCount > 0)
            {
                var go = _frame.ToObject("FacadeFrame",
                    PlayableWorld.NewSharedMaterial(new Color(0.46f, 0.45f, 0.43f)),
                    Vector3.zero);
                go.transform.SetParent(parent, false);
            }

            if (_glass != null && _glass.VertexCount > 0)
            {
                var go = _glass.ToObject("FacadeGlass",
                    PlayableWorld.NewSharedMaterial(new Color(0.30f, 0.42f, 0.46f)),
                    Vector3.zero);
                go.transform.SetParent(parent, false);

                foreach (var c in go.GetComponentsInChildren<Collider>())
                {
                    Object.Destroy(c);
                }
            }

            _frame = null;
            _glass = null;
        }
    }
}