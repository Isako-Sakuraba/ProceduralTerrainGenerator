using System;
using UnityEngine;

namespace TerrainGeneration
{
    [Serializable]
    public sealed class ElevationSettings
    {
        [Serializable]
        public struct Point
        {
            [SerializeField]
            private ElevationType _type;

            [SerializeField, Range(0f, 1f)]
            private float _elevation;

            public ElevationType Type => _type;
            public float Elevation => _elevation;

            public Point(ElevationType type, float elevation)
            {
                _type = type;
                _elevation = Mathf.Clamp01(elevation);
            }
        }

        [SerializeField]
        private Point[] _points =
        {
            new Point(ElevationType.DeepOcean, 0f),
            new Point(ElevationType.ShallowWater, 0.2f),
            new Point(ElevationType.Shore, 0.32f),
            new Point(ElevationType.Lowland, 0.38f),
            new Point(ElevationType.Highland, 0.58f),
            new Point(ElevationType.MountainBase, 0.72f),
            new Point(ElevationType.Mountain, 0.82f),
            new Point(ElevationType.MountainPeak, 0.93f)
        };

        public ReadOnlySpan<Point> Points => _points;

        public ElevationType Evaluate(float elevation)
        {
            elevation = Mathf.Clamp01(elevation);

            if (_points == null || _points.Length == 0)
                return default;

            ElevationType result = _points[0].Type;
            float bestElevation = float.NegativeInfinity;

            for (int i = 0; i < _points.Length; i++)
            {
                Point point = _points[i];
                float pointElevation = Mathf.Clamp01(point.Elevation);

                if (pointElevation > elevation || pointElevation < bestElevation)
                    continue;

                bestElevation = pointElevation;
                result = point.Type;
            }

            return result;
        }
    }
}
