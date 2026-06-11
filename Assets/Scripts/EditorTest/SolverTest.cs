using UnityEngine;
using System.Collections.Generic;
using RubiksCube.Data;
using RubiksCube.Solver;

namespace RubiksCube.EditorTest
{
    /// <summary>
    /// Attach to any GameObject in the scene. In Play mode:
    ///   T = solve the facelet string below
    ///   Y = solve from the 6 color strings below
    ///   R = scramble a solved cube with real moves, then solve it
    /// </summary>
    public class SolverTest : MonoBehaviour
    {
        [Header("Kociemba facelet string to solve (T)")]
        [TextArea(2, 5)]
        [SerializeField] private string testFacelets = "UUUUUUUUURRRRRRRRRFFFFFFFFFDDDDDDDDDLLLLLLLLLBBBBBBBBB";

        [Header("Face colors, 9 chars each (Y)")]
        [SerializeField] private string faceU = "WWWWWWWWW";
        [SerializeField] private string faceR = "RRRRRRRRR";
        [SerializeField] private string faceF = "GGGGGGGGG";
        [SerializeField] private string faceD = "YYYYYYYYY";
        [SerializeField] private string faceL = "OOOOOOOOO";
        [SerializeField] private string faceB = "BBBBBBBBB";

        [Header("Scramble used by the R test")]
        [SerializeField] private string scramble = "R U F' L2 D B R2 U' F D2 L B'";

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.T))
                TestSolver(testFacelets);
            if (Input.GetKeyDown(KeyCode.Y))
                TestFromColorInput();
            if (Input.GetKeyDown(KeyCode.R))
                TestScrambleAndSolve();
        }

        private void TestSolver(string facelets)
        {
            Debug.Log($"=== Solver test ===\nInput: {facelets}");

            float t0 = Time.realtimeSinceStartup;
            string result = Kociemba.Search.solution(facelets, 24, 15.0);
            float dt = Time.realtimeSinceStartup - t0;

            if (result != null && result.StartsWith("Error"))
            {
                Debug.LogError($"Solver failed: {result}");
                return;
            }

            if (string.IsNullOrEmpty(result))
            {
                Debug.Log($"Cube is already solved! ({dt:F2}s)");
                return;
            }

            var moves = KociembaSolver.ParseMoves(result);
            Debug.Log($"Solution ({moves.Count} moves, {dt:F2}s): {result}");
        }

        private void TestFromColorInput()
        {
            Debug.Log("=== Solver test (color input) ===");

            var state = new CubeState();
            string[] inputs = { faceU, faceR, faceF, faceD, faceL, faceB };

            for (int i = 0; i < 6; i++)
            {
                if (inputs[i].Length != 9)
                {
                    Debug.LogError($"Face {i} length is not 9: '{inputs[i]}'");
                    return;
                }
                state.faces[i] = inputs[i].ToCharArray();
            }

            if (!state.Validate(out string valError))
            {
                Debug.LogError($"Validation failed: {valError}");
                return;
            }

            string kociemba = state.ToKociembaString();
            Debug.Log($"Kociemba string: {kociemba}");
            TestSolver(kociemba);
        }

        private void TestScrambleAndSolve()
        {
            Debug.Log($"=== Scramble & solve test ===\nScramble: {scramble}");

            string facelets = ApplyScramble(scramble);
            if (facelets == null) return;

            Debug.Log($"Scrambled state: {facelets}");
            TestSolver(facelets);
        }

        /// <summary>
        /// Apply a scramble in standard notation to a solved cube and return
        /// the resulting facelet string — generates guaranteed-legal states.
        /// </summary>
        private static string ApplyScramble(string scrambleMoves)
        {
            var cc = new Kociemba.CubieCube();
            foreach (string token in scrambleMoves.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                int axis = "URFDLB".IndexOf(token[0]);
                if (axis < 0)
                {
                    Debug.LogError($"Bad scramble token: {token}");
                    return null;
                }
                int power = 1;
                if (token.Length > 1)
                {
                    if (token[1] == '2') power = 2;
                    else if (token[1] == '\'') power = 3;
                }
                for (int p = 0; p < power; p++)
                    cc.Multiply(Kociemba.CubieCube.moveCube[axis]);
            }
            return cc.ToFaceCube().ToFaceletString();
        }
    }
}
