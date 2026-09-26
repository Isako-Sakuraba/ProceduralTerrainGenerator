using System;
using UnityEngine;

namespace TerrainGeneration
{
    [Serializable]
    public class NoiseLayer
    {
        [field: SerializeField]
        public bool Active { get; private set; } = true;

        [field: SerializeField]
        public FastNoiseLite.NoiseType NoiseType { get; private set; }

        [field: SerializeField]
        public Vector2 Scale { get; private set; } = Vector2.one;

        [field: SerializeField]
        public Vector2 Offset { get; private set; } = Vector2.zero;
    }
}