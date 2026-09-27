using System.Collections.Generic;
using UnityEngine;

namespace TerrainGeneration
{
    [ExecuteAlways]
    public sealed class TerrainSurfaceGenerator : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private SurfaceDefinition _definition;

        private readonly Dictionary<Texture2D, int> _textureIds = new Dictionary<Texture2D, int>();
        private readonly List<Texture2D> _textures = new List<Texture2D>();

        private int[] _surfaceIds;
        private Texture2DArray _textureArray;

        public SurfaceDefinition Definition => _definition;
        public int[] SurfaceIds => _surfaceIds;
        public Texture2DArray TextureArray => _textureArray;

        /// <summary>
        /// Releases generated textures after inspector changes.
        /// </summary>
        private void OnValidate()
        {
            Release();
        }

        /// <summary>
        /// Generates surface IDs for the terrain points.
        /// </summary>
        public void Generate(System.ReadOnlySpan<TerrainPoint> points)
        {
            if (_definition == null || !_definition.HasBiomes())
            {
                Debug.LogWarning("SurfaceDefinition is missing or empty.", this);
                return;
            }

            EnsureSurfaceIds(points.Length);
            EnsureTextureArray();

            Generate(points, _surfaceIds);
        }

        /// <summary>
        /// Writes surface IDs for the terrain points into a buffer.
        /// </summary>
        public void Generate(System.ReadOnlySpan<TerrainPoint> points, int[] output)
        {
            if (_definition == null || !_definition.HasBiomes())
                throw new System.InvalidOperationException("SurfaceDefinition is missing or empty.");
            if (output == null || output.Length < points.Length)
                throw new System.ArgumentException("The surface output buffer is too small.", nameof(output));

            EnsureTextureArray();

            for (int i = 0; i < points.Length; i++)
            {
                TerrainPoint point = points[i];
                BiomeDefinition biome = FindBiome(point);

                if (biome == null || biome.TextureDefinition == null)
                {
                    output[i] = 0;
                    continue;
                }

                ElevationType elevation = _definition.GetElevationType(point.Elevation);
                Texture2D texture = biome.TextureDefinition.GetTexture(elevation);

                output[i] = GetTextureId(texture);
            }
        }

        /// <summary>
        /// Creates the surface texture array when needed.
        /// </summary>
        public void EnsureTextureArray()
        {
            if (_textureArray != null)
                return;

            CollectTextures();
            BuildTextureArray();
        }

        /// <summary>
        /// Releases the generated texture array.
        /// </summary>
        public void Release()
        {
            if (_textureArray != null)
            {
                DestroyGeneratedObject(_textureArray);
                _textureArray = null;
            }
        }

        /// <summary>
        /// Resizes the reusable surface ID buffer when needed.
        /// </summary>
        private void EnsureSurfaceIds(int count)
        {
            if (_surfaceIds == null || _surfaceIds.Length != count)
                _surfaceIds = new int[count];
        }

        /// <summary>
        /// Collects the unique textures used by all biomes.
        /// </summary>
        private void CollectTextures()
        {
            _textures.Clear();
            _textureIds.Clear();

            foreach (BiomeDefinition biome in _definition.Biomes)
            {
                if (biome == null || biome.TextureDefinition == null)
                    continue;

                foreach (Texture2D texture in biome.TextureDefinition)
                {
                    if (texture == null || _textureIds.ContainsKey(texture))
                        continue;

                    _textureIds.Add(texture, _textures.Count);
                    _textures.Add(texture);
                }
            }
        }

        /// <summary>
        /// Builds a texture array from the collected textures.
        /// </summary>
        private void BuildTextureArray()
        {
            if (_textures.Count == 0)
            {
                if (_textureArray != null)
                {
                    DestroyGeneratedObject(_textureArray);
                    _textureArray = null;
                }

                return;
            }

            Texture2D first = _textures[0];

            if (_textureArray != null)
                DestroyGeneratedObject(_textureArray);

            _textureArray = new Texture2DArray(first.width, first.height, _textures.Count, first.format, first.mipmapCount > 1, false)
            {
                name = "Terrain Surface Textures",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            for (int i = 0; i < _textures.Count; i++)
            {
                Texture2D texture = _textures[i];

                if (texture.width != first.width || texture.height != first.height || texture.format != first.format)
                {
                    Debug.LogWarning($"Texture {texture.name} does not match the first surface texture. Using the first texture for this slot.", this);
                    Graphics.CopyTexture(first, 0, _textureArray, i);
                    continue;
                }

                Graphics.CopyTexture(texture, 0, _textureArray, i);
            }

            _textureArray.Apply(false, false);
        }

        /// <summary>
        /// Finds the biome that best matches a terrain point.
        /// </summary>
        private BiomeDefinition FindBiome(TerrainPoint point)
        {
            BiomeDefinition bestBiome = null;
            float bestScore = -1f;

            foreach (BiomeDefinition biome in _definition.Biomes)
            {
                if (biome == null || biome.TextureDefinition == null)
                    continue;

                float score = biome.Evaluate(point.Temperature, point.Humidity, point.Elevation);

                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestBiome = biome;
            }

            return bestBiome;
        }

        /// <summary>
        /// Gets the array index for a surface texture.
        /// </summary>
        private int GetTextureId(Texture2D texture)
        {
            if (texture == null)
                return 0;

            if (_textureIds.TryGetValue(texture, out int id))
                return id;

            return 0;
        }

        /// <summary>
        /// Destroys a generated Unity object in the current mode.
        /// </summary>
        private static void DestroyGeneratedObject(UnityEngine.Object generatedObject)
        {
            if (Application.isPlaying)
                Destroy(generatedObject);
            else
                DestroyImmediate(generatedObject);
        }

    }
}
