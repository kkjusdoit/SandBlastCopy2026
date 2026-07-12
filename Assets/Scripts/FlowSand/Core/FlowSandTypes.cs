using System;
using UnityEngine;

namespace FlowSand.Core
{
    public enum CellColor : byte
    {
        Empty = 0,
        Coral = 1,
        Mint = 2,
        Gold = 3,
        Sky = 4,
        Violet = 5,
    }

    // Per-grain behavior tag, stored in a plane parallel to the color grid.
    // Normal grains flow and take part in bridge clears; special materials
    // override those behaviors while keeping their color slot independent.
    public enum SandMaterial : byte
    {
        Normal = 0,
        Obstacle = 1,
        Bomb = 2,
    }

    public enum TetrominoKind : byte
    {
        I,
        O,
        T,
        S,
        Z,
        J,
        L,
        Domino,
        Mono,
        Triomino,
    }

    public enum MixedColorPattern : byte
    {
        LeftRight,
        CenterSymmetric,
        PerCell,
    }

    // How a falling bomb piece behaves when it lands.
    public enum BombPieceKind : byte
    {
        None = 0,
        Mine = 1,     // becomes a live fused mine (player can defuse or dodge)
        Instant = 2,  // detonates immediately on landing
    }

    [Serializable]
    public struct ActivePiece
    {
        public TetrominoKind Kind;
        public CellColor Color;
        public int Rotation;
        public int Col;
        public int Row;
        public bool IsMixed;
        public bool IsSuperMixed;
        public BombPieceKind BombKind;
        public MixedColorPattern MixedPattern;
        public int ColorSeed;

        public bool IsBomb => BombKind != BombPieceKind.None;
    }

    public readonly struct TetrominoDefinition
    {
        public TetrominoDefinition(TetrominoKind kind, Vector2Int[][] rotations)
        {
            Kind = kind;
            Rotations = rotations;
        }

        public TetrominoKind Kind { get; }
        public Vector2Int[][] Rotations { get; }
    }

    public readonly struct BoardBounds
    {
        public BoardBounds(int minX, int maxX, int minY, int maxY)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }

        public int MinX { get; }
        public int MaxX { get; }
        public int MinY { get; }
        public int MaxY { get; }
        public int Width => (MaxX - MinX) + 1;
        public int Height => (MaxY - MinY) + 1;
    }

    public static class TetrominoLibrary
    {
        private static readonly TetrominoDefinition[] Definitions =
        {
                new TetrominoDefinition(
                    TetrominoKind.I,
                    new[]
                    {
                        Cells((0, 1), (1, 1), (2, 1), (3, 1)),
                        Cells((2, 0), (2, 1), (2, 2), (2, 3)),
                        Cells((0, 2), (1, 2), (2, 2), (3, 2)),
                        Cells((1, 0), (1, 1), (1, 2), (1, 3)),
                    }),
                new TetrominoDefinition(
                    TetrominoKind.O,
                    new[]
                    {
                        Cells((1, 0), (2, 0), (1, 1), (2, 1)),
                        Cells((1, 0), (2, 0), (1, 1), (2, 1)),
                        Cells((1, 0), (2, 0), (1, 1), (2, 1)),
                        Cells((1, 0), (2, 0), (1, 1), (2, 1)),
                    }),
                new TetrominoDefinition(
                    TetrominoKind.T,
                    new[]
                    {
                        Cells((1, 0), (0, 1), (1, 1), (2, 1)),
                        Cells((1, 0), (1, 1), (2, 1), (1, 2)),
                        Cells((0, 1), (1, 1), (2, 1), (1, 2)),
                        Cells((1, 0), (0, 1), (1, 1), (1, 2)),
                    }),
                new TetrominoDefinition(
                    TetrominoKind.S,
                    new[]
                    {
                        Cells((1, 0), (2, 0), (0, 1), (1, 1)),
                        Cells((1, 0), (1, 1), (2, 1), (2, 2)),
                        Cells((1, 1), (2, 1), (0, 2), (1, 2)),
                        Cells((0, 0), (0, 1), (1, 1), (1, 2)),
                    }),
                new TetrominoDefinition(
                    TetrominoKind.Z,
                    new[]
                    {
                        Cells((0, 0), (1, 0), (1, 1), (2, 1)),
                        Cells((2, 0), (1, 1), (2, 1), (1, 2)),
                        Cells((0, 1), (1, 1), (1, 2), (2, 2)),
                        Cells((1, 0), (0, 1), (1, 1), (0, 2)),
                    }),
                new TetrominoDefinition(
                    TetrominoKind.J,
                    new[]
                    {
                        Cells((0, 0), (0, 1), (1, 1), (2, 1)),
                        Cells((1, 0), (2, 0), (1, 1), (1, 2)),
                        Cells((0, 1), (1, 1), (2, 1), (2, 2)),
                        Cells((1, 0), (1, 1), (0, 2), (1, 2)),
                    }),
                new TetrominoDefinition(
                    TetrominoKind.L,
                    new[]
                    {
                        Cells((2, 0), (0, 1), (1, 1), (2, 1)),
                        Cells((1, 0), (1, 1), (1, 2), (2, 2)),
                        Cells((0, 1), (1, 1), (2, 1), (0, 2)),
                        Cells((0, 0), (1, 0), (1, 1), (1, 2)),
                    }),
                new TetrominoDefinition(
                    TetrominoKind.Domino,
                    new[]
                    {
                        Cells((0, 0), (1, 0)),
                        Cells((0, 0), (0, 1)),
                        Cells((0, 0), (1, 0)),
                        Cells((0, 0), (0, 1)),
                    }),
                new TetrominoDefinition(
                    TetrominoKind.Mono,
                    new[]
                    {
                        Cells((0, 0)),
                        Cells((0, 0)),
                        Cells((0, 0)),
                        Cells((0, 0)),
                    }),
                new TetrominoDefinition(
                    TetrominoKind.Triomino,
                    new[]
                    {
                        Cells((0, 0), (1, 0), (2, 0)),
                        Cells((0, 0), (0, 1), (0, 2)),
                        Cells((0, 0), (1, 0), (2, 0)),
                        Cells((0, 0), (0, 1), (0, 2)),
                    }),
        };

        private static readonly CellColor[] Palette = { CellColor.Coral, CellColor.Mint, CellColor.Gold, CellColor.Sky, CellColor.Violet };
        private static readonly BoardBounds[,] Bounds = BuildBounds();

        public static TetrominoDefinition Get(TetrominoKind kind)
        {
            return Definitions[(int)kind];
        }

        public static Vector2Int[] GetCells(TetrominoKind kind, int rotation)
        {
            return Definitions[(int)kind].Rotations[rotation & 3];
        }

        public static BoardBounds GetBounds(TetrominoKind kind, int rotation)
        {
            return Bounds[(int)kind, rotation & 3];
        }

        private static BoardBounds CalculateBounds(TetrominoKind kind, int rotation)
        {
            Vector2Int[] cells = GetCells(kind, rotation);
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;

            for (int i = 0; i < cells.Length; i++)
            {
                Vector2Int cell = cells[i];
                minX = Math.Min(minX, cell.x);
                maxX = Math.Max(maxX, cell.x);
                minY = Math.Min(minY, cell.y);
                maxY = Math.Max(maxY, cell.y);
            }

            return new BoardBounds(minX, maxX, minY, maxY);
        }

        public static TetrominoKind RandomKind(System.Random random)
        {
            return (TetrominoKind)random.Next(Definitions.Length);
        }

        public static CellColor RandomColor(System.Random random)
        {
            return Palette[random.Next(Palette.Length)];
        }

        public static CellColor GetPieceGrainColor(ActivePiece piece, int cellIndex, int x, int y, int cellScale)
        {
            if (!piece.IsMixed)
            {
                return piece.Color;
            }

            if (piece.IsSuperMixed)
            {
                int excludedColor = piece.ColorSeed % Palette.Length;
                int phase = (piece.ColorSeed / Palette.Length) & 3;
                int slot = (phase + (cellIndex * cellScale * cellScale) + (y * cellScale) + x) & 3;
                int paletteIndex = slot >= excludedColor ? slot + 1 : slot;
                return Palette[paletteIndex];
            }

            if (piece.MixedPattern == MixedColorPattern.PerCell)
            {
                Vector2Int[] pieceCells = GetCells(piece.Kind, piece.Rotation);
                int[] cellColors = new int[pieceCells.Length];
                for (int i = 0; i <= cellIndex; i++)
                {
                    int candidate = PositiveModulo(piece.ColorSeed + (i * 1103515245), Palette.Length);
                    for (int attempts = 0; attempts < Palette.Length; attempts++)
                    {
                        bool conflicts = false;
                        for (int previous = 0; previous < i; previous++)
                        {
                            Vector2Int delta = pieceCells[i] - pieceCells[previous];
                            if (Math.Abs(delta.x) + Math.Abs(delta.y) == 1 && cellColors[previous] == candidate)
                            {
                                conflicts = true;
                                break;
                            }
                        }

                        if (!conflicts)
                        {
                            break;
                        }

                        candidate = (candidate + 1) % Palette.Length;
                    }

                    cellColors[i] = candidate;
                }

                return Palette[cellColors[cellIndex]];
            }

            int firstColorIndex = PositiveModulo(piece.ColorSeed, Palette.Length);
            int secondOffset = 1 + ((piece.ColorSeed / Palette.Length) % (Palette.Length - 1));
            int secondColorIndex = (firstColorIndex + secondOffset) % Palette.Length;

            Vector2Int cell = GetCells(piece.Kind, piece.Rotation)[cellIndex];
            BoardBounds bounds = GetBounds(piece.Kind, piece.Rotation);
            int localX = ((cell.x - bounds.MinX) * cellScale) + x;
            int totalWidth = bounds.Width * cellScale;
            bool useFirstColor;

            if (piece.MixedPattern == MixedColorPattern.CenterSymmetric)
            {
                int outerBandWidth = Math.Max(1, totalWidth / 4);
                useFirstColor = localX >= outerBandWidth && localX < totalWidth - outerBandWidth;
            }
            else
            {
                useFirstColor = localX < totalWidth / 2;
            }

            return Palette[useFirstColor ? firstColorIndex : secondColorIndex];
        }

        private static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static Vector2Int[] Cells(params (int x, int y)[] points)
        {
            Vector2Int[] result = new Vector2Int[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                result[i] = new Vector2Int(points[i].x, points[i].y);
            }

            return result;
        }

        private static BoardBounds[,] BuildBounds()
        {
            BoardBounds[,] bounds = new BoardBounds[Definitions.Length, 4];
            for (int kind = 0; kind < Definitions.Length; kind++)
            {
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    bounds[kind, rotation] = CalculateBounds((TetrominoKind)kind, rotation);
                }
            }

            return bounds;
        }
    }
}
