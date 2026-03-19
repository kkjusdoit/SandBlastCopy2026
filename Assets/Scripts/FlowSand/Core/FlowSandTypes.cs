using System;
using System.Collections.Generic;
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

    public enum TetrominoKind : byte
    {
        I,
        O,
        T,
        S,
        Z,
        J,
        L,
    }

    [Serializable]
    public struct ActivePiece
    {
        public TetrominoKind Kind;
        public CellColor Color;
        public int Rotation;
        public int Col;
        public int Row;
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
        private static readonly Dictionary<TetrominoKind, TetrominoDefinition> Definitions = new()
        {
            {
                TetrominoKind.I,
                new TetrominoDefinition(
                    TetrominoKind.I,
                    new[]
                    {
                        Cells((0, 1), (1, 1), (2, 1), (3, 1)),
                        Cells((2, 0), (2, 1), (2, 2), (2, 3)),
                        Cells((0, 2), (1, 2), (2, 2), (3, 2)),
                        Cells((1, 0), (1, 1), (1, 2), (1, 3)),
                    })
            },
            {
                TetrominoKind.O,
                new TetrominoDefinition(
                    TetrominoKind.O,
                    new[]
                    {
                        Cells((1, 0), (2, 0), (1, 1), (2, 1)),
                        Cells((1, 0), (2, 0), (1, 1), (2, 1)),
                        Cells((1, 0), (2, 0), (1, 1), (2, 1)),
                        Cells((1, 0), (2, 0), (1, 1), (2, 1)),
                    })
            },
            {
                TetrominoKind.T,
                new TetrominoDefinition(
                    TetrominoKind.T,
                    new[]
                    {
                        Cells((1, 0), (0, 1), (1, 1), (2, 1)),
                        Cells((1, 0), (1, 1), (2, 1), (1, 2)),
                        Cells((0, 1), (1, 1), (2, 1), (1, 2)),
                        Cells((1, 0), (0, 1), (1, 1), (1, 2)),
                    })
            },
            {
                TetrominoKind.S,
                new TetrominoDefinition(
                    TetrominoKind.S,
                    new[]
                    {
                        Cells((1, 0), (2, 0), (0, 1), (1, 1)),
                        Cells((1, 0), (1, 1), (2, 1), (2, 2)),
                        Cells((1, 1), (2, 1), (0, 2), (1, 2)),
                        Cells((0, 0), (0, 1), (1, 1), (1, 2)),
                    })
            },
            {
                TetrominoKind.Z,
                new TetrominoDefinition(
                    TetrominoKind.Z,
                    new[]
                    {
                        Cells((0, 0), (1, 0), (1, 1), (2, 1)),
                        Cells((2, 0), (1, 1), (2, 1), (1, 2)),
                        Cells((0, 1), (1, 1), (1, 2), (2, 2)),
                        Cells((1, 0), (0, 1), (1, 1), (0, 2)),
                    })
            },
            {
                TetrominoKind.J,
                new TetrominoDefinition(
                    TetrominoKind.J,
                    new[]
                    {
                        Cells((0, 0), (0, 1), (1, 1), (2, 1)),
                        Cells((1, 0), (2, 0), (1, 1), (1, 2)),
                        Cells((0, 1), (1, 1), (2, 1), (2, 2)),
                        Cells((1, 0), (1, 1), (0, 2), (1, 2)),
                    })
            },
            {
                TetrominoKind.L,
                new TetrominoDefinition(
                    TetrominoKind.L,
                    new[]
                    {
                        Cells((2, 0), (0, 1), (1, 1), (2, 1)),
                        Cells((1, 0), (1, 1), (1, 2), (2, 2)),
                        Cells((0, 1), (1, 1), (2, 1), (0, 2)),
                        Cells((0, 0), (1, 0), (1, 1), (1, 2)),
                    })
            },
        };

        private static readonly CellColor[] Palette = { CellColor.Coral, CellColor.Mint, CellColor.Gold, CellColor.Sky, CellColor.Violet };

        public static TetrominoDefinition Get(TetrominoKind kind)
        {
            return Definitions[kind];
        }

        public static Vector2Int[] GetCells(TetrominoKind kind, int rotation)
        {
            return Definitions[kind].Rotations[rotation & 3];
        }

        public static BoardBounds GetBounds(TetrominoKind kind, int rotation)
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
            Array values = Enum.GetValues(typeof(TetrominoKind));
            return (TetrominoKind)values.GetValue(random.Next(values.Length));
        }

        public static CellColor RandomColor(System.Random random)
        {
            return Palette[random.Next(Palette.Length)];
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
    }
}
