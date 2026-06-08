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
                CubeFace.U => "上面 (Up)",
                CubeFace.D => "下面 (Down)",
                CubeFace.F => "前面 (Front)",
                CubeFace.B => "後面 (Back)",
                CubeFace.L => "左面 (Left)",
                CubeFace.R => "右面 (Right)",
                _ => face.ToString()
            };

            string direction = turns switch
            {
                1 => "順時針 90°",
                -1 => "逆時針 90°",
                2 => "旋轉 180°",
                _ => $"旋轉 {turns}"
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

            if (colorCount.Count != 6)
            {
                error = $"應有 6 種顏色，但偵測到 {colorCount.Count} 種";
                isValid = false;
                errorMessage = error;
                return false;
            }

            foreach (var kv in colorCount)
            {
                if (kv.Value != 9)
                {
                    error = $"顏色 '{kv.Key}' 出現 {kv.Value} 次（應為 9 次）";
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
                    error = $"第 {i + 1} 面的中心色重複";
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
