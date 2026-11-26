using System;
using System.Collections.Generic;

namespace Kesselwerk.Core
{
    /// <summary>
    /// Wabenfeld wie in Auto-Battlern: 7 Spalten, je Seite 4 Reihen (zusammen 8).
    /// Index = Reihe * 7 + Spalte. Reihen 0–3 gehören Spieler 0, 4–7 dem Gegner.
    /// Ungerade Reihen sind um eine halbe Wabe nach rechts versetzt („odd-r“).
    /// </summary>
    public static class Hex
    {
        public const int Cols = 7;
        public const int RowsPerSide = 4;
        public const int Rows = RowsPerSide * 2;
        public const int SideCells = Cols * RowsPerSide; // 28
        public const int Cells = Cols * Rows;            // 56

        public static int Row(int i) => i / Cols;
        public static int Col(int i) => i % Cols;
        public static int Index(int col, int row) => row * Cols + col;
        public static bool Valid(int col, int row) => col >= 0 && col < Cols && row >= 0 && row < Rows;

        /// <summary>Feld der eigenen Seite (0–27, Reihe 0 = ganz hinten) auf das gemeinsame Feld legen.</summary>
        public static int ToBoard(int sideCell, int team)
        {
            int c = sideCell % Cols, r = sideCell / Cols; // r: 0 = vorne (zur Mitte), 3 = hinten
            return team == 0 ? Index(c, RowsPerSide - 1 - r) : Index(Cols - 1 - c, RowsPerSide + r);
        }

        static void Cube(int i, out int x, out int y, out int z)
        {
            int col = Col(i), row = Row(i);
            x = col - (row - (row & 1)) / 2;
            z = row;
            y = -x - z;
        }

        public static int Distance(int a, int b)
        {
            Cube(a, out int ax, out int ay, out int az);
            Cube(b, out int bx, out int by, out int bz);
            return (Math.Abs(ax - bx) + Math.Abs(ay - by) + Math.Abs(az - bz)) / 2;
        }

        static readonly int[,] OddR = { { 1, 0 }, { 1, -1 }, { 0, -1 }, { -1, 0 }, { 0, 1 }, { 1, 1 } };
        static readonly int[,] EvenR = { { 1, 0 }, { 0, -1 }, { -1, -1 }, { -1, 0 }, { -1, 1 }, { 0, 1 } };

        public static List<int> Neighbors(int i)
        {
            var res = new List<int>(6);
            int col = Col(i), row = Row(i);
            var d = (row & 1) == 1 ? OddR : EvenR;
            for (int k = 0; k < 6; k++)
            {
                int c = col + d[k, 0], r = row + d[k, 1];
                if (Valid(c, r)) res.Add(Index(c, r));
            }
            return res;
        }
    }
}
