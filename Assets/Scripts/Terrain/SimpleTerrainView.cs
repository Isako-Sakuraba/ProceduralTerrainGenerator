using NaughtyAttributes;
using System;
using UnityEngine;

namespace TerrainGeneration
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class SimpleTerrainView : MonoBehaviour
    {
        /// <summary>
        /// Chooses which terrain values are shown in the preview.
        /// </summary>
        public enum ViewMode
        {
            Combined,
            Temperature,
            Humidity,
            Elevation
        }

        /// <summary>
        /// Chooses how terrain values are converted into colors.
        /// </summary>
        public enum ColorScheme
        {
            Monochrome,
            Colored
        }

        [Header("References")]
        [SerializeField] private TerrainGenerator _terrain;

        [Header("View Settings")]
        [SerializeField] private ViewMode _viewMode = ViewMode.Combined;
        [SerializeField] private ColorScheme _colorScheme = ColorScheme.Colored;

        [Header("Noise Settings")]
        [SerializeField] private bool _remapNoiseFromSignedRange;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _propertyBlock;

        private Texture2D _texture;
        private Color32[] _pixels;

        public Texture2D Texture => _texture;

        public ViewMode Mode
        {
            get => _viewMode;
            set
            {
                if (_viewMode == value)
                    return;

                _viewMode = value;
                Refresh();
            }
        }

        public ColorScheme Scheme
        {
            get => _colorScheme;
            set
            {
                if (_colorScheme == value)
                    return;

                _colorScheme = value;
                Refresh();
            }
        }

        /// <summary>
        /// Finds the components used by the terrain view.
        /// </summary>
        private void Awake()
        {
            EnsureReferences();
        }

        /// <summary>
        /// Finds any missing component references.
        /// </summary>
        private void EnsureReferences()
        {
            _renderer = GetComponent<MeshRenderer>();

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Starts listening for terrain generation and refreshes the view.
        /// </summary>
        private void OnEnable()
        {
            EnsureReferences();

            if (_terrain == null)
                return;

            _terrain.Generated += OnTerrainGenerated;

            Refresh();
        }

        /// <summary>
        /// Stops listening for terrain generation.
        /// </summary>
        private void OnDisable()
        {
            if (_terrain != null)
                _terrain.Generated -= OnTerrainGenerated;
        }

        /// <summary>
        /// Cleans up the generated preview texture.
        /// </summary>
        private void OnDestroy()
        {
            if (_texture != null)
                DestroyGeneratedObject(_texture);
        }

        /// <summary>
        /// Refreshes the terrain view after editor changes.
        /// </summary>
        private void OnValidate()
        {
            EnsureReferences();

            if (isActiveAndEnabled)
                Refresh();
        }

        /// <summary>
        /// Refreshes the view when the terrain is generated.
        /// </summary>
        private void OnTerrainGenerated(TerrainGenerator terrain)
        {
            Refresh();
        }

        /// <summary>
        /// Rebuilds the preview texture from the terrain points.
        /// </summary>
        [Button]
        public void Refresh()
        {
            EnsureReferences();

            if (_terrain == null)
                return;

            ReadOnlySpan<TerrainPoint> points = _terrain.Points;

            Vector2Int size = _terrain.Definition.Size;

            if (size.x <= 0 || size.y <= 0)
                return;

            if (points.Length < size.x * size.y)
                return;

            EnsureTexture(size);

            for (int i = 0; i < size.x * size.y; i++)
            {
                ref readonly TerrainPoint point = ref points[i];

                float temperature = Normalize(point.Temperature);
                float humidity = Normalize(point.Humidity);
                float elevation = Normalize(point.Elevation);

                _pixels[i] = EvaluateColor(
                    temperature,
                    humidity,
                    elevation);
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);

            ApplyTexture();
        }

        /// <summary>
        /// Creates a preview texture with the requested size when needed.
        /// </summary>
        private void EnsureTexture(Vector2Int size)
        {
            int count = size.x * size.y;

            if (_pixels == null || _pixels.Length != count)
                _pixels = new Color32[count];

            if (_texture != null &&
                _texture.width == size.x &&
                _texture.height == size.y)
            {
                return;
            }

            if (_texture != null)
                DestroyGeneratedObject(_texture);

            _texture = new Texture2D(
                size.x,
                size.y,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "Terrain Preview",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        /// <summary>
        /// Converts the terrain values into a preview color.
        /// </summary>
        private Color32 EvaluateColor(
            float temperature,
            float humidity,
            float elevation)
        {
            if (_colorScheme == ColorScheme.Monochrome)
            {
                float value = _viewMode switch
                {
                    ViewMode.Temperature => temperature,
                    ViewMode.Humidity => humidity,
                    ViewMode.Elevation => elevation,

                    _ => (temperature + humidity + elevation) / 3f
                };

                byte intensity = ToByte(value);

                return new Color32(
                    intensity,
                    intensity,
                    intensity,
                    255);
            }

            return _viewMode switch
            {
                ViewMode.Temperature => new Color32(
                    ToByte(temperature), 0, 0, 255),

                ViewMode.Humidity => new Color32(
                    0, ToByte(humidity), 0, 255),

                ViewMode.Elevation => new Color32(
                    0, 0, ToByte(elevation), 255),

                _ => new Color32(
                    ToByte(temperature),
                    ToByte(humidity),
                    ToByte(elevation),
                    255)
            };
        }

        /// <summary>
        /// Converts a noise value into the zero-to-one range.
        /// </summary>
        private float Normalize(float value)
        {
            if (_remapNoiseFromSignedRange)
                value = value * 0.5f + 0.5f;

            return Mathf.Clamp01(value);
        }

        /// <summary>
        /// Converts a normalized value into a color byte.
        /// </summary>
        private static byte ToByte(float value)
        {
            return (byte)Mathf.RoundToInt(value * 255f);
        }

        /// <summary>
        /// Applies the preview texture to the renderer.
        /// </summary>
        private void ApplyTexture()
        {
            if (_renderer == null)
                return;

            _renderer.GetPropertyBlock(_propertyBlock);

            _propertyBlock.SetTexture(BaseMapId, _texture);
            _propertyBlock.SetTexture(MainTexId, _texture);

            _renderer.SetPropertyBlock(_propertyBlock);
        }

        /// <summary>
        /// Destroys a generated object safely in play mode or edit mode.
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
