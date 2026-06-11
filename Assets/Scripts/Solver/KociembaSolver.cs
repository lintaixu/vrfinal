using System;
using System.Collections.Generic;
using RubiksCube.Data;
using UnityEngine;

namespace RubiksCube.Solver
{
    public class KociembaSolver : MonoBehaviour
    {
        public static KociembaSolver Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            // Application.persistentDataPath is main-thread only — capture it
            // here so the background solve thread can cache lookup tables.
            Kociemba.CoordCube.CacheDir =
                System.IO.Path.Combine(Application.persistentDataPath, "KociembaTables");
        }

        /// <summary>
        /// Solve a cube state and return move steps.
        /// </summary>
        public bool Solve(CubeState state, out List<MoveStep> moves, out string error)
        {
            moves = new List<MoveStep>();
            error = null;

            if (!state.Validate(out string validationError))
            {
                error = $"Invalid cube state: {validationError}";
                return false;
            }

            string kociembaString = state.ToKociembaString();
            Debug.Log($"[Solver] Kociemba string: {kociembaString}");

            if (kociembaString.Contains('?'))
            {
                error = "Cube string contains unrecognized colors";
                return false;
            }

            try
            {
                // Two-phase Kociemba: ~20-move solutions for any legal state.
                // First call builds lookup tables (slow once, then disk-cached).
                string solution = Kociemba.Search.solution(kociembaString, 24, 15.0);

                if (solution != null && solution.StartsWith("Error"))
                {
                    error = ErrorCodeToMessage(solution);
                    return false;
                }

                // Empty solution means the cube is already solved
                if (string.IsNullOrEmpty(solution))
                {
                    Debug.Log("[Solver] Cube is already solved.");
                    return true;
                }

                Debug.Log($"[Solver] Solution: {solution}");
                moves = ParseMoves(solution.Trim());
                return true;
            }
            catch (Exception e)
            {
                error = $"Solver exception: {e.Message}";
                Debug.LogError($"[Solver] Exception: {e}");
                return false;
            }
        }

        private static string ErrorCodeToMessage(string errorCode)
        {
            return errorCode switch
            {
                "Error 1" => "Each color must appear exactly 9 times",
                "Error 2" => "Impossible edge pieces — check sticker colors",
                "Error 3" => "An edge is flipped — a face was scanned at the wrong angle",
                "Error 4" => "Impossible corner pieces — check sticker colors",
                "Error 5" => "A corner is twisted — a face was scanned at the wrong angle",
                "Error 6" => "Parity error — two faces are likely swapped or rotated",
                "Error 7" => "No solution within move limit — check face orientations",
                "Error 8" => "Solver timed out — please try again",
                _ => $"Solver error: {errorCode}"
            };
        }

        public static List<MoveStep> ParseMoves(string solutionString)
        {
            var steps = new List<MoveStep>();
            if (string.IsNullOrWhiteSpace(solutionString)) return steps;

            string[] tokens = solutionString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                if (token.Length == 0) continue;

                CubeFace face = ParseFace(token[0]);
                int turns = 1;

                if (token.Length > 1)
                {
                    if (token[1] == '\'') turns = -1;
                    else if (token[1] == '2') turns = 2;
                }

                steps.Add(new MoveStep
                {
                    face = face,
                    turns = turns,
                    notation = token
                });
            }

            return steps;
        }

        private static CubeFace ParseFace(char c)
        {
            return c switch
            {
                'U' => CubeFace.U,
                'D' => CubeFace.D,
                'F' => CubeFace.F,
                'B' => CubeFace.B,
                'L' => CubeFace.L,
                'R' => CubeFace.R,
                _ => throw new ArgumentException($"Unknown face symbol: {c}")
            };
        }
    }
}
