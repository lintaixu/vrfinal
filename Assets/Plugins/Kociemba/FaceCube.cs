using System;

namespace Kociemba
{
    public enum CubeColor { U, R, F, D, L, B }

    public enum Facelet
    {
        U1, U2, U3, U4, U5, U6, U7, U8, U9,
        R1, R2, R3, R4, R5, R6, R7, R8, R9,
        F1, F2, F3, F4, F5, F6, F7, F8, F9,
        D1, D2, D3, D4, D5, D6, D7, D8, D9,
        L1, L2, L3, L4, L5, L6, L7, L8, L9,
        B1, B2, B3, B4, B5, B6, B7, B8, B9
    }

    /// <summary>
    /// Cube on the facelet level (54 sticker colors).
    /// </summary>
    public class FaceCube
    {
        public CubeColor[] f = new CubeColor[54];

        // Map corner positions to facelet positions
        public static readonly Facelet[][] cornerFacelet =
        {
            new[] { Facelet.U9, Facelet.R1, Facelet.F3 },
            new[] { Facelet.U7, Facelet.F1, Facelet.L3 },
            new[] { Facelet.U1, Facelet.L1, Facelet.B3 },
            new[] { Facelet.U3, Facelet.B1, Facelet.R3 },
            new[] { Facelet.D3, Facelet.F9, Facelet.R7 },
            new[] { Facelet.D1, Facelet.L9, Facelet.F7 },
            new[] { Facelet.D7, Facelet.B9, Facelet.L7 },
            new[] { Facelet.D9, Facelet.R9, Facelet.B7 }
        };

        // Map edge positions to facelet positions
        public static readonly Facelet[][] edgeFacelet =
        {
            new[] { Facelet.U6, Facelet.R2 },
            new[] { Facelet.U8, Facelet.F2 },
            new[] { Facelet.U4, Facelet.L2 },
            new[] { Facelet.U2, Facelet.B2 },
            new[] { Facelet.D6, Facelet.R8 },
            new[] { Facelet.D2, Facelet.F8 },
            new[] { Facelet.D4, Facelet.L8 },
            new[] { Facelet.D8, Facelet.B8 },
            new[] { Facelet.F6, Facelet.R4 },
            new[] { Facelet.F4, Facelet.L6 },
            new[] { Facelet.B6, Facelet.L4 },
            new[] { Facelet.B4, Facelet.R6 }
        };

        // Map corner positions to colors
        public static readonly CubeColor[][] cornerColor =
        {
            new[] { CubeColor.U, CubeColor.R, CubeColor.F },
            new[] { CubeColor.U, CubeColor.F, CubeColor.L },
            new[] { CubeColor.U, CubeColor.L, CubeColor.B },
            new[] { CubeColor.U, CubeColor.B, CubeColor.R },
            new[] { CubeColor.D, CubeColor.F, CubeColor.R },
            new[] { CubeColor.D, CubeColor.L, CubeColor.F },
            new[] { CubeColor.D, CubeColor.B, CubeColor.L },
            new[] { CubeColor.D, CubeColor.R, CubeColor.B }
        };

        // Map edge positions to colors
        public static readonly CubeColor[][] edgeColor =
        {
            new[] { CubeColor.U, CubeColor.R },
            new[] { CubeColor.U, CubeColor.F },
            new[] { CubeColor.U, CubeColor.L },
            new[] { CubeColor.U, CubeColor.B },
            new[] { CubeColor.D, CubeColor.R },
            new[] { CubeColor.D, CubeColor.F },
            new[] { CubeColor.D, CubeColor.L },
            new[] { CubeColor.D, CubeColor.B },
            new[] { CubeColor.F, CubeColor.R },
            new[] { CubeColor.F, CubeColor.L },
            new[] { CubeColor.B, CubeColor.L },
            new[] { CubeColor.B, CubeColor.R }
        };

        public FaceCube()
        {
            string s = "UUUUUUUUURRRRRRRRRFFFFFFFFFDDDDDDDDDLLLLLLLLLBBBBBBBBB";
            for (int i = 0; i < 54; i++)
                f[i] = (CubeColor)Enum.Parse(typeof(CubeColor), s[i].ToString());
        }

        public FaceCube(string cubeString)
        {
            for (int i = 0; i < 54; i++)
                f[i] = (CubeColor)Enum.Parse(typeof(CubeColor), cubeString[i].ToString());
        }

        public string ToFaceletString()
        {
            var sb = new System.Text.StringBuilder(54);
            for (int i = 0; i < 54; i++)
                sb.Append(f[i].ToString());
            return sb.ToString();
        }

        /// <summary>
        /// Convert facelet representation to cubie representation.
        /// </summary>
        public CubieCube ToCubieCube()
        {
            sbyte ori;
            var ccRet = new CubieCube();
            for (int i = 0; i < 8; i++)
                ccRet.cp[i] = Corner.URF; // invalidate
            for (int i = 0; i < 12; i++)
                ccRet.ep[i] = Edge.UR;

            CubeColor col1, col2;
            for (int i = 0; i < 8; i++)
            {
                Corner corner = (Corner)i;
                // get the colors of the cubie at corner i, starting with U/D
                for (ori = 0; ori < 3; ori++)
                    if (f[(int)cornerFacelet[i][ori]] == CubeColor.U || f[(int)cornerFacelet[i][ori]] == CubeColor.D)
                        break;
                col1 = f[(int)cornerFacelet[i][(ori + 1) % 3]];
                col2 = f[(int)cornerFacelet[i][(ori + 2) % 3]];

                for (int j = 0; j < 8; j++)
                {
                    if (col1 == cornerColor[j][1] && col2 == cornerColor[j][2])
                    {
                        // in cornerposition i we have cornercubie j
                        ccRet.cp[i] = (Corner)j;
                        ccRet.co[i] = (sbyte)(ori % 3);
                        break;
                    }
                }
            }

            for (int i = 0; i < 12; i++)
            {
                for (int j = 0; j < 12; j++)
                {
                    if (f[(int)edgeFacelet[i][0]] == edgeColor[j][0]
                        && f[(int)edgeFacelet[i][1]] == edgeColor[j][1])
                    {
                        ccRet.ep[i] = (Edge)j;
                        ccRet.eo[i] = 0;
                        break;
                    }
                    if (f[(int)edgeFacelet[i][0]] == edgeColor[j][1]
                        && f[(int)edgeFacelet[i][1]] == edgeColor[j][0])
                    {
                        ccRet.ep[i] = (Edge)j;
                        ccRet.eo[i] = 1;
                        break;
                    }
                }
            }
            return ccRet;
        }
    }
}
