using System.Collections.Generic;
using MadVoxel.Core;
using MadVoxel.Farming.Crops;
using UnityEngine;

namespace MadVoxel.Farming.Plots
{
    /// <summary>
    /// The plant on top of a plot. Stage has to be readable at a glance from standing
    /// height - a ripe row should look obviously different from a green one - so the
    /// plant grows in height and gains coloured fruit markers when it is ready.
    /// </summary>
    public class FarmPlotVisuals
    {
        const int StalkCount = 5;

        readonly Transform _root;
        readonly List<GameObject> _plant = new List<GameObject>();
        GameObject _container;

        public FarmPlotVisuals(Transform structureRoot)
        {
            _root = structureRoot;
        }

        public void Show(CropDefinition crop, CropStage stage, float progress01)
        {
            Clear();
            if (crop == null || stage == CropStage.Empty) return;

            if (_container == null)
            {
                _container = new GameObject("Plant");
                _container.transform.SetParent(_root, false);
            }

            float height = Mathf.Max(0.08f, crop.matureHeight * Mathf.Lerp(0.18f, 1f, progress01));

            // Ripe crops go warmer and brighter so a ready row reads across the garden.
            Color tint = stage == CropStage.Ready
                ? Color.Lerp(crop.plantTint, new Color(0.85f, 0.72f, 0.24f), 0.55f)
                : Color.Lerp(crop.plantTint * 0.75f, crop.plantTint, progress01);

            var leaf = MaterialLibrary.Get(SurfaceFamily.Foliage, tint, 0.05f);

            for (int i = 0; i < StalkCount; i++)
            {
                float angle = i / (float)StalkCount * Mathf.PI * 2f;
                float radius = i == 0 ? 0f : 0.24f;
                var offset = new Vector3(0.5f + Mathf.Cos(angle) * radius, 0.26f + height * 0.5f, 0.5f + Mathf.Sin(angle) * radius);

                var stalk = PrimitiveBuilder.Box(_container.transform, offset,
                    new Vector3(0.07f, height, 0.07f), leaf, "Stalk" + i);
                _plant.Add(stalk);
            }

            if (stage == CropStage.Ready)
            {
                var fruit = MaterialLibrary.Get(SurfaceFamily.Foliage, new Color(0.88f, 0.66f, 0.18f), 0.2f);
                for (int i = 0; i < 3; i++)
                {
                    float angle = i / 3f * Mathf.PI * 2f + 0.6f;
                    var offset = new Vector3(0.5f + Mathf.Cos(angle) * 0.2f, 0.26f + height * 0.82f, 0.5f + Mathf.Sin(angle) * 0.2f);
                    _plant.Add(PrimitiveBuilder.Box(_container.transform, offset, Vector3.one * 0.16f, fruit, "Fruit" + i));
                }
            }
        }

        void Clear()
        {
            for (int i = 0; i < _plant.Count; i++)
            {
                if (_plant[i] != null) Object.Destroy(_plant[i]);
            }
            _plant.Clear();
        }
    }
}
