using System;

namespace Kociemba
{
    /// <summary>
    /// Herbert Kociemba's two-phase algorithm. Finds a solution of ~19-23
    /// moves for any legal cube state, typically in well under a second
    /// once the tables are built.
    /// </summary>
    public class Search
    {
        private static readonly int[] ax = new int[31];  // axis of the move (0..5 = URFDLB)
        private static readonly int[] po = new int[31];  // power of the move (1..3)

        private static readonly int[] flip = new int[31];
        private static readonly int[] twist = new int[31];
        private static readonly int[] slice = new int[31];

        private static readonly int[] parity = new int[31];
        private static readonly int[] URFtoDLF = new int[31];
        private static readonly int[] FRtoBR = new int[31];
        private static readonly int[] URtoUL = new int[31];
        private static readonly int[] UBtoDF = new int[31];
        private static readonly int[] URtoDF = new int[31];

        private static readonly int[] minDistPhase1 = new int[31];
        private static readonly int[] minDistPhase2 = new int[31];

        private static readonly object searchLock = new object();

        private static string SolutionToString(int length)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < length; i++)
            {
                sb.Append("URFDLB"[ax[i]]);
                switch (po[i])
                {
                    case 1: sb.Append(" "); break;
                    case 2: sb.Append("2 "); break;
                    case 3: sb.Append("' "); break;
                }
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Solve a cube given by its 54-facelet string (URFDLB order).
        /// maxDepth: maximum solution length (e.g. 22-24).
        /// timeOutSeconds: give up after this long.
        /// Returns the move sequence, "" if already solved, or "Error N".
        ///
        /// Error 1: each color must appear exactly 9 times
        /// Error 2: not all 12 edges exist exactly once
        /// Error 3: one edge is flipped
        /// Error 4: not all 8 corners exist exactly once
        /// Error 5: one corner is twisted
        /// Error 6: parity error - two corners or two edges swapped
        /// Error 7: no solution within maxDepth
        /// Error 8: timeout
        /// </summary>
        public static string solution(string facelets, int maxDepth, double timeOutSeconds)
        {
            lock (searchLock)
            {
                return SolutionInternal(facelets, maxDepth, timeOutSeconds);
            }
        }

        private static string SolutionInternal(string facelets, int maxDepth, double timeOutSeconds)
        {
            int s;

            // ----- validate the facelet string -----
            int[] count = new int[6];
            try
            {
                for (int i = 0; i < 54; i++)
                    count[(int)(CubeColor)Enum.Parse(typeof(CubeColor), facelets[i].ToString())]++;
            }
            catch
            {
                return "Error 1";
            }
            for (int i = 0; i < 6; i++)
                if (count[i] != 9)
                    return "Error 1";

            var fc = new FaceCube(facelets);
            var cc = fc.ToCubieCube();
            if ((s = cc.Verify()) != 0)
                return "Error " + Math.Abs(s);

            // ----- make sure tables exist -----
            CoordCube.Init();

            // ----- initialize search -----
            var c = new CoordCube(cc);

            po[0] = 0;
            ax[0] = 0;
            flip[0] = c.flip;
            twist[0] = c.twist;
            parity[0] = c.parity;
            slice[0] = c.FRtoBR / 24;
            URFtoDLF[0] = c.URFtoDLF;
            FRtoBR[0] = c.FRtoBR;
            URtoUL[0] = c.URtoUL;
            UBtoDF[0] = c.UBtoDF;

            minDistPhase1[1] = 1; // else failure for depth=1, n=0
            int mv, n = 0;
            bool busy = false;
            int depthPhase1 = 1;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            long timeOutMs = (long)(timeOutSeconds * 1000.0);

            // ----- main IDA* loop (phase 1) -----
            do
            {
                do
                {
                    if ((depthPhase1 - n > minDistPhase1[n + 1]) && !busy)
                    {
                        if (ax[n] == 0 || ax[n] == 3) // initialize next move
                            ax[++n] = 1;
                        else
                            ax[++n] = 0;
                        po[n] = 1;
                    }
                    else if (++po[n] > 3)
                    {
                        do
                        {
                            // increment axis
                            if (++ax[n] > 5)
                            {
                                if (sw.ElapsedMilliseconds > timeOutMs)
                                    return "Error 8";
                                if (n == 0)
                                {
                                    if (depthPhase1 >= maxDepth)
                                        return "Error 7";
                                    depthPhase1++;
                                    ax[n] = 0;
                                    po[n] = 1;
                                    busy = false;
                                    break;
                                }
                                else
                                {
                                    n--;
                                    busy = true;
                                    break;
                                }
                            }
                            else
                            {
                                po[n] = 1;
                                busy = false;
                            }
                        } while (n != 0 && (ax[n - 1] == ax[n] || ax[n - 1] - 3 == ax[n]));
                    }
                    else
                        busy = false;
                } while (busy);

                // compute new coordinates and new minDistPhase1
                mv = 3 * ax[n] + po[n] - 1;
                flip[n + 1] = CoordCube.flipMove[flip[n], mv];
                twist[n + 1] = CoordCube.twistMove[twist[n], mv];
                slice[n + 1] = CoordCube.FRtoBR_Move[slice[n] * 24, mv] / 24;
                minDistPhase1[n + 1] = Math.Max(
                    CoordCube.GetPruning(CoordCube.Slice_Flip_Prun,
                        CoordCube.N_SLICE1 * flip[n + 1] + slice[n + 1]),
                    CoordCube.GetPruning(CoordCube.Slice_Twist_Prun,
                        CoordCube.N_SLICE1 * twist[n + 1] + slice[n + 1]));

                if (minDistPhase1[n + 1] == 0 && n >= depthPhase1 - 5)
                {
                    minDistPhase1[n + 1] = 10; // any value > 5 — avoids re-entering phase 2 here
                    if (n == depthPhase1 - 1 && (s = TotalDepth(depthPhase1, maxDepth)) >= 0)
                    {
                        if (s == depthPhase1
                            || (ax[depthPhase1 - 1] != ax[depthPhase1]
                                && ax[depthPhase1 - 1] != ax[depthPhase1] + 3))
                            return SolutionToString(s);
                    }
                }
            } while (true);
        }

        /// <summary>
        /// Phase 2 search. Returns total solution length, or -1 if no phase 2
        /// solution exists within the remaining depth budget.
        /// </summary>
        private static int TotalDepth(int depthPhase1, int maxDepth)
        {
            int mv, d1, d2;
            int maxDepthPhase2 = Math.Min(10, maxDepth - depthPhase1); // allow up to 10 phase-2 moves

            for (int i = 0; i < depthPhase1; i++)
            {
                mv = 3 * ax[i] + po[i] - 1;
                URFtoDLF[i + 1] = CoordCube.URFtoDLF_Move[URFtoDLF[i], mv];
                FRtoBR[i + 1] = CoordCube.FRtoBR_Move[FRtoBR[i], mv];
                parity[i + 1] = CoordCube.ParityMove((short)parity[i], mv);
            }

            if ((d1 = CoordCube.GetPruning(CoordCube.Slice_URFtoDLF_Parity_Prun,
                (CoordCube.N_SLICE2 * URFtoDLF[depthPhase1] + FRtoBR[depthPhase1] % 24) * 2
                + parity[depthPhase1])) > maxDepthPhase2)
                return -1;

            for (int i = 0; i < depthPhase1; i++)
            {
                mv = 3 * ax[i] + po[i] - 1;
                URtoUL[i + 1] = CoordCube.URtoUL_Move[URtoUL[i], mv];
                UBtoDF[i + 1] = CoordCube.UBtoDF_Move[UBtoDF[i], mv];
            }
            URtoDF[depthPhase1] =
                CoordCube.MergeURtoULandUBtoDF[URtoUL[depthPhase1], UBtoDF[depthPhase1]];

            if ((d2 = CoordCube.GetPruning(CoordCube.Slice_URtoDF_Parity_Prun,
                (CoordCube.N_SLICE2 * URtoDF[depthPhase1] + FRtoBR[depthPhase1] % 24) * 2
                + parity[depthPhase1])) > maxDepthPhase2)
                return -1;

            if ((minDistPhase2[depthPhase1] = Math.Max(d1, d2)) == 0) // already solved
                return depthPhase1;

            // setup phase 2 search
            int depthPhase2 = 1;
            int n = depthPhase1;
            bool busy = false;
            po[depthPhase1] = 0;
            ax[depthPhase1] = 0;
            minDistPhase2[n + 1] = 1; // else failure for depthPhase2=1, n=0

            do
            {
                do
                {
                    if ((depthPhase1 + depthPhase2 - n > minDistPhase2[n + 1]) && !busy)
                    {
                        if (ax[n] == 0 || ax[n] == 3) // initialize next move
                        {
                            ax[++n] = 1;
                            po[n] = 2;
                        }
                        else
                        {
                            ax[++n] = 0;
                            po[n] = 1;
                        }
                    }
                    else if ((ax[n] == 0 || ax[n] == 3) ? (++po[n] > 3) : ((po[n] = po[n] + 2) > 3))
                    {
                        do
                        {
                            // increment axis
                            if (++ax[n] > 5)
                            {
                                if (n == depthPhase1)
                                {
                                    if (depthPhase2 >= maxDepthPhase2)
                                        return -1;
                                    depthPhase2++;
                                    ax[n] = 0;
                                    po[n] = 1;
                                    busy = false;
                                    break;
                                }
                                else
                                {
                                    n--;
                                    busy = true;
                                    break;
                                }
                            }
                            else
                            {
                                po[n] = (ax[n] == 0 || ax[n] == 3) ? 1 : 2;
                                busy = false;
                            }
                        } while (n != depthPhase1
                                 && (ax[n - 1] == ax[n] || ax[n - 1] - 3 == ax[n]));
                    }
                    else
                        busy = false;
                } while (busy);

                // compute new coordinates and new minDist
                mv = 3 * ax[n] + po[n] - 1;

                URFtoDLF[n + 1] = CoordCube.URFtoDLF_Move[URFtoDLF[n], mv];
                FRtoBR[n + 1] = CoordCube.FRtoBR_Move[FRtoBR[n], mv];
                parity[n + 1] = CoordCube.ParityMove((short)parity[n], mv);
                URtoDF[n + 1] = CoordCube.URtoDF_Move[URtoDF[n], mv];

                minDistPhase2[n + 1] = Math.Max(
                    CoordCube.GetPruning(CoordCube.Slice_URtoDF_Parity_Prun,
                        (CoordCube.N_SLICE2 * URtoDF[n + 1] + FRtoBR[n + 1] % 24) * 2 + parity[n + 1]),
                    CoordCube.GetPruning(CoordCube.Slice_URFtoDLF_Parity_Prun,
                        (CoordCube.N_SLICE2 * URFtoDLF[n + 1] + FRtoBR[n + 1] % 24) * 2 + parity[n + 1]));
            } while (minDistPhase2[n + 1] != 0);

            return depthPhase1 + depthPhase2;
        }
    }
}
