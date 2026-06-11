using UnityEngine;
using System;

namespace RubiksCube.ColorDetection
{
    /// <summary>
    /// Detects cube sticker colors by voting over ALL pixels in each grid cell.
    /// Works with ring-style stickers (color only on the outline) because dark
    /// transparent centers are simply ignored — only saturated pixels vote.
    /// </summary>
    public class ColorDetector : MonoBehaviour
    {
        [Header("Sampling")]
        [Tooltip("Sample every Nth pixel inside a cell (higher = faster)")]
        [SerializeField] private int pixelStep = 3;

        [Header("Colored pixel filter")]
        [SerializeField] private float minColoredSaturation = 0.30f;
        [SerializeField] private float minColoredValue = 0.25f;

        [Header("White pixel filter")]
        [SerializeField] private float whiteMaxSaturation = 0.25f;
        [SerializeField] private float whiteMinValue = 0.60f;

        [Tooltip("A color must win at least this fraction of sampled pixels")]
        [SerializeField] private float minVoteFraction = 0.04f;

        /// <summary>
        /// Analyze a face region and return 9 color chars (row-major, top-left first).
        /// </summary>
        public char[] AnalyzeFace(Texture2D faceTexture, RectInt gridRegion)
        {
            char[] result = new char[9];
            int cellW = gridRegion.width / 3;
            int cellH = gridRegion.height / 3;

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    // Texture Y axis points up, but facelet order reads top-left
                    // first — so row 0 must sample the TOP band of the region.
                    var cell = new RectInt(
                        gridRegion.x + col * cellW,
                        gridRegion.y + (2 - row) * cellH,
                        cellW, cellH);
                    result[row * 3 + col] = ClassifyCell(faceTexture, cell);
                }
            }

            return result;
        }

        /// <summary>
        /// Vote over all pixels in the cell. Dark/transparent pixels are ignored,
        /// saturated pixels vote by hue, bright low-saturation pixels vote White.
        /// </summary>
        public char ClassifyCell(Texture2D tex, RectInt cell)
        {
            int step = Mathf.Max(1, pixelStep);
            int whiteVotes = 0;
            int totalSampled = 0;
            // Votes: Y, R, O, B, G
            int[] votes = new int[5];

            for (int y = cell.yMin; y < cell.yMax; y += step)
            {
                for (int x = cell.xMin; x < cell.xMax; x += step)
                {
                    if (x < 0 || y < 0 || x >= tex.width || y >= tex.height) continue;

                    Color p = tex.GetPixel(x, y);
                    Color.RGBToHSV(p, out float h, out float s, out float v);
                    totalSampled++;

                    // White: bright and unsaturated
                    if (s <= whiteMaxSaturation && v >= whiteMinValue)
                    {
                        whiteVotes++;
                        continue;
                    }

                    // Skip dark / transparent / desaturated pixels (cube body, gaps)
                    if (s < minColoredSaturation || v < minColoredValue) continue;

                    h *= 360f;
                    switch (HueToSymbol(h))
                    {
                        case 'Y': votes[0]++; break;
                        case 'R': votes[1]++; break;
                        case 'O': votes[2]++; break;
                        case 'B': votes[3]++; break;
                        case 'G': votes[4]++; break;
                    }
                }
            }

            if (totalSampled == 0) return '?';

            int minVotes = Mathf.Max(8, (int)(totalSampled * minVoteFraction));

            char best = 'W';
            int bestVotes = whiteVotes;
            char[] symbols = { 'Y', 'R', 'O', 'B', 'G' };
            for (int i = 0; i < votes.Length; i++)
            {
                if (votes[i] > bestVotes)
                {
                    bestVotes = votes[i];
                    best = symbols[i];
                }
            }

            return bestVotes >= minVotes ? best : '?';
        }

        /// <summary>
        /// Map hue (0-360) to a cube color symbol.
        /// Pink/magenta (290-360) counts as Red for cubes with pink stickers.
        /// </summary>
        private static char HueToSymbol(float h)
        {
            if (h < 15f) return 'R';
            if (h < 45f) return 'O';
            if (h < 75f) return 'Y';
            if (h < 170f) return 'G';
            if (h < 270f) return 'B';
            return 'R'; // 270-360: magenta/pink/red
        }

        /// <summary>
        /// Average RGB color around a point (kept for debug logging).
        /// </summary>
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

        public Color GetPreviewColor(char symbol)
        {
            switch (symbol)
            {
                case 'W': return Color.white;
                case 'Y': return Color.yellow;
                case 'R': return new Color(1f, 0.2f, 0.4f); // pink-red
                case 'O': return new Color(1f, 0.5f, 0f);
                case 'B': return new Color(0.2f, 0.4f, 1f);
                case 'G': return new Color(0.2f, 0.9f, 0.3f);
                default: return Color.gray;
            }
        }
    }
}
