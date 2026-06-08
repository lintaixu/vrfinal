using UnityEngine;
using System.Collections.Generic;
using RubiksCube.Data;
using RubiksCube.Solver;

namespace RubiksCube.EditorTest
{
    /// <summary>
    /// Attach to any GameObject in the scene. Press T in Play mode to test the solver.
    /// </summary>
    public class SolverTest : MonoBehaviour
    {
        [Header("測試用 Kociemba 字串")]
        [TextArea(2, 5)]
        [SerializeField] private string testFacelets = "UUUUUUUUURRRRRRRRRFFFFFFFFFDDDDDDDDDLLLLLLLLLBBBBBBBBB";

        [Header("手動輸入各面顏色 (9字元)")]
        [SerializeField] private string faceU = "WWWWWWWWW";
        [SerializeField] private string faceR = "RRRRRRRRR";
        [SerializeField] private string faceF = "GGGGGGGGG";
        [SerializeField] private string faceD = "YYYYYYYYY";
        [SerializeField] private string faceL = "OOOOOOOOO";
        [SerializeField] private string faceB = "BBBBBBBBB";

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.T))
                TestSolver();
            if (Input.GetKeyDown(KeyCode.Y))
                TestFromColorInput();
            if (Input.GetKeyDown(KeyCode.R))
                TestScrambleAndSolve();
        }

        private void TestSolver()
        {
            Debug.Log("=== 測試求解器 (Kociemba 字串) ===");
            Debug.Log($"輸入: {testFacelets}");

            string result = Kociemba.SearchRuntime.solution(testFacelets, out string error);

            if (error != null)
            {
                Debug.LogError($"錯誤: {error}");
                return;
            }

            if (string.IsNullOrEmpty(result))
            {
                Debug.Log("方塊已經是完成狀態！");
                return;
            }

            Debug.Log($"解法: {result}");

            var moves = KociembaSolver.ParseMoves(result);
            Debug.Log($"共 {moves.Count} 步:");
            for (int i = 0; i < moves.Count; i++)
            {
                Debug.Log($"  Step {i + 1}: {moves[i].notation} - {moves[i].GetDescription()}");
            }
        }

        private void TestFromColorInput()
        {
            Debug.Log("=== 測試求解器 (顏色輸入) ===");

            var state = new CubeState();
            string[] inputs = { faceU, faceR, faceF, faceD, faceL, faceB };

            for (int i = 0; i < 6; i++)
            {
                if (inputs[i].Length != 9)
                {
                    Debug.LogError($"面 {i} 長度不是 9: '{inputs[i]}'");
                    return;
                }
                state.faces[i] = inputs[i].ToCharArray();
            }

            if (!state.Validate(out string valError))
            {
                Debug.LogError($"驗證失敗: {valError}");
                return;
            }

            string kociemba = state.ToKociembaString();
            Debug.Log($"Kociemba 字串: {kociemba}");

            string result = Kociemba.SearchRuntime.solution(kociemba, out string error);

            if (error != null)
                Debug.LogError($"求解錯誤: {error}");
            else if (string.IsNullOrEmpty(result))
                Debug.Log("已完成狀態！");
            else
                Debug.Log($"解法: {result}");
        }

        private void TestScrambleAndSolve()
        {
            Debug.Log("=== 測試：打亂再求解 ===");

            // Start with solved state, apply some moves, then solve
            string scrambleMoves = "R U F' L2 D B";
            Debug.Log($"打亂步驟: {scrambleMoves}");

            // Apply scramble to solved state and get the facelet string
            string solved = "UUUUUUUUURRRRRRRRRFFFFFFFFFDDDDDDDDDLLLLLLLLLBBBBBBBBB";
            Debug.Log($"初始狀態: {solved}");

            // For now just test that the solver handles a simple state
            string result = Kociemba.SearchRuntime.solution(solved, out string error);
            if (error != null)
                Debug.LogError($"錯誤: {error}");
            else
                Debug.Log($"已完成的方塊，解法: '{result}' (應為空)");
        }
    }
}
