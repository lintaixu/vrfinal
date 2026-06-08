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
                error = $"方塊狀態無效: {validationError}";
                return false;
            }

            string kociembaString = state.ToKociembaString();
            Debug.Log($"[Solver] Kociemba string: {kociembaString}");

            if (kociembaString.Contains('?'))
            {
                error = "方塊字串包含無法識別的顏色";
                return false;
            }

            try
            {
                string solution = Kociemba.SearchRuntime.solution(kociembaString, out string solveError);

                if (!string.IsNullOrEmpty(solveError))
                {
                    error = $"求解錯誤: {solveError}";
                    return false;
                }

                if (string.IsNullOrEmpty(solution))
                {
                    error = "求解器未回傳結果";
                    return false;
                }

                Debug.Log($"[Solver] Solution: {solution}");
                moves = ParseMoves(solution.Trim());
                return true;
            }
            catch (Exception e)
            {
                error = $"求解過程發生例外: {e.Message}";
                Debug.LogError($"[Solver] Exception: {e}");
                return false;
            }
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
                _ => throw new ArgumentException($"未知的面符號: {c}")
            };
        }
    }
}
