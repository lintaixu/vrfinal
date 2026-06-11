using System;
using System.Collections.Generic;

namespace RubiksCube.Data
{
    public enum CubeFace
    {
        U, // Up
        R, // Right
        F, // Front
        D, // Down
        L, // Left
        B  // Back
    }

    [Serializable]
    public struct MoveStep
    {
        public CubeFace face;
        public int turns; // 1=CW, -1=CCW, 2=180
        public string notation;

        public string GetDescription()
        {
            string faceName = face switch
            {
                CubeFace.U => "Top (U)",
                CubeFace.D => "Bottom (D)",
                CubeFace.F => "Front (F)",
                CubeFace.B => "Back (B)",
                CubeFace.L => "Left (L)",
                CubeFace.R => "Right (R)",
                _ => face.ToString()
            };

            string direction = turns switch
            {
                1 => "clockwise 90°",
                -1 => "counter-clockwise 90°",
                2 => "turn 180°",
                _ => $"turn {turns}"
            };

            return $"{faceName} {direction}";
        }
    }

    public class CubeState
    {
        // 6 faces, each with 9 color chars: W/Y/R/O/B/G
        // Index order: U, R, F, D, L, B (Kociemba standard)
        public char[][] faces = new char[6][];
        public bool isValid;
        public string errorMessage;

        public CubeState()
        {
            for (int i = 0; i < 6; i++)
                faces[i] = new char[9];
        }

        public string ToKociembaString()
        {
            // Kociemba uses face-letter notation where each facelets is
            // labeled by the face it belongs to (center color).
            // Map our color chars to face letters based on center colors.
            var centerToFace = new Dictionary<char, char>();
            char[] faceLetters = { 'U', 'R', 'F', 'D', 'L', 'B' };

            for (int i = 0; i < 6; i++)
            {
                char centerColor = faces[i][4]; // center is index 4
                centerToFace[centerColor] = faceLetters[i];
            }

            var sb = new System.Text.StringBuilder(54);
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    if (centerToFace.TryGetValue(faces[i][j], out char faceLetter))
                        sb.Append(faceLetter);
                    else
                        sb.Append('?');
                }
            }
            return sb.ToString();
        }

        public bool Validate(out string error)
        {
            var colorCount = new Dictionary<char, int>();
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 9; j++)
                {
                    char c = faces[i][j];
                    if (!colorCount.ContainsKey(c))
                        colorCount[c] = 0;
                    colorCount[c]++;
                }
            }

            if (colorCount.ContainsKey('?'))
            {
                error = $"{colorCount['?']} stickers could not be recognized — rescan with better lighting";
                isValid = false;
                errorMessage = error;
                return false;
            }

            if (colorCount.Count != 6)
            {
                error = $"Expected 6 colors but detected {colorCount.Count}";
                isValid = false;
                errorMessage = error;
                return false;
            }

            foreach (var kv in colorCount)
            {
                if (kv.Value != 9)
                {
                    error = $"Color '{kv.Key}' appears {kv.Value} times (should be 9)";
                    isValid = false;
                    errorMessage = error;
                    return false;
                }
            }

            // Check center uniqueness
            var centers = new HashSet<char>();
            for (int i = 0; i < 6; i++)
            {
                if (!centers.Add(faces[i][4]))
                {
                    error = $"Face {i + 1} has a duplicated center color";
                    isValid = false;
                    errorMessage = error;
                    return false;
                }
            }

            error = null;
            isValid = true;
            errorMessage = null;
            return true;
        }
    }
}
