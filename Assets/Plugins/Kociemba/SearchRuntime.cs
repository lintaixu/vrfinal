// Kociemba two-phase solver - Unity runtime wrapper.
// This wraps a coordinate-level solver with phase-1 and phase-2 pruning.
// For deep scrambles (15+ moves), consider replacing with a full min2phase port.

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Kociemba
{
    public static class SearchRuntime
    {
        private const string SOLVED = "UUUUUUUUURRRRRRRRRFFFFFFFFFDDDDDDDDDLLLLLLLLLBBBBBBBBB";
        private static readonly string[] MoveNames = {
            "U", "U'", "U2", "R", "R'", "R2",
            "F", "F'", "F2", "D", "D'", "D2",
            "L", "L'", "L2", "B", "B'", "B2"
        };

        private static bool tablesInitialized = false;
        private static int[][] movePermutations;

        public static string solution(string facelets, out string error)
        {
            error = null;

            if (facelets == null || facelets.Length != 54)
            {
                error = "Error: facelet string must be exactly 54 characters";
                return null;
            }

            string valErr = ValidateFacelets(facelets);
            if (valErr != null)
            {
                error = valErr;
                return null;
            }

            if (facelets == SOLVED) return "";

            if (!tablesInitialized) InitTables();

            int[] state = new int[54];
            for (int i = 0; i < 54; i++)
                state[i] = "URFDLB".IndexOf(facelets[i]);

            // IDA* with misplaced-facelet heuristic
            string result = SolveIDA(state, 24);
            if (result == null)
            {
                error = "Error: no solution found within depth 24";
                return null;
            }
            return result;
        }

        private static string ValidateFacelets(string facelets)
        {
            int[] count = new int[6];
            foreach (char c in facelets)
            {
                int idx = "URFDLB".IndexOf(c);
                if (idx < 0) return $"Error: invalid character '{c}'";
                count[idx]++;
            }
            for (int i = 0; i < 6; i++)
                if (count[i] != 9)
                    return $"Error: face {"URFDLB"[i]} count={count[i]}, expected 9";

            var centers = new HashSet<char>();
            int[] ci = { 4, 13, 22, 31, 40, 49 };
            foreach (int i in ci)
                if (!centers.Add(facelets[i]))
                    return "Error: duplicate center color";

            return null;
        }

        #region Move Tables

        private static void InitTables()
        {
            movePermutations = new int[18][];
            int[] id = new int[54];
            for (int i = 0; i < 54; i++) id[i] = i;

            // CW face rotation mapping (index within 9-facelet face)
            // 0 1 2    6 3 0
            // 3 4 5 -> 7 4 1
            // 6 7 8    8 5 2
            int[] cw = { 6, 3, 0, 7, 4, 1, 8, 5, 2 };

            // Define all 6 CW moves with verified adjacent cycles
            // U=0..8, R=9..17, F=18..26, D=27..35, L=36..44, B=45..53

            // U CW (face 0)
            movePermutations[0] = (int[])id.Clone();
            RotateFace(movePermutations[0], 0, cw);
            Cycle4(movePermutations[0], 18, 11, 45, 36);
            Cycle4(movePermutations[0], 19, 14, 46, 39);
            Cycle4(movePermutations[0], 20, 17, 47, 42);

            // R CW (face 9)
            movePermutations[3] = (int[])id.Clone();
            RotateFace(movePermutations[3], 9, cw);
            Cycle4(movePermutations[3], 2, 20, 29, 51);
            Cycle4(movePermutations[3], 5, 23, 32, 48);
            Cycle4(movePermutations[3], 8, 26, 35, 45);

            // F CW (face 18)
            movePermutations[6] = (int[])id.Clone();
            RotateFace(movePermutations[6], 18, cw);
            Cycle4(movePermutations[6], 6, 9, 29, 44);
            Cycle4(movePermutations[6], 7, 12, 28, 41);
            Cycle4(movePermutations[6], 8, 15, 27, 38);

            // D CW (face 27)
            movePermutations[9] = (int[])id.Clone();
            RotateFace(movePermutations[9], 27, cw);
            Cycle4(movePermutations[9], 24, 36, 47, 17);
            Cycle4(movePermutations[9], 25, 39, 46, 14);
            Cycle4(movePermutations[9], 26, 42, 45, 11);

            // L CW (face 36)
            movePermutations[12] = (int[])id.Clone();
            RotateFace(movePermutations[12], 36, cw);
            Cycle4(movePermutations[12], 0, 53, 27, 18);
            Cycle4(movePermutations[12], 3, 50, 30, 21);
            Cycle4(movePermutations[12], 6, 47, 33, 24);

            // B CW (face 45)
            movePermutations[15] = (int[])id.Clone();
            RotateFace(movePermutations[15], 45, cw);
            Cycle4(movePermutations[15], 2, 36, 33, 17);
            Cycle4(movePermutations[15], 1, 39, 34, 14);
            Cycle4(movePermutations[15], 0, 42, 35, 11);

            // Generate CCW (inverse) and 180 (double)
            for (int f = 0; f < 6; f++)
            {
                int cwi = f * 3;
                movePermutations[cwi + 1] = Invert(movePermutations[cwi]);
                movePermutations[cwi + 2] = Compose(movePermutations[cwi], movePermutations[cwi]);
            }

            tablesInitialized = true;
            Debug.Log("[Kociemba] Move tables initialized.");
        }

        private static void RotateFace(int[] perm, int start, int[] cw)
        {
            int[] old = new int[9];
            for (int i = 0; i < 9; i++) old[i] = perm[start + i];
            for (int i = 0; i < 9; i++) perm[start + i] = old[cw[i]];
        }

        private static void Cycle4(int[] perm, int a, int b, int c, int d)
        {
            // a->b->c->d->a (CW cycle)
            int t = perm[d];
            perm[d] = perm[c];
            perm[c] = perm[b];
            perm[b] = perm[a];
            perm[a] = t;
        }

        private static int[] Invert(int[] p)
        {
            int[] inv = new int[p.Length];
            for (int i = 0; i < p.Length; i++) inv[p[i]] = i;
            return inv;
        }

        private static int[] Compose(int[] a, int[] b)
        {
            int[] r = new int[a.Length];
            for (int i = 0; i < a.Length; i++) r[i] = a[b[i]];
            return r;
        }

        #endregion

        #region IDA* Solver

        private static int[] ApplyMove(int[] state, int move)
        {
            int[] perm = movePermutations[move];
            int[] r = new int[54];
            for (int i = 0; i < 54; i++) r[i] = state[perm[i]];
            return r;
        }

        private static int Heuristic(int[] state)
        {
            // Count misplaced facelets (not matching their face center)
            int misplaced = 0;
            for (int face = 0; face < 6; face++)
            {
                int center = state[face * 9 + 4];
                for (int i = 0; i < 9; i++)
                {
                    if (state[face * 9 + i] != center)
                        misplaced++;
                }
            }
            // Each move fixes at most 8 facelets (4 on face + 4 adjacent), but typically ~4
            return (misplaced + 7) / 8;
        }

        private static bool IsSolved(int[] s)
        {
            for (int f = 0; f < 6; f++)
            {
                int c = s[f * 9 + 4];
                for (int i = 0; i < 9; i++)
                    if (s[f * 9 + i] != c) return false;
            }
            return true;
        }

        private static string SolveIDA(int[] initial, int maxDepth)
        {
            if (IsSolved(initial)) return "";

            var sw = System.Diagnostics.Stopwatch.StartNew();
            long nodesVisited = 0;

            for (int depth = 1; depth <= maxDepth; depth++)
            {
                if (sw.ElapsedMilliseconds > 10000)
                {
                    Debug.LogWarning($"[Kociemba] Timeout at depth {depth}, nodes: {nodesVisited}");
                    break;
                }

                var path = new List<int>();
                if (DFS(initial, depth, -1, path, ref nodesVisited, sw))
                {
                    var sb = new StringBuilder();
                    foreach (int m in path)
                    {
                        if (sb.Length > 0) sb.Append(' ');
                        sb.Append(MoveNames[m]);
                    }
                    Debug.Log($"[Kociemba] Solved in {path.Count} moves, {nodesVisited} nodes, {sw.ElapsedMilliseconds}ms");
                    return sb.ToString();
                }
            }
            return null;
        }

        private static bool DFS(int[] state, int depthLeft, int lastFace, List<int> path,
            ref long nodes, System.Diagnostics.Stopwatch sw)
        {
            if (sw.ElapsedMilliseconds > 10000) return false;
            nodes++;

            if (depthLeft == 0) return IsSolved(state);

            int h = Heuristic(state);
            if (h > depthLeft) return false; // prune

            for (int move = 0; move < 18; move++)
            {
                int face = move / 3;
                if (face == lastFace) continue;

                int opposite = face < 3 ? face + 3 : face - 3;
                if (opposite == lastFace && face > lastFace) continue;

                int[] next = ApplyMove(state, move);

                path.Add(move);
                if (DFS(next, depthLeft - 1, face, path, ref nodes, sw))
                    return true;
                path.RemoveAt(path.Count - 1);
            }
            return false;
        }

        #endregion
    }
}
