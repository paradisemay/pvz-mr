using System;

namespace Pvz.Core
{
    /// <summary>
    /// QR-looking module grid for a box label: three finder patterns, timing lines, an alignment pattern and
    /// pseudo-random data derived from the box id. Decorative only — the game never decodes it — but stable:
    /// the same id always gives the same picture, in the editor and at runtime.
    /// </summary>
    public static class QrPattern
    {
        public const int Modules = 29;

        /// <summary>Returns grid[x, y], true = dark module; y = 0 is the top row.</summary>
        public static bool[,] Generate(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id must not be empty.", nameof(id));

            var grid = new bool[Modules, Modules];
            var rng = new Random(StableHash(id));
            for (var y = 0; y < Modules; y++)
            for (var x = 0; x < Modules; x++)
                grid[x, y] = rng.Next(100) < 48;

            Finder(grid, 0, 0);
            Finder(grid, Modules - 7, 0);
            Finder(grid, 0, Modules - 7);

            for (var i = 8; i < Modules - 8; i++)
            {
                grid[i, 6] = i % 2 == 0;
                grid[6, i] = i % 2 == 0;
            }

            for (var y = -2; y <= 2; y++)
            for (var x = -2; x <= 2; x++)
            {
                var ring = Math.Max(Math.Abs(x), Math.Abs(y));
                grid[Modules - 7 + x, Modules - 7 + y] = ring != 1;
            }

            return grid;
        }

        /// <summary>FNV-1a; unlike string.GetHashCode it is the same on every platform and run.</summary>
        public static int StableHash(string s)
        {
            unchecked
            {
                var h = (int)2166136261;
                foreach (var c in s) h = (h ^ c) * 16777619;
                return h;
            }
        }

        private static void Finder(bool[,] grid, int ox, int oy)
        {
            for (var y = -1; y <= 7; y++)
            for (var x = -1; x <= 7; x++)
            {
                var gx = ox + x;
                var gy = oy + y;
                if (gx < 0 || gy < 0 || gx >= Modules || gy >= Modules) continue;
                var ring = Math.Max(Math.Abs(x - 3), Math.Abs(y - 3));
                // 7x7 outer ring, white gap, 3x3 core, white separator around
                grid[gx, gy] = ring != 2 && ring != 4;
            }
        }
    }
}
