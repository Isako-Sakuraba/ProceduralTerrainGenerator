using System;
using UnityEngine;

namespace TerrainGeneration
{
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class SimpleTerrainView : MonoBehaviour
    {
        public enum ViewMode
        {
            Combined,
            Temperature,
            Humidity,
            Elevation
        }

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

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (_terrain == null)
                return;

            _terrain.Generated += OnTerrainGenerated;

            Refresh();
        }

        private void OnDisable()
        {
            if (_terrain != null)
                _terrain.Generated -= OnTerrainGenerated;
        }

        private void OnDestroy()
        {
            if (_texture != null)
                Destroy(_texture);
        }

        private void OnValidate()
        {
            if (Application.isPlaying && isActiveAndEnabled)
                Refresh();
        }

        private void OnTerrainGenerated(TerrainGenerator terrain)
        {
            Refresh();
        }

        public void Refresh()
        {
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
                Destroy(_texture);

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

        private float Normalize(float value)
        {
            if (_remapNoiseFromSignedRange)
                value = value * 0.5f + 0.5f;

            return Mathf.Clamp01(value);
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.RoundToInt(value * 255f);
        }

        private void ApplyTexture()
        {
            if (_renderer == null)
                return;

            _renderer.GetPropertyBlock(_propertyBlock);

            _propertyBlock.SetTexture(BaseMapId, _texture);
            _propertyBlock.SetTexture(MainTexId, _texture);

            _renderer.SetPropertyBlock(_propertyBlock);
        }
    }
}