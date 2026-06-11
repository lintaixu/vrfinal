using System;

namespace Kociemba
{
    public enum Corner { URF, UFL, ULB, UBR, DFR, DLF, DBL, DRB }
    public enum Edge { UR, UF, UL, UB, DR, DF, DL, DB, FR, FL, BL, BR }

    /// <summary>
    /// Cube on the cubie level: corner/edge permutations and orientations.
    /// Standard Herbert Kociemba two-phase representation.
    /// </summary>
    public class CubieCube
    {
        // Corner permutation
        public Corner[] cp = { Corner.URF, Corner.UFL, Corner.ULB, Corner.UBR, Corner.DFR, Corner.DLF, Corner.DBL, Corner.DRB };
        // Corner orientation (0..2)
        public sbyte[] co = { 0, 0, 0, 0, 0, 0, 0, 0 };
        // Edge permutation
        public Edge[] ep = { Edge.UR, Edge.UF, Edge.UL, Edge.UB, Edge.DR, Edge.DF, Edge.DL, Edge.DB, Edge.FR, Edge.FL, Edge.BL, Edge.BR };
        // Edge orientation (0..1)
        public sbyte[] eo = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        // ************************** Moves on the cubie level ***************************

        private static readonly Corner[] cpU = { Corner.UBR, Corner.URF, Corner.UFL, Corner.ULB, Corner.DFR, Corner.DLF, Corner.DBL, Corner.DRB };
        private static readonly sbyte[] coU = { 0, 0, 0, 0, 0, 0, 0, 0 };
        private static readonly Edge[] epU = { Edge.UB, Edge.UR, Edge.UF, Edge.UL, Edge.DR, Edge.DF, Edge.DL, Edge.DB, Edge.FR, Edge.FL, Edge.BL, Edge.BR };
        private static readonly sbyte[] eoU = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        private static readonly Corner[] cpR = { Corner.DFR, Corner.UFL, Corner.ULB, Corner.URF, Corner.DRB, Corner.DLF, Corner.DBL, Corner.UBR };
        private static readonly sbyte[] coR = { 2, 0, 0, 1, 1, 0, 0, 2 };
        private static readonly Edge[] epR = { Edge.FR, Edge.UF, Edge.UL, Edge.UB, Edge.BR, Edge.DF, Edge.DL, Edge.DB, Edge.DR, Edge.FL, Edge.BL, Edge.UR };
        private static readonly sbyte[] eoR = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        private static readonly Corner[] cpF = { Corner.UFL, Corner.DLF, Corner.ULB, Corner.UBR, Corner.URF, Corner.DFR, Corner.DBL, Corner.DRB };
        private static readonly sbyte[] coF = { 1, 2, 0, 0, 2, 1, 0, 0 };
        private static readonly Edge[] epF = { Edge.UR, Edge.FL, Edge.UL, Edge.UB, Edge.DR, Edge.FR, Edge.DL, Edge.DB, Edge.UF, Edge.DF, Edge.BL, Edge.BR };
        private static readonly sbyte[] eoF = { 0, 1, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0 };

        private static readonly Corner[] cpD = { Corner.URF, Corner.UFL, Corner.ULB, Corner.UBR, Corner.DLF, Corner.DBL, Corner.DRB, Corner.DFR };
        private static readonly sbyte[] coD = { 0, 0, 0, 0, 0, 0, 0, 0 };
        private static readonly Edge[] epD = { Edge.UR, Edge.UF, Edge.UL, Edge.UB, Edge.DF, Edge.DL, Edge.DB, Edge.DR, Edge.FR, Edge.FL, Edge.BL, Edge.BR };
        private static readonly sbyte[] eoD = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        private static readonly Corner[] cpL = { Corner.URF, Corner.ULB, Corner.DBL, Corner.UBR, Corner.DFR, Corner.UFL, Corner.DLF, Corner.DRB };
        private static readonly sbyte[] coL = { 0, 1, 2, 0, 0, 2, 1, 0 };
        private static readonly Edge[] epL = { Edge.UR, Edge.UF, Edge.BL, Edge.UB, Edge.DR, Edge.DF, Edge.FL, Edge.DB, Edge.FR, Edge.UL, Edge.DL, Edge.BR };
        private static readonly sbyte[] eoL = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        private static readonly Corner[] cpB = { Corner.URF, Corner.UFL, Corner.UBR, Corner.DRB, Corner.DFR, Corner.DLF, Corner.ULB, Corner.DBL };
        private static readonly sbyte[] coB = { 0, 0, 1, 2, 0, 0, 2, 1 };
        private static readonly Edge[] epB = { Edge.UR, Edge.UF, Edge.UL, Edge.BR, Edge.DR, Edge.DF, Edge.DL, Edge.BL, Edge.FR, Edge.FL, Edge.UB, Edge.DB };
        private static readonly sbyte[] eoB = { 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 1, 1 };

        // The 6 basic face moves
        public static CubieCube[] moveCube = new CubieCube[6];

        static CubieCube()
        {
            moveCube[0] = new CubieCube(cpU, coU, epU, eoU);
            moveCube[1] = new CubieCube(cpR, coR, epR, eoR);
            moveCube[2] = new CubieCube(cpF, coF, epF, eoF);
            moveCube[3] = new CubieCube(cpD, coD, epD, eoD);
            moveCube[4] = new CubieCube(cpL, coL, epL, eoL);
            moveCube[5] = new CubieCube(cpB, coB, epB, eoB);
        }

        public CubieCube() { }

        public CubieCube(Corner[] cp, sbyte[] co, Edge[] ep, sbyte[] eo)
        {
            this.cp = (Corner[])cp.Clone();
            this.co = (sbyte[])co.Clone();
            this.ep = (Edge[])ep.Clone();
            this.eo = (sbyte[])eo.Clone();
        }

        // n choose k
        public static int Cnk(int n, int k)
        {
            int i, j, s;
            if (n < k) return 0;
            if (k > n / 2) k = n - k;
            for (s = 1, i = n, j = 1; i != n - k; i--, j++)
            {
                s *= i;
                s /= j;
            }
            return s;
        }

        public static void RotateLeft(Corner[] arr, int l, int r)
        {
            Corner temp = arr[l];
            for (int i = l; i < r; i++) arr[i] = arr[i + 1];
            arr[r] = temp;
        }

        public static void RotateRight(Corner[] arr, int l, int r)
        {
            Corner temp = arr[r];
            for (int i = r; i > l; i--) arr[i] = arr[i - 1];
            arr[l] = temp;
        }

        public static void RotateLeft(Edge[] arr, int l, int r)
        {
            Edge temp = arr[l];
            for (int i = l; i < r; i++) arr[i] = arr[i + 1];
            arr[r] = temp;
        }

        public static void RotateRight(Edge[] arr, int l, int r)
        {
            Edge temp = arr[r];
            for (int i = r; i > l; i--) arr[i] = arr[i - 1];
            arr[l] = temp;
        }

        // Multiply this cubie cube with another (corners only)
        public void CornerMultiply(CubieCube b)
        {
            Corner[] cPerm = new Corner[8];
            sbyte[] cOri = new sbyte[8];
            for (int corn = 0; corn < 8; corn++)
            {
                cPerm[corn] = cp[(int)b.cp[corn]];

                sbyte oriA = co[(int)b.cp[corn]];
                sbyte oriB = b.co[corn];
                sbyte ori = 0;

                if (oriA < 3 && oriB < 3)
                {
                    ori = (sbyte)(oriA + oriB);
                    if (ori >= 3) ori -= 3;
                }
                else if (oriA < 3 && oriB >= 3)
                {
                    ori = (sbyte)(oriA + oriB);
                    if (ori >= 6) ori -= 3;
                }
                else if (oriA >= 3 && oriB < 3)
                {
                    ori = (sbyte)(oriA - oriB);
                    if (ori < 3) ori += 3;
                }
                else if (oriA >= 3 && oriB >= 3)
                {
                    ori = (sbyte)(oriA - oriB);
                    if (ori < 0) ori += 3;
                }
                cOri[corn] = ori;
            }
            for (int c = 0; c < 8; c++)
            {
                cp[c] = cPerm[c];
                co[c] = cOri[c];
            }
        }

        // Multiply this cubie cube with another (edges only)
        public void EdgeMultiply(CubieCube b)
        {
            Edge[] ePerm = new Edge[12];
            sbyte[] eOri = new sbyte[12];
            for (int edge = 0; edge < 12; edge++)
            {
                ePerm[edge] = ep[(int)b.ep[edge]];
                eOri[edge] = (sbyte)((b.eo[edge] + eo[(int)b.ep[edge]]) % 2);
            }
            for (int e = 0; e < 12; e++)
            {
                ep[e] = ePerm[e];
                eo[e] = eOri[e];
            }
        }

        public void Multiply(CubieCube b)
        {
            CornerMultiply(b);
            EdgeMultiply(b);
        }

        // ********************************** Coordinates **********************************

        // Corner orientation coordinate (0..2186)
        public short GetTwist()
        {
            short ret = 0;
            for (int i = 0; i < 7; i++)
                ret = (short)(3 * ret + co[i]);
            return ret;
        }

        public void SetTwist(short twist)
        {
            int twistParity = 0;
            for (int i = 6; i >= 0; i--)
            {
                twistParity += co[i] = (sbyte)(twist % 3);
                twist /= 3;
            }
            co[7] = (sbyte)((3 - twistParity % 3) % 3);
        }

        // Edge orientation coordinate (0..2047)
        public short GetFlip()
        {
            short ret = 0;
            for (int i = 0; i < 11; i++)
                ret = (short)(2 * ret + eo[i]);
            return ret;
        }

        public void SetFlip(short flip)
        {
            int flipParity = 0;
            for (int i = 10; i >= 0; i--)
            {
                flipParity += eo[i] = (sbyte)(flip % 2);
                flip /= 2;
            }
            eo[11] = (sbyte)((2 - flipParity % 2) % 2);
        }

        // Corner permutation parity
        public short CornerParity()
        {
            int s = 0;
            for (int i = 7; i >= 1; i--)
                for (int j = i - 1; j >= 0; j--)
                    if ((int)cp[j] > (int)cp[i]) s++;
            return (short)(s % 2);
        }

        public short EdgeParity()
        {
            int s = 0;
            for (int i = 11; i >= 1; i--)
                for (int j = i - 1; j >= 0; j--)
                    if ((int)ep[j] > (int)ep[i]) s++;
            return (short)(s % 2);
        }

        // Permutation of the UD-slice edges FR,FL,BL,BR (0..11879)
        public short GetFRtoBR()
        {
            int a = 0, x = 0;
            Edge[] edge4 = new Edge[4];
            for (int j = 11; j >= 0; j--)
            {
                if (Edge.FR <= ep[j] && ep[j] <= Edge.BR)
                {
                    a += Cnk(11 - j, x + 1);
                    edge4[3 - x++] = ep[j];
                }
            }

            int b = 0;
            for (int j = 3; j > 0; j--)
            {
                int k = 0;
                while ((int)edge4[j] != j + 8)
                {
                    RotateLeft(edge4, 0, j);
                    k++;
                }
                b = (j + 1) * b + k;
            }
            return (short)(24 * a + b);
        }

        public void SetFRtoBR(short idx)
        {
            int x;
            Edge[] sliceEdge = { Edge.FR, Edge.FL, Edge.BL, Edge.BR };
            Edge[] otherEdge = { Edge.UR, Edge.UF, Edge.UL, Edge.UB, Edge.DR, Edge.DF, Edge.DL, Edge.DB };
            int b = idx % 24;
            int a = idx / 24;
            for (int e = 0; e < 12; e++)
                ep[e] = Edge.DB; // invalidate

            for (int j = 1, k; j < 4; j++)
            {
                k = b % (j + 1);
                b /= j + 1;
                while (k-- > 0)
                    RotateRight(sliceEdge, 0, j);
            }

            x = 3;
            for (int j = 0; j < 12; j++)
            {
                if (a - Cnk(11 - j, x + 1) >= 0)
                {
                    ep[j] = sliceEdge[3 - x];
                    a -= Cnk(11 - j, x-- + 1);
                }
            }
            x = 0;
            for (int j = 0; j < 12; j++)
            {
                if (ep[j] == Edge.DB)
                    ep[j] = otherEdge[x++];
            }
        }

        // Permutation of corners URF,UFL,ULB,UBR,DFR,DLF (0..20159)
        public short GetURFtoDLF()
        {
            int a = 0, x = 0;
            Corner[] corner6 = new Corner[6];
            for (int j = 0; j < 8; j++)
            {
                if (cp[j] <= Corner.DLF)
                {
                    a += Cnk(j, x + 1);
                    corner6[x++] = cp[j];
                }
            }

            int b = 0;
            for (int j = 5; j > 0; j--)
            {
                int k = 0;
                while ((int)corner6[j] != j)
                {
                    RotateLeft(corner6, 0, j);
                    k++;
                }
                b = (j + 1) * b + k;
            }
            return (short)(720 * a + b);
        }

        public void SetURFtoDLF(short idx)
        {
            int x;
            Corner[] corner6 = { Corner.URF, Corner.UFL, Corner.ULB, Corner.UBR, Corner.DFR, Corner.DLF };
            Corner[] otherCorner = { Corner.DBL, Corner.DRB };
            int b = idx % 720;
            int a = idx / 720;
            for (int c = 0; c < 8; c++)
                cp[c] = Corner.DRB; // invalidate

            for (int j = 1, k; j < 6; j++)
            {
                k = b % (j + 1);
                b /= j + 1;
                while (k-- > 0)
                    RotateRight(corner6, 0, j);
            }
            x = 5;
            for (int j = 7; j >= 0; j--)
            {
                if (a - Cnk(j, x + 1) >= 0)
                {
                    cp[j] = corner6[x];
                    a -= Cnk(j, x-- + 1);
                }
            }
            x = 0;
            for (int j = 0; j < 8; j++)
            {
                if (cp[j] == Corner.DRB)
                    cp[j] = otherCorner[x++];
            }
        }

        // Permutation of the six edges UR,UF,UL,UB,DR,DF (phase 2 coordinate)
        public int GetURtoDF()
        {
            int a = 0, x = 0;
            Edge[] edge6 = new Edge[6];
            for (int j = 0; j < 12; j++)
            {
                if (ep[j] <= Edge.DF)
                {
                    a += Cnk(j, x + 1);
                    edge6[x++] = ep[j];
                }
            }

            int b = 0;
            for (int j = 5; j > 0; j--)
            {
                int k = 0;
                while ((int)edge6[j] != j)
                {
                    RotateLeft(edge6, 0, j);
                    k++;
                }
                b = (j + 1) * b + k;
            }
            return 720 * a + b;
        }

        public void SetURtoDF(int idx)
        {
            int x;
            Edge[] edge6 = { Edge.UR, Edge.UF, Edge.UL, Edge.UB, Edge.DR, Edge.DF };
            Edge[] otherEdge = { Edge.DL, Edge.DB, Edge.FR, Edge.FL, Edge.BL, Edge.BR };
            int b = idx % 720;
            int a = idx / 720;
            for (int e = 0; e < 12; e++)
                ep[e] = Edge.BR; // invalidate

            for (int j = 1, k; j < 6; j++)
            {
                k = b % (j + 1);
                b /= j + 1;
                while (k-- > 0)
                    RotateRight(edge6, 0, j);
            }
            x = 5;
            for (int j = 11; j >= 0; j--)
            {
                if (a - Cnk(j, x + 1) >= 0)
                {
                    ep[j] = edge6[x];
                    a -= Cnk(j, x-- + 1);
                }
            }
            x = 0;
            for (int j = 0; j < 12; j++)
            {
                if (ep[j] == Edge.BR)
                    ep[j] = otherEdge[x++];
            }
        }

        // Permutation of the three edges UR,UF,UL (0..1319)
        public short GetURtoUL()
        {
            int a = 0, x = 0;
            Edge[] edge3 = new Edge[3];
            for (int j = 0; j < 12; j++)
            {
                if (ep[j] <= Edge.UL)
                {
                    a += Cnk(j, x + 1);
                    edge3[x++] = ep[j];
                }
            }

            int b = 0;
            for (int j = 2; j > 0; j--)
            {
                int k = 0;
                while ((int)edge3[j] != j)
                {
                    RotateLeft(edge3, 0, j);
                    k++;
                }
                b = (j + 1) * b + k;
            }
            return (short)(6 * a + b);
        }

        public void SetURtoUL(short idx)
        {
            int x;
            Edge[] edge3 = { Edge.UR, Edge.UF, Edge.UL };
            int b = idx % 6;
            int a = idx / 6;
            for (int e = 0; e < 12; e++)
                ep[e] = Edge.BR; // invalidate

            for (int j = 1, k; j < 3; j++)
            {
                k = b % (j + 1);
                b /= j + 1;
                while (k-- > 0)
                    RotateRight(edge3, 0, j);
            }
            x = 2;
            for (int j = 11; j >= 0; j--)
            {
                if (a - Cnk(j, x + 1) >= 0)
                {
                    ep[j] = edge3[x];
                    a -= Cnk(j, x-- + 1);
                }
            }
        }

        // Permutation of the three edges UB,DR,DF (0..1319)
        public short GetUBtoDF()
        {
            int a = 0, x = 0;
            Edge[] edge3 = new Edge[3];
            for (int j = 0; j < 12; j++)
            {
                if (Edge.UB <= ep[j] && ep[j] <= Edge.DF)
                {
                    a += Cnk(j, x + 1);
                    edge3[x++] = ep[j];
                }
            }

            int b = 0;
            for (int j = 2; j > 0; j--)
            {
                int k = 0;
                while ((int)edge3[j] != (int)Edge.UB + j)
                {
                    RotateLeft(edge3, 0, j);
                    k++;
                }
                b = (j + 1) * b + k;
            }
            return (short)(6 * a + b);
        }

        public void SetUBtoDF(short idx)
        {
            int x;
            Edge[] edge3 = { Edge.UB, Edge.DR, Edge.DF };
            int b = idx % 6;
            int a = idx / 6;
            for (int e = 0; e < 12; e++)
                ep[e] = Edge.BR; // invalidate

            for (int j = 1, k; j < 3; j++)
            {
                k = b % (j + 1);
                b /= j + 1;
                while (k-- > 0)
                    RotateRight(edge3, 0, j);
            }
            x = 2;
            for (int j = 11; j >= 0; j--)
            {
                if (a - Cnk(j, x + 1) >= 0)
                {
                    ep[j] = edge3[x];
                    a -= Cnk(j, x-- + 1);
                }
            }
        }

        public static int GetURtoDF(short idx1, short idx2)
        {
            CubieCube a = new CubieCube();
            CubieCube b = new CubieCube();
            a.SetURtoUL(idx1);
            b.SetUBtoDF(idx2);
            for (int i = 0; i < 8; i++)
            {
                if (a.ep[i] != Edge.BR)
                {
                    if (b.ep[i] != Edge.BR)
                        return -1; // collision
                    else
                        b.ep[i] = a.ep[i];
                }
            }
            return b.GetURtoDF();
        }

        /// <summary>
        /// Convert cubie representation back to the facelet level.
        /// </summary>
        public FaceCube ToFaceCube()
        {
            var fcRet = new FaceCube();
            for (int i = 0; i < 8; i++)
            {
                int j = (int)cp[i];
                sbyte ori = co[i];
                for (int n = 0; n < 3; n++)
                    fcRet.f[(int)FaceCube.cornerFacelet[i][(n + ori) % 3]] = FaceCube.cornerColor[j][n];
            }
            for (int i = 0; i < 12; i++)
            {
                int j = (int)ep[i];
                sbyte ori = eo[i];
                for (int n = 0; n < 2; n++)
                    fcRet.f[(int)FaceCube.edgeFacelet[i][(n + ori) % 2]] = FaceCube.edgeColor[j][n];
            }
            return fcRet;
        }

        /// <summary>
        /// Check if the cube state is physically possible.
        /// Returns 0 if ok, negative error code otherwise.
        /// </summary>
        public int Verify()
        {
            int sum = 0;
            int[] edgeCount = new int[12];
            foreach (Edge e in ep)
                edgeCount[(int)e]++;
            for (int i = 0; i < 12; i++)
                if (edgeCount[i] != 1)
                    return -2; // not all 12 edges exist exactly once

            foreach (sbyte o in eo)
                sum += o;
            if (sum % 2 != 0)
                return -3; // flip error: one edge flipped

            int[] cornerCount = new int[8];
            foreach (Corner c in cp)
                cornerCount[(int)c]++;
            for (int i = 0; i < 8; i++)
                if (cornerCount[i] != 1)
                    return -4; // not all corners exist exactly once

            sum = 0;
            foreach (sbyte o in co)
                sum += o;
            if (sum % 3 != 0)
                return -5; // twist error: one corner twisted

            if ((EdgeParity() ^ CornerParity()) != 0)
                return -6; // parity error: two corners or two edges swapped

            return 0;
        }
    }
}
