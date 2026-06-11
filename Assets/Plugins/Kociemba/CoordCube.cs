using System;
using System.IO;

namespace Kociemba
{
    /// <summary>
    /// Cube on the coordinate level + move/pruning tables for the two-phase
    /// algorithm. Tables are generated on first use (a few seconds on a phone)
    /// and cached to disk when CacheDir is set, so later launches load fast.
    /// </summary>
    public class CoordCube
    {
        public const short N_TWIST = 2187;   // 3^7 corner orientations
        public const short N_FLIP = 2048;    // 2^11 edge orientations
        public const short N_SLICE1 = 495;   // 12 choose 4 UD-slice edge positions
        public const short N_SLICE2 = 24;    // 4! UD-slice edge permutations (phase 2)
        public const short N_PARITY = 2;
        public const short N_URFtoDLF = 20160; // 8!/2! corner permutations
        public const short N_FRtoBR = 11880;   // 12*11*10*9 slice edge permutations
        public const short N_URtoUL = 1320;
        public const short N_UBtoDF = 1320;
        public const int N_URtoDF = 20160;
        public const int N_MOVE = 18;

        // Optional directory for table caching (set from the main thread)
        public static string CacheDir;

        public short twist;
        public short flip;
        public short parity;
        public short FRtoBR;
        public short URFtoDLF;
        public short URtoUL;
        public short UBtoDF;
        public int URtoDF;

        public CoordCube(CubieCube c)
        {
            twist = c.GetTwist();
            flip = c.GetFlip();
            parity = c.CornerParity();
            FRtoBR = c.GetFRtoBR();
            URFtoDLF = c.GetURFtoDLF();
            URtoUL = c.GetURtoUL();
            UBtoDF = c.GetUBtoDF();
            URtoDF = c.GetURtoDF(); // only needed in phase 2
        }

        // ******************************** Move tables ********************************

        public static short[,] twistMove;
        public static short[,] flipMove;
        public static short[,] FRtoBR_Move;
        public static short[,] URFtoDLF_Move;
        public static short[,] URtoDF_Move;
        public static short[,] URtoUL_Move;
        public static short[,] UBtoDF_Move;
        public static short[,] MergeURtoULandUBtoDF;

        // ******************************* Pruning tables *******************************

        public static sbyte[] Slice_URFtoDLF_Parity_Prun;
        public static sbyte[] Slice_URtoDF_Parity_Prun;
        public static sbyte[] Slice_Twist_Prun;
        public static sbyte[] Slice_Flip_Prun;

        public static readonly short[] parityMove =
        {
            1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1
        };
        // parityMove is indexed [parity, move] conceptually; flattened below
        private static readonly short[,] parityMove2 =
        {
            { 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1 },
            { 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0 }
        };

        private static bool initialized;
        private static readonly object initLock = new object();

        public static void Init()
        {
            if (initialized) return;
            lock (initLock)
            {
                if (initialized) return;
                BuildTables();
                initialized = true;
            }
        }

        public static short ParityMove(short parity, int move) => parityMove2[parity, move];

        private static void BuildTables()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            if (TryLoadAll())
            {
                UnityEngine.Debug.Log($"[Kociemba] Tables loaded from cache in {sw.ElapsedMilliseconds} ms");
                return;
            }

            // ---- twistMove ----
            twistMove = new short[N_TWIST, N_MOVE];
            var a = new CubieCube();
            for (short i = 0; i < N_TWIST; i++)
            {
                a.SetTwist(i);
                for (int j = 0; j < 6; j++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        a.CornerMultiply(CubieCube.moveCube[j]);
                        twistMove[i, 3 * j + k] = a.GetTwist();
                    }
                    a.CornerMultiply(CubieCube.moveCube[j]); // restore
                }
            }

            // ---- flipMove ----
            flipMove = new short[N_FLIP, N_MOVE];
            a = new CubieCube();
            for (short i = 0; i < N_FLIP; i++)
            {
                a.SetFlip(i);
                for (int j = 0; j < 6; j++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        a.EdgeMultiply(CubieCube.moveCube[j]);
                        flipMove[i, 3 * j + k] = a.GetFlip();
                    }
                    a.EdgeMultiply(CubieCube.moveCube[j]);
                }
            }

            // ---- FRtoBR_Move ----
            FRtoBR_Move = new short[N_FRtoBR, N_MOVE];
            a = new CubieCube();
            for (short i = 0; i < N_FRtoBR; i++)
            {
                a.SetFRtoBR(i);
                for (int j = 0; j < 6; j++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        a.EdgeMultiply(CubieCube.moveCube[j]);
                        FRtoBR_Move[i, 3 * j + k] = a.GetFRtoBR();
                    }
                    a.EdgeMultiply(CubieCube.moveCube[j]);
                }
            }

            // ---- URFtoDLF_Move ----
            URFtoDLF_Move = new short[N_URFtoDLF, N_MOVE];
            a = new CubieCube();
            for (short i = 0; i < N_URFtoDLF; i++)
            {
                a.SetURFtoDLF(i);
                for (int j = 0; j < 6; j++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        a.CornerMultiply(CubieCube.moveCube[j]);
                        URFtoDLF_Move[i, 3 * j + k] = a.GetURFtoDLF();
                    }
                    a.CornerMultiply(CubieCube.moveCube[j]);
                }
            }

            // ---- URtoDF_Move ----
            URtoDF_Move = new short[N_URtoDF, N_MOVE];
            a = new CubieCube();
            for (int i = 0; i < N_URtoDF; i++)
            {
                a.SetURtoDF(i);
                for (int j = 0; j < 6; j++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        a.EdgeMultiply(CubieCube.moveCube[j]);
                        URtoDF_Move[i, 3 * j + k] = (short)a.GetURtoDF();
                        // only phase 2 moves are relevant, but full table is built
                    }
                    a.EdgeMultiply(CubieCube.moveCube[j]);
                }
            }

            // ---- URtoUL_Move ----
            URtoUL_Move = new short[N_URtoUL, N_MOVE];
            a = new CubieCube();
            for (short i = 0; i < N_URtoUL; i++)
            {
                a.SetURtoUL(i);
                for (int j = 0; j < 6; j++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        a.EdgeMultiply(CubieCube.moveCube[j]);
                        URtoUL_Move[i, 3 * j + k] = a.GetURtoUL();
                    }
                    a.EdgeMultiply(CubieCube.moveCube[j]);
                }
            }

            // ---- UBtoDF_Move ----
            UBtoDF_Move = new short[N_UBtoDF, N_MOVE];
            a = new CubieCube();
            for (short i = 0; i < N_UBtoDF; i++)
            {
                a.SetUBtoDF(i);
                for (int j = 0; j < 6; j++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        a.EdgeMultiply(CubieCube.moveCube[j]);
                        UBtoDF_Move[i, 3 * j + k] = a.GetUBtoDF();
                    }
                    a.EdgeMultiply(CubieCube.moveCube[j]);
                }
            }

            // ---- MergeURtoULandUBtoDF ----
            MergeURtoULandUBtoDF = new short[336, 336];
            for (short uRtoUL = 0; uRtoUL < 336; uRtoUL++)
            {
                for (short uBtoDF = 0; uBtoDF < 336; uBtoDF++)
                {
                    MergeURtoULandUBtoDF[uRtoUL, uBtoDF] =
                        (short)CubieCube.GetURtoDF(uRtoUL, uBtoDF);
                }
            }

            // ---- Slice_URFtoDLF_Parity_Prun ----
            Slice_URFtoDLF_Parity_Prun = new sbyte[N_SLICE2 * N_URFtoDLF * N_PARITY / 2];
            for (int i = 0; i < N_SLICE2 * N_URFtoDLF * N_PARITY / 2; i++)
                Slice_URFtoDLF_Parity_Prun[i] = -1;
            {
                int depth = 0;
                SetPruning(Slice_URFtoDLF_Parity_Prun, 0, 0);
                int done = 1;
                while (done != N_SLICE2 * N_URFtoDLF * N_PARITY)
                {
                    for (int i = 0; i < N_SLICE2 * N_URFtoDLF * N_PARITY; i++)
                    {
                        int parity = i % 2;
                        int URFtoDLF = (i / 2) / N_SLICE2;
                        int slice = (i / 2) % N_SLICE2;
                        if (GetPruning(Slice_URFtoDLF_Parity_Prun, i) == depth)
                        {
                            for (int j = 0; j < 18; j++)
                            {
                                switch (j)
                                {
                                    case 3: case 5: case 6: case 8:
                                    case 12: case 14: case 15: case 17:
                                        continue;
                                    default:
                                        int newSlice = FRtoBR_Move[slice * 24, j] / 24;
                                        int newURFtoDLF = URFtoDLF_Move[URFtoDLF, j];
                                        int newParity = parityMove2[parity, j];
                                        if (GetPruning(Slice_URFtoDLF_Parity_Prun,
                                            (N_SLICE2 * newURFtoDLF + newSlice) * 2 + newParity) == 0x0f)
                                        {
                                            SetPruning(Slice_URFtoDLF_Parity_Prun,
                                                (N_SLICE2 * newURFtoDLF + newSlice) * 2 + newParity,
                                                (sbyte)(depth + 1));
                                            done++;
                                        }
                                        break;
                                }
                            }
                        }
                    }
                    depth++;
                }
            }

            // ---- Slice_URtoDF_Parity_Prun ----
            Slice_URtoDF_Parity_Prun = new sbyte[N_SLICE2 * N_URtoDF * N_PARITY / 2];
            for (int i = 0; i < N_SLICE2 * N_URtoDF * N_PARITY / 2; i++)
                Slice_URtoDF_Parity_Prun[i] = -1;
            {
                int depth = 0;
                SetPruning(Slice_URtoDF_Parity_Prun, 0, 0);
                int done = 1;
                while (done != N_SLICE2 * N_URtoDF * N_PARITY)
                {
                    for (int i = 0; i < N_SLICE2 * N_URtoDF * N_PARITY; i++)
                    {
                        int parity = i % 2;
                        int URtoDF = (i / 2) / N_SLICE2;
                        int slice = (i / 2) % N_SLICE2;
                        if (GetPruning(Slice_URtoDF_Parity_Prun, i) == depth)
                        {
                            for (int j = 0; j < 18; j++)
                            {
                                switch (j)
                                {
                                    case 3: case 5: case 6: case 8:
                                    case 12: case 14: case 15: case 17:
                                        continue;
                                    default:
                                        int newSlice = FRtoBR_Move[slice * 24, j] / 24;
                                        int newURtoDF = URtoDF_Move[URtoDF, j];
                                        int newParity = parityMove2[parity, j];
                                        if (GetPruning(Slice_URtoDF_Parity_Prun,
                                            (N_SLICE2 * newURtoDF + newSlice) * 2 + newParity) == 0x0f)
                                        {
                                            SetPruning(Slice_URtoDF_Parity_Prun,
                                                (N_SLICE2 * newURtoDF + newSlice) * 2 + newParity,
                                                (sbyte)(depth + 1));
                                            done++;
                                        }
                                        break;
                                }
                            }
                        }
                    }
                    depth++;
                }
            }

            // ---- Slice_Twist_Prun ----
            Slice_Twist_Prun = new sbyte[N_SLICE1 * N_TWIST / 2 + 1];
            for (int i = 0; i < N_SLICE1 * N_TWIST / 2 + 1; i++)
                Slice_Twist_Prun[i] = -1;
            {
                int depth = 0;
                SetPruning(Slice_Twist_Prun, 0, 0);
                int done = 1;
                while (done != N_SLICE1 * N_TWIST)
                {
                    for (int i = 0; i < N_SLICE1 * N_TWIST; i++)
                    {
                        int twist = i / N_SLICE1, slice = i % N_SLICE1;
                        if (GetPruning(Slice_Twist_Prun, i) == depth)
                        {
                            for (int j = 0; j < 18; j++)
                            {
                                int newSlice = FRtoBR_Move[slice * 24, j] / 24;
                                int newTwist = twistMove[twist, j];
                                if (GetPruning(Slice_Twist_Prun, N_SLICE1 * newTwist + newSlice) == 0x0f)
                                {
                                    SetPruning(Slice_Twist_Prun, N_SLICE1 * newTwist + newSlice, (sbyte)(depth + 1));
                                    done++;
                                }
                            }
                        }
                    }
                    depth++;
                }
            }

            // ---- Slice_Flip_Prun ----
            Slice_Flip_Prun = new sbyte[N_SLICE1 * N_FLIP / 2];
            for (int i = 0; i < N_SLICE1 * N_FLIP / 2; i++)
                Slice_Flip_Prun[i] = -1;
            {
                int depth = 0;
                SetPruning(Slice_Flip_Prun, 0, 0);
                int done = 1;
                while (done != N_SLICE1 * N_FLIP)
                {
                    for (int i = 0; i < N_SLICE1 * N_FLIP; i++)
                    {
                        int flip = i / N_SLICE1, slice = i % N_SLICE1;
                        if (GetPruning(Slice_Flip_Prun, i) == depth)
                        {
                            for (int j = 0; j < 18; j++)
                            {
                                int newSlice = FRtoBR_Move[slice * 24, j] / 24;
                                int newFlip = flipMove[flip, j];
                                if (GetPruning(Slice_Flip_Prun, N_SLICE1 * newFlip + newSlice) == 0x0f)
                                {
                                    SetPruning(Slice_Flip_Prun, N_SLICE1 * newFlip + newSlice, (sbyte)(depth + 1));
                                    done++;
                                }
                            }
                        }
                    }
                    depth++;
                }
            }

            UnityEngine.Debug.Log($"[Kociemba] Tables generated in {sw.ElapsedMilliseconds} ms");
            TrySaveAll();
        }

        public static void SetPruning(sbyte[] table, int index, sbyte value)
        {
            if ((index & 1) == 0)
                table[index / 2] &= (sbyte)(0xf0 | value);
            else
                table[index / 2] &= unchecked((sbyte)(0x0f | (value << 4)));
        }

        public static sbyte GetPruning(sbyte[] table, int index)
        {
            if ((index & 1) == 0)
                return (sbyte)(table[index / 2] & 0x0f);
            else
                return (sbyte)((table[index / 2] >> 4) & 0x0f);
        }

        // ******************************* Disk caching *******************************

        private const int CacheVersion = 1;

        private static string CachePath(string name) =>
            Path.Combine(CacheDir, $"kociemba_v{CacheVersion}_{name}.bin");

        private static bool TryLoadAll()
        {
            if (string.IsNullOrEmpty(CacheDir)) return false;
            try
            {
                twistMove = Load2D("twistMove", N_TWIST, N_MOVE);
                flipMove = Load2D("flipMove", N_FLIP, N_MOVE);
                FRtoBR_Move = Load2D("FRtoBR", N_FRtoBR, N_MOVE);
                URFtoDLF_Move = Load2D("URFtoDLF", N_URFtoDLF, N_MOVE);
                URtoDF_Move = Load2D("URtoDF", N_URtoDF, N_MOVE);
                URtoUL_Move = Load2D("URtoUL", N_URtoUL, N_MOVE);
                UBtoDF_Move = Load2D("UBtoDF", N_UBtoDF, N_MOVE);
                MergeURtoULandUBtoDF = Load2D("Merge", 336, 336);
                Slice_URFtoDLF_Parity_Prun = LoadBytes("PrunCorn", N_SLICE2 * N_URFtoDLF * N_PARITY / 2);
                Slice_URtoDF_Parity_Prun = LoadBytes("PrunEdge", N_SLICE2 * N_URtoDF * N_PARITY / 2);
                Slice_Twist_Prun = LoadBytes("PrunTwist", N_SLICE1 * N_TWIST / 2 + 1);
                Slice_Flip_Prun = LoadBytes("PrunFlip", N_SLICE1 * N_FLIP / 2);
                return twistMove != null && flipMove != null && FRtoBR_Move != null
                    && URFtoDLF_Move != null && URtoDF_Move != null && URtoUL_Move != null
                    && UBtoDF_Move != null && MergeURtoULandUBtoDF != null
                    && Slice_URFtoDLF_Parity_Prun != null && Slice_URtoDF_Parity_Prun != null
                    && Slice_Twist_Prun != null && Slice_Flip_Prun != null;
            }
            catch
            {
                return false;
            }
        }

        private static void TrySaveAll()
        {
            if (string.IsNullOrEmpty(CacheDir)) return;
            try
            {
                Directory.CreateDirectory(CacheDir);
                Save2D("twistMove", twistMove);
                Save2D("flipMove", flipMove);
                Save2D("FRtoBR", FRtoBR_Move);
                Save2D("URFtoDLF", URFtoDLF_Move);
                Save2D("URtoDF", URtoDF_Move);
                Save2D("URtoUL", URtoUL_Move);
                Save2D("UBtoDF", UBtoDF_Move);
                Save2D("Merge", MergeURtoULandUBtoDF);
                SaveBytes("PrunCorn", Slice_URFtoDLF_Parity_Prun);
                SaveBytes("PrunEdge", Slice_URtoDF_Parity_Prun);
                SaveBytes("PrunTwist", Slice_Twist_Prun);
                SaveBytes("PrunFlip", Slice_Flip_Prun);
                UnityEngine.Debug.Log("[Kociemba] Tables cached to disk");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[Kociemba] Could not cache tables: {e.Message}");
            }
        }

        private static short[,] Load2D(string name, int d0, int d1)
        {
            string path = CachePath(name);
            if (!File.Exists(path)) return null;
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length != d0 * d1 * 2) return null;
            var table = new short[d0, d1];
            Buffer.BlockCopy(bytes, 0, table, 0, bytes.Length);
            return table;
        }

        private static void Save2D(string name, short[,] table)
        {
            byte[] bytes = new byte[table.GetLength(0) * table.GetLength(1) * 2];
            Buffer.BlockCopy(table, 0, bytes, 0, bytes.Length);
            File.WriteAllBytes(CachePath(name), bytes);
        }

        private static sbyte[] LoadBytes(string name, int length)
        {
            string path = CachePath(name);
            if (!File.Exists(path)) return null;
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length != length) return null;
            var table = new sbyte[length];
            Buffer.BlockCopy(bytes, 0, table, 0, length);
            return table;
        }

        private static void SaveBytes(string name, sbyte[] table)
        {
            byte[] bytes = new byte[table.Length];
            Buffer.BlockCopy(table, 0, bytes, 0, table.Length);
            File.WriteAllBytes(CachePath(name), bytes);
        }
    }
}
