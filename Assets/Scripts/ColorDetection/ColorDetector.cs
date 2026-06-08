using UnityEngine;
using System;

namespace RubiksCube.ColorDetection
{
    [Serializable]
    public struct ColorThreshold
    {
        public string name;
        public char symbol;
        public float hMin, hMax;
        public float sMin, sMax;
        public float vMin, vMax;
        public Color previewColor;
    }

    public class ColorDetector : MonoBehaviour
    {
        [Header("閾值設定 (在 Inspector 中即時調整)")]
        [SerializeField] private ColorThreshold[] thresholds = new ColorThreshold[]
        {
            new ColorThreshold { name = "白 White", symbol = 'W',
                hMin = 0, hMax = 360, sMin = 0f, sMax = 0.15f, vMin = 0.85f, vMax = 1f,
                previewColor = Color.white },
            new ColorThreshold { name = "黃 Yellow", symbol = 'Y',
                hMin = 40, hMax = 70, sMin = 0.4f, sMax = 1f, vMin = 0.6f, vMax = 1f,
                previewColor = Color.yellow },
            new ColorThreshold { name = "紅 Red", symbol = 'R',
                hMin = 350, hMax = 10, sMin = 0.5f, sMax = 1f, vMin = 0.3f, vMax = 1f,
                previewColor = Color.red },
            new ColorThreshold { name = "橙 Orange", symbol = 'O',
                hMin = 10, hMax = 40, sMin = 0.5f, sMax = 1f, vMin = 0.5f, vMax = 1f,
                previewColor = new Color(1f, 0.5f, 0f) },
            new ColorThreshold { name = "藍 Blue", symbol = 'B',
                hMin = 200, hMax = 260, sMin = 0.3f, sMax = 1f, vMin = 0.2f, vMax = 1f,
                previewColor = Color.blue },
            new ColorThreshold { name = "綠 Green", symbol = 'G',
                hMin = 100, hMax = 170, sMin = 0.3f, sMax = 1f, vMin = 0.2f, vMax = 1f,
                previewColor = Color.green },
        };

        [Header("取樣設定")]
        [SerializeField] private int sampleSize = 20;

        public ColorThreshold[] Thresholds => thresholds;

        /// <summary>
        /// Analyze a captured face texture and return 9 color chars.
        /// The texture should be cropped to just the cube face area.
        /// </summary>
        public char[] AnalyzeFace(Texture2D faceTexture, RectInt gridRegion)
        {
            char[] result = new char[9];
            int cellWidth = gridRegion.width / 3;
            int cellHeight = gridRegion.height / 3;

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    int centerX = gridRegion.x + col * cellWidth + cellWidth / 2;
                    int centerY = gridRegion.y + row * cellHeight + cellHeight / 2;

                    Color avgColor = GetAverageColor(faceTexture, centerX, centerY, sampleSize);
                    result[row * 3 + col] = ClassifyColor(avgColor);
                }
            }

            return result;
        }

        /// <summary>
        /// Analyze full-screen texture using a predefined grid overlay rect.
        /// </summary>
        public char[] AnalyzeFromScreenCapture(Texture2D screenshot, Rect normalizedGridRect)
        {
            int px = Mathf.RoundToInt(normalizedGridRect.x * screenshot.width);
            int py = Mathf.RoundToInt(normalizedGridRect.y * screenshot.height);
            int pw = Mathf.RoundToInt(normalizedGridRect.width * screenshot.width);
            int ph = Mathf.RoundToInt(normalizedGridRect.height * screenshot.height);

            var gridRegion = new RectInt(px, py, pw, ph);
            return AnalyzeFace(screenshot, gridRegion);
        }

        public Color GetAverageColor(Texture2D tex, int centerX, int centerY, int size)
        {
            int halfSize = size / 2;
            float r = 0, g = 0, b = 0;
            int count = 0;

            for (int y = centerY - halfSize; y < centerY + halfSize; y++)
            {
                for (int x = centerX - halfSize; x < centerX + halfSize; x++)
                {
                    if (x < 0 || x >= tex.width || y < 0 || y >= tex.height)
                        continue;
                    Color pixel = tex.GetPixel(x, y);
                    r += pixel.r;
                    g += pixel.g;
                    b += pixel.b;
                    count++;
                }
            }

            if (count == 0) return Color.black;
            return new Color(r / count, g / count, b / count);
        }

        public char ClassifyColor(Color rgb)
        {
            Color.RGBToHSV(rgb, out float h, out float s, out float v);
            h *= 360f; // Unity returns 0-1, convert to 0-360

            // White check first (low saturation, high value)
            if (s <= thresholds[0].sMax && v >= thresholds[0].vMin)
                return 'W';

            float bestDistance = float.MaxValue;
            char bestMatch = '?';

            for (int i = 1; i < thresholds.Length; i++)
            {
                var t = thresholds[i];
                if (!IsInRange(h, s, v, t)) continue;

                float dist = ColorDistance(h, s, v, t);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestMatch = t.symbol;
                }
            }

            return bestMatch;
        }

        private bool IsInRange(float h, float s, float v, ColorThreshold t)
        {
            if (s < t.sMin || s > t.sMax) return false;
            if (v < t.vMin || v > t.vMax) return false;

            // Handle hue wrap-around (e.g., red: 350-10)
            if (t.hMin > t.hMax)
                return h >= t.hMin || h <= t.hMax;
            else
                return h >= t.hMin && h <= t.hMax;
        }

        private float ColorDistance(float h, float s, float v, ColorThreshold t)
        {
            float hCenter = (t.hMin + t.hMax) / 2f;
            if (t.hMin > t.hMax)
                hCenter = ((t.hMin + t.hMax + 360f) / 2f) % 360f;

            float hDist = Mathf.Min(Mathf.Abs(h - hCenter), 360f - Mathf.Abs(h - hCenter));
            float sDist = Mathf.Abs(s - (t.sMin + t.sMax) / 2f);
            float vDist = Mathf.Abs(v - (t.vMin + t.vMax) / 2f);

            return hDist / 180f + sDist + vDist;
        }

        public Color GetPreviewColor(char symbol)
        {
            foreach (var t in thresholds)
            {
                if (t.symbol == symbol) return t.previewColor;
            }
            return Color.gray;
        }
    }
}
