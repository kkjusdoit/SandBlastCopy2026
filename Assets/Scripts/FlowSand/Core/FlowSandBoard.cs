using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlowSand.Core
{
    public sealed class FlowSandBoard
    {
        private readonly CellColor[] sandGrid;
        private readonly int[] shuffleBuffer;
        private readonly Queue<TetrominoKind> pieceBag = new();
        private readonly HashSet<int> bridgeVisited = new();
        private readonly HashSet<int> bridgeClearSet = new();
        private readonly List<int> bridgeResult = new();
        private readonly List<int> bridgeComponent = new();
        private readonly Stack<int> bridgeStack = new();

        public FlowSandBoard(int coarseCols, int coarseRows, int grainScale)
        {
            CoarseCols = coarseCols;
            CoarseRows = coarseRows;
            GrainScale = grainScale;
            SandCols = coarseCols * grainScale;
            SandRows = coarseRows * grainScale;

            sandGrid = new CellColor[SandCols * SandRows];
            shuffleBuffer = new int[SandCols];
        }

        public int CoarseCols { get; }
        public int CoarseRows { get; }
        public int GrainScale { get; }
        public int SandCols { get; }
        public int SandRows { get; }
        public ActivePiece? CurrentPiece { get; private set; }
        public ActivePiece NextPiece { get; private set; }

        public CellColor GetSand(int x, int y)
        {
            if (!IsInsideSand(x, y))
            {
                return CellColor.Empty;
            }

            return sandGrid[ToIndex(x, y)];
        }

        public bool HasActivePiece => CurrentPiece.HasValue;

        public void Reset(System.Random random)
        {
            Array.Fill(sandGrid, CellColor.Empty);
            pieceBag.Clear();
            CurrentPiece = null;
            NextPiece = CreateQueuedPiece(random);
        }

        public bool SpawnNextPiece(System.Random random)
        {
            ActivePiece next = NextPiece;
            BoardBounds bounds = TetrominoLibrary.GetBounds(next.Kind, next.Rotation);

            next.Col = Mathf.Clamp((CoarseCols - bounds.Width) / 2 - bounds.MinX, -bounds.MinX, CoarseCols - bounds.MaxX - 1);
            next.Row = CoarseRows - bounds.MaxY - 1;

            NextPiece = CreateQueuedPiece(random);
            if (Collides(next.Col, next.Row, next.Kind, next.Rotation))
            {
                CurrentPiece = null;
                return false;
            }

            CurrentPiece = next;
            return true;
        }

        public bool TryMoveHorizontal(int delta)
        {
            if (!CurrentPiece.HasValue)
            {
                return false;
            }

            ActivePiece piece = CurrentPiece.Value;
            int targetCol = piece.Col + delta;
            if (Collides(targetCol, piece.Row, piece.Kind, piece.Rotation))
            {
                return false;
            }

            piece.Col = targetCol;
            CurrentPiece = piece;
            return true;
        }

        public bool TryRotate()
        {
            if (!CurrentPiece.HasValue)
            {
                return false;
            }

            ActivePiece piece = CurrentPiece.Value;
            int targetRotation = (piece.Rotation + 1) & 3;
            int[] kicks = { 0, -1, 1, -2, 2 };

            for (int i = 0; i < kicks.Length; i++)
            {
                int candidateCol = piece.Col + kicks[i];
                if (Collides(candidateCol, piece.Row, piece.Kind, targetRotation))
                {
                    continue;
                }

                piece.Col = candidateCol;
                piece.Rotation = targetRotation;
                CurrentPiece = piece;
                return true;
            }

            return false;
        }

        public bool TryStepDown()
        {
            if (!CurrentPiece.HasValue)
            {
                return false;
            }

            ActivePiece piece = CurrentPiece.Value;
            int targetRow = piece.Row - 1;
            if (Collides(piece.Col, targetRow, piece.Kind, piece.Rotation))
            {
                return false;
            }

            piece.Row = targetRow;
            CurrentPiece = piece;
            return true;
        }

        public void LockCurrentPiece()
        {
            if (!CurrentPiece.HasValue)
            {
                return;
            }

            ActivePiece piece = CurrentPiece.Value;
            Vector2Int[] cells = TetrominoLibrary.GetCells(piece.Kind, piece.Rotation);

            for (int i = 0; i < cells.Length; i++)
            {
                Vector2Int cell = cells[i];
                int coarseX = piece.Col + cell.x;
                int coarseY = piece.Row + cell.y;
                int sandStartX = coarseX * GrainScale;
                int sandStartY = coarseY * GrainScale;

                for (int dx = 0; dx < GrainScale; dx++)
                {
                    int sandX = sandStartX + dx;
                    if (sandX < 0 || sandX >= SandCols)
                    {
                        continue;
                    }

                    for (int dy = 0; dy < GrainScale; dy++)
                    {
                        int sandY = sandStartY + dy;
                        if (sandY < 0 || sandY >= SandRows)
                        {
                            continue;
                        }

                        sandGrid[ToIndex(sandX, sandY)] = piece.Color;
                    }
                }
            }

            CurrentPiece = null;
        }

        public bool StepSand(System.Random random)
        {
            bool moved = false;
            for (int i = 0; i < SandCols; i++)
            {
                shuffleBuffer[i] = i;
            }

            for (int y = 1; y < SandRows; y++)
            {
                Shuffle(random, shuffleBuffer);
                for (int i = 0; i < SandCols; i++)
                {
                    int x = shuffleBuffer[i];
                    CellColor value = sandGrid[ToIndex(x, y)];
                    if (value == CellColor.Empty)
                    {
                        continue;
                    }

                    int belowIndex = ToIndex(x, y - 1);
                    if (sandGrid[belowIndex] == CellColor.Empty)
                    {
                        sandGrid[belowIndex] = value;
                        sandGrid[ToIndex(x, y)] = CellColor.Empty;
                        moved = true;
                        continue;
                    }

                    bool canLeft = x > 0 && sandGrid[ToIndex(x - 1, y - 1)] == CellColor.Empty;
                    bool canRight = x < SandCols - 1 && sandGrid[ToIndex(x + 1, y - 1)] == CellColor.Empty;
                    if (!canLeft && !canRight)
                    {
                        continue;
                    }

                    int targetX;
                    if (canLeft && canRight)
                    {
                        targetX = random.NextDouble() < 0.5d ? x - 1 : x + 1;
                    }
                    else
                    {
                        targetX = canLeft ? x - 1 : x + 1;
                    }

                    sandGrid[ToIndex(targetX, y - 1)] = value;
                    sandGrid[ToIndex(x, y)] = CellColor.Empty;
                    moved = true;
                }
            }

            return moved;
        }

        public IReadOnlyList<int> FindBridgeClearCells()
        {
            bridgeResult.Clear();
            bridgeClearSet.Clear();
            bridgeVisited.Clear();

            for (int y = 0; y < SandRows; y++)
            {
                int leftIndex = ToIndex(0, y);
                CellColor color = sandGrid[leftIndex];
                if (color == CellColor.Empty || bridgeVisited.Contains(leftIndex))
                {
                    continue;
                }

                bridgeComponent.Clear();
                bool touchesRight = false;
                bridgeStack.Clear();
                bridgeStack.Push(leftIndex);
                bridgeVisited.Add(leftIndex);

                while (bridgeStack.Count > 0)
                {
                    int index = bridgeStack.Pop();
                    bridgeComponent.Add(index);
                    int cx = index % SandCols;
                    int cy = index / SandCols;

                    if (cx == SandCols - 1)
                    {
                        touchesRight = true;
                    }

                    TryVisitNeighbor(cx - 1, cy, color, bridgeVisited, bridgeStack);
                    TryVisitNeighbor(cx + 1, cy, color, bridgeVisited, bridgeStack);
                    TryVisitNeighbor(cx, cy - 1, color, bridgeVisited, bridgeStack);
                    TryVisitNeighbor(cx, cy + 1, color, bridgeVisited, bridgeStack);
                }

                if (!touchesRight)
                {
                    continue;
                }

                for (int i = 0; i < bridgeComponent.Count; i++)
                {
                    if (bridgeClearSet.Add(bridgeComponent[i]))
                    {
                        bridgeResult.Add(bridgeComponent[i]);
                    }
                }
            }

            return bridgeResult;
        }

        public void ClearCells(IReadOnlyList<int> indices)
        {
            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                if (index >= 0 && index < sandGrid.Length)
                {
                    sandGrid[index] = CellColor.Empty;
                }
            }
        }

        public bool Collides(int col, int row, TetrominoKind kind, int rotation)
        {
            Vector2Int[] cells = TetrominoLibrary.GetCells(kind, rotation);
            for (int i = 0; i < cells.Length; i++)
            {
                Vector2Int cell = cells[i];
                int coarseX = col + cell.x;
                int coarseY = row + cell.y;
                int sandStartX = coarseX * GrainScale;
                int sandStartY = coarseY * GrainScale;

                if (sandStartX < 0 || sandStartX + GrainScale > SandCols)
                {
                    return true;
                }

                if (sandStartY < 0 || sandStartY + GrainScale > SandRows)
                {
                    return true;
                }

                for (int dx = 0; dx < GrainScale; dx++)
                {
                    for (int dy = 0; dy < GrainScale; dy++)
                    {
                        int sandX = sandStartX + dx;
                        int sandY = sandStartY + dy;
                        if (sandGrid[ToIndex(sandX, sandY)] != CellColor.Empty)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public int ToIndex(int x, int y)
        {
            return (y * SandCols) + x;
        }

        public bool IsInsideSand(int x, int y)
        {
            return x >= 0 && x < SandCols && y >= 0 && y < SandRows;
        }

        private ActivePiece CreateQueuedPiece(System.Random random)
        {
            if (pieceBag.Count == 0)
            {
                RefillBag(random);
            }

            return new ActivePiece
            {
                Kind = pieceBag.Dequeue(),
                Color = TetrominoLibrary.RandomColor(random),
                Rotation = 0,
                Col = 0,
                Row = 0,
            };
        }

        private void RefillBag(System.Random random)
        {
            List<TetrominoKind> values = new()
            {
                TetrominoKind.I,
                TetrominoKind.O,
                TetrominoKind.T,
                TetrominoKind.S,
                TetrominoKind.Z,
                TetrominoKind.J,
                TetrominoKind.L,
            };

            for (int i = values.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
            }

            for (int i = 0; i < values.Count; i++)
            {
                pieceBag.Enqueue(values[i]);
            }
        }

        private void TryVisitNeighbor(int x, int y, CellColor targetColor, HashSet<int> visited, Stack<int> stack)
        {
            if (!IsInsideSand(x, y))
            {
                return;
            }

            int index = ToIndex(x, y);
            if (visited.Contains(index) || sandGrid[index] != targetColor)
            {
                return;
            }

            visited.Add(index);
            stack.Push(index);
        }

        private static void Shuffle(System.Random random, int[] buffer)
        {
            for (int i = buffer.Length - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (buffer[i], buffer[swapIndex]) = (buffer[swapIndex], buffer[i]);
            }
        }
    }
}
