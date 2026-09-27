using UnityEditor;
using UnityEngine;

namespace TerrainGeneration.Editor
{
    [CustomPropertyDrawer(typeof(ElevationSettings))]
    public sealed class ElevationSettingsDrawer : PropertyDrawer
    {
        private const float SliderHeight = 18f;
        private const float MarkerWidth = 10f;
        private const float MarkerHeight = 22f;
        private const float RowHeight = 20f;
        private const float Padding = 4f;

        private static readonly Color[] Colors =
        {
            new Color(0.02f, 0.08f, 0.30f),
            new Color(0.04f, 0.26f, 0.62f),
            new Color(0.85f, 0.72f, 0.42f),
            new Color(0.28f, 0.58f, 0.22f),
            new Color(0.36f, 0.50f, 0.24f),
            new Color(0.42f, 0.36f, 0.30f),
            new Color(0.48f, 0.46f, 0.44f),
            new Color(0.90f, 0.90f, 0.88f)
        };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty points = property.FindPropertyRelative("_points");
            int count = points != null ? points.arraySize : 0;

            return EditorGUIUtility.singleLineHeight + Padding + MarkerHeight + Padding + SliderHeight + Padding + count * RowHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty points = property.FindPropertyRelative("_points");

            Rect labelRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, label);

            if (points == null || points.arraySize == 0)
                return;

            Rect sliderRect = new Rect(
                position.x,
                labelRect.yMax + Padding + MarkerHeight * 0.5f,
                position.width,
                SliderHeight);

            DrawRanges(sliderRect, points);
            DrawTicks(sliderRect);

            for (int i = 0; i < points.arraySize; i++)
                DrawMarker(sliderRect, points.GetArrayElementAtIndex(i), i);

            Rect rowsRect = new Rect(
                position.x,
                sliderRect.yMax + Padding,
                position.width,
                points.arraySize * RowHeight);

            DrawRows(rowsRect, points);
        }

        private static void DrawRanges(Rect sliderRect, SerializedProperty points)
        {
            EditorGUI.DrawRect(sliderRect, new Color(0.18f, 0.18f, 0.18f));

            for (int i = 0; i < points.arraySize; i++)
            {
                SerializedProperty point = points.GetArrayElementAtIndex(i);
                SerializedProperty elevation = point.FindPropertyRelative("_elevation");

                float start = Mathf.Clamp01(elevation.floatValue);
                float end = 1f;

                for (int j = 0; j < points.arraySize; j++)
                {
                    if (i == j)
                        continue;

                    float candidate = Mathf.Clamp01(points.GetArrayElementAtIndex(j).FindPropertyRelative("_elevation").floatValue);

                    if (candidate > start && candidate < end)
                        end = candidate;
                }

                Rect rangeRect = new Rect(
                    Mathf.Lerp(sliderRect.xMin, sliderRect.xMax, start),
                    sliderRect.y,
                    Mathf.Max(1f, sliderRect.width * (end - start)),
                    sliderRect.height);

                EditorGUI.DrawRect(rangeRect, GetColor(point.FindPropertyRelative("_type").enumValueIndex));
            }

            GUI.Box(sliderRect, GUIContent.none);
        }

        private static void DrawTicks(Rect sliderRect)
        {
            GUIStyle style = EditorStyles.miniLabel;

            EditorGUI.LabelField(new Rect(sliderRect.xMin, sliderRect.yMax, 32f, RowHeight), "0", style);
            EditorGUI.LabelField(new Rect(sliderRect.xMax - 20f, sliderRect.yMax, 20f, RowHeight), "1", style);
        }

        private static void DrawMarker(Rect sliderRect, SerializedProperty point, int index)
        {
            SerializedProperty type = point.FindPropertyRelative("_type");
            SerializedProperty elevation = point.FindPropertyRelative("_elevation");

            float value = Mathf.Clamp01(elevation.floatValue);
            float x = Mathf.Lerp(sliderRect.xMin, sliderRect.xMax, value);
            Rect markerRect = new Rect(
                x - MarkerWidth * 0.5f,
                sliderRect.y - MarkerHeight * 0.5f,
                MarkerWidth,
                MarkerHeight);

            int controlId = GUIUtility.GetControlID(FocusType.Passive, markerRect);
            Event current = Event.current;

            switch (current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (markerRect.Contains(current.mousePosition) && current.button == 0)
                    {
                        GUIUtility.hotControl = controlId;
                        current.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        elevation.floatValue = Mathf.Clamp01(Mathf.InverseLerp(sliderRect.xMin, sliderRect.xMax, current.mousePosition.x));
                        current.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        current.Use();
                    }
                    break;

                case EventType.Repaint:
                    EditorGUI.DrawRect(markerRect, Color.black);
                    EditorGUI.DrawRect(new Rect(markerRect.x + 1f, markerRect.y + 1f, markerRect.width - 2f, markerRect.height - 2f), GetColor(type.enumValueIndex));
                    break;
            }

            Rect nameRect = new Rect(
                markerRect.x - 44f,
                markerRect.y - RowHeight,
                96f,
                RowHeight);
            EditorGUI.LabelField(nameRect, type.enumDisplayNames[type.enumValueIndex], EditorStyles.miniBoldLabel);
        }

        private static void DrawRows(Rect rowsRect, SerializedProperty points)
        {
            for (int i = 0; i < points.arraySize; i++)
            {
                SerializedProperty point = points.GetArrayElementAtIndex(i);
                SerializedProperty type = point.FindPropertyRelative("_type");
                SerializedProperty elevation = point.FindPropertyRelative("_elevation");

                Rect rowRect = new Rect(rowsRect.x, rowsRect.y + i * RowHeight, rowsRect.width, RowHeight);
                Rect swatchRect = new Rect(rowRect.x, rowRect.y + 4f, 12f, 12f);
                Rect typeRect = new Rect(rowRect.x + 18f, rowRect.y, rowRect.width * 0.45f, RowHeight);
                Rect valueRect = new Rect(typeRect.xMax + Padding, rowRect.y + 1f, rowRect.width - typeRect.width - 18f - Padding, EditorGUIUtility.singleLineHeight);

                EditorGUI.DrawRect(swatchRect, GetColor(type.enumValueIndex));
                EditorGUI.LabelField(typeRect, type.enumDisplayNames[type.enumValueIndex]);
                elevation.floatValue = EditorGUI.Slider(valueRect, elevation.floatValue, 0f, 1f);
            }
        }

        private static Color GetColor(int index)
        {
            if (index < 0 || index >= Colors.Length)
                return Color.magenta;

            return Colors[index];
        }
    }
}
