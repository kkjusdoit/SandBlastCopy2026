using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlowSand.Core
{
    public sealed class FlowSandBoard
    {
        private static readonly int[] RotationKicks = { 0, -1, 1, -2, 2 };

        private readonly CellColor[] sandGrid;
        private readonly TetrominoKind[] pieceBag = new TetrominoKind[7];
        private readonly byte[] sizeBag = new byte[10];
        private readonly int[] bridgeVisitStamps;
        private readonly int[] bridgeComponent;
        private readonly int[] bridgeStack;
        private readonly List<int> bridgeResult;

        private int pieceBagIndex;
        private int sizeBagIndex;
        private int bridgeVisitStamp;
        private int sandStepCount;

        public FlowSandBoard(int coarseCols, int coarseRows, int grainScale)
        {
            CoarseCols = coarseCols;
            CoarseRows = coarseRows;
            GrainScale = grainScale;
            SandCols = coarseCols * grainScale;
            SandRows = coarseRows * grainScale;

            int cellCount = SandCols * SandRows;
            sandGrid = new CellColor[cellCount];
            bridgeVisitStamps = new int[cellCount];
            bridgeComponent = new int[cellCount];
            bridgeStack = new int[cellCount];
            bridgeResult = new List<int>(cellCount);
            pieceBagIndex = pieceBag.Length;
            sizeBagIndex = sizeBag.Length;
        }

        public int CoarseCols { get; }
        public int CoarseRows { get; }
        public int GrainScale { get; }
        public int SandCols { get; }
        public int SandRows { get; }
        public int CellCount => sandGrid.Length;
        public ActivePiece? CurrentPiece { get; private set; }
        public ActivePiece NextPiece { get; private set; }
        public int BridgeScanCount { get; private set; }

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
            pieceBagIndex = pieceBag.Length;
            sizeBagIndex = sizeBag.Length;
            sandStepCount = 0;
            CurrentPiece = null;
            NextPiece = CreateQueuedPiece(random);
        }

        public bool SpawnNextPiece(System.Random random)
        {
            ActivePiece next = NextPiece;
            BoardBounds bounds = TetrominoLibrary.GetBounds(next.Kind, next.Rotation);

            next.Col = Mathf.Clamp((CoarseCols - bounds.Width) / 2 - bounds.MinX, -bounds.MinX, CoarseCols - bounds.MaxX - 1);
            // Spawn with the lowest occupied row at the visible ceiling. The rest of
            // the piece enters from above instead of reserving empty rows in the board.
            next.Row = CoarseRows - bounds.MinY - 1;

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
            for (int i = 0; i < RotationKicks.Length; i++)
            {
                int candidateCol = piece.Col + RotationKicks[i];
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
            int fineDropDistance = GetFineLockDropDistance(piece, cells);

            for (int i = 0; i < cells.Length; i++)
            {
                Vector2Int cell = cells[i];
                int coarseX = piece.Col + cell.x;
                int coarseY = piece.Row + cell.y;
                int sandStartX = coarseX * GrainScale;
                int sandStartY = (coarseY * GrainScale) - fineDropDistance;

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
            sandStepCount += 1;

            for (int y = 1; y < SandRows; y++)
            {
                int rowOffset = y * SandCols;
                int belowRowOffset = rowOffset - SandCols;
                int startX = GetRowStart(y);
                int stride = GetCoprimeStride(y);
                int x = startX;

                for (int visited = 0; visited < SandCols; visited++)
                {
                    int sourceIndex = rowOffset + x;
                    CellColor value = sandGrid[sourceIndex];
                    if (value == CellColor.Empty)
                    {
                        x += stride;
                        if (x >= SandCols)
                        {
                            x -= SandCols;
                        }

                        continue;
                    }

                    int belowIndex = belowRowOffset + x;
                    if (sandGrid[belowIndex] == CellColor.Empty)
                    {
                        sandGrid[belowIndex] = value;
                        sandGrid[sourceIndex] = CellColor.Empty;
                        moved = true;
                    }
                    else
                    {
                        bool canLeft = x > 0 && sandGrid[belowIndex - 1] == CellColor.Empty;
                        bool canRight = x < SandCols - 1 && sandGrid[belowIndex + 1] == CellColor.Empty;
                        if (canLeft || canRight)
                        {
                            int targetX;
                            if (canLeft && canRight)
                            {
                                targetX = random.Next(2) == 0 ? x - 1 : x + 1;
                            }
                            else
                            {
                                targetX = canLeft ? x - 1 : x + 1;
                            }

                            sandGrid[belowRowOffset + targetX] = value;
                            sandGrid[sourceIndex] = CellColor.Empty;
                            moved = true;
                        }
                    }

                    x += stride;
                    if (x >= SandCols)
                    {
                        x -= SandCols;
                    }
                }
            }

            return moved;
        }

        private int GetRowStart(int y)
        {
            uint hash = ((uint)y * 19349663u) ^ ((uint)sandStepCount * 83492791u);
            hash ^= hash >> 13;
            return (int)(hash % (uint)SandCols);
        }

        private int GetCoprimeStride(int y)
        {
            if (SandCols <= 2)
            {
                return 1;
            }

            uint hash = ((uint)y * 73856093u) ^ ((uint)sandStepCount * 2654435761u);
            int stride = 1 + (int)(hash % (uint)(SandCols - 1));
            while (GreatestCommonDivisor(stride, SandCols) != 1)
            {
                stride += 1;
                if (stride >= SandCols)
                {
                    stride = 1;
                }
            }

            return stride;
        }

        private static int GreatestCommonDivisor(int a, int b)
        {
            while (b != 0)
            {
                int remainder = a % b;
                a = b;
                b = remainder;
            }

            return a;
        }

        private int GetFineLockDropDistance(ActivePiece piece, Vector2Int[] cells)
        {
            int fineDropDistance = 0;
            for (int candidate = 1; candidate < GrainScale; candidate++)
            {
                if (CollidesAtFineDrop(piece, cells, candidate))
                {
                    break;
                }

                fineDropDistance = candidate;
            }

            return fineDropDistance;
        }

        private bool CollidesAtFineDrop(ActivePiece piece, Vector2Int[] cells, int fineDropDistance)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                Vector2Int cell = cells[i];
                int sandStartX = (piece.Col + cell.x) * GrainScale;
                int sandStartY = ((piece.Row + cell.y) * GrainScale) - fineDropDistance;

                for (int dx = 0; dx < GrainScale; dx++)
                {
                    for (int dy = 0; dy < GrainScale; dy++)
                    {
                        int sandY = sandStartY + dy;
                        if (sandY < 0)
                        {
                            return true;
                        }

                        if (sandY >= SandRows)
                        {
                            continue;
                        }

                        int sandX = sandStartX + dx;
                        if (sandGrid[ToIndex(sandX, sandY)] != CellColor.Empty)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public IReadOnlyList<int> FindBridgeClearCells()
        {
            BridgeScanCount += 1;
            bridgeResult.Clear();
            int visitStamp = NextBridgeVisitStamp();

            for (int y = 0; y < SandRows; y++)
            {
                int leftIndex = ToIndex(0, y);
                CellColor color = sandGrid[leftIndex];
                if (color == CellColor.Empty || bridgeVisitStamps[leftIndex] == visitStamp)
                {
                    continue;
                }

                int componentCount = 0;
                int stackCount = 0;
                bool touchesRight = false;
                bridgeStack[stackCount++] = leftIndex;
                bridgeVisitStamps[leftIndex] = visitStamp;

                while (stackCount > 0)
                {
                    int index = bridgeStack[--stackCount];
                    bridgeComponent[componentCount++] = index;
                    int cx = index % SandCols;
                    int cy = index / SandCols;

                    if (cx == SandCols - 1)
                    {
                        touchesRight = true;
                    }

                    if (cx > 0)
                    {
                        TryVisitNeighbor(index - 1, color, visitStamp, ref stackCount);
                    }

                    if (cx < SandCols - 1)
                    {
                        TryVisitNeighbor(index + 1, color, visitStamp, ref stackCount);
                    }

                    if (cy > 0)
                    {
                        TryVisitNeighbor(index - SandCols, color, visitStamp, ref stackCount);
                    }

                    if (cy < SandRows - 1)
                    {
                        TryVisitNeighbor(index + SandCols, color, visitStamp, ref stackCount);
                    }
                }

                if (!touchesRight)
                {
                    continue;
                }

                for (int i = 0; i < componentCount; i++)
                {
                    bridgeResult.Add(bridgeComponent[i]);
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

                if (sandStartY < 0)
                {
                    return true;
                }

                if (sandStartY >= SandRows)
                {
                    continue;
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

        internal CellColor GetSandByIndex(int index)
        {
            return sandGrid[index];
        }

        private ActivePiece CreateQueuedPiece(System.Random random)
        {
            if (sizeBagIndex >= sizeBag.Length)
            {
                RefillSizeBag(random);
            }

            byte size = sizeBag[sizeBagIndex++];

            return new ActivePiece
            {
                Kind = size == 1
                    ? TetrominoKind.Mono
                    : size == 2
                        ? TetrominoKind.Domino
                        : TakeTetrominoFromBag(random),
                Color = TetrominoLibrary.RandomColor(random),
                Rotation = 0,
                Col = 0,
                Row = 0,
            };
        }

        private TetrominoKind TakeTetrominoFromBag(System.Random random)
        {
            if (pieceBagIndex >= pieceBag.Length)
            {
                RefillPieceBag(random);
            }

            return pieceBag[pieceBagIndex++];
        }

        private void RefillPieceBag(System.Random random)
        {
            pieceBag[0] = TetrominoKind.I;
            pieceBag[1] = TetrominoKind.O;
            pieceBag[2] = TetrominoKind.T;
            pieceBag[3] = TetrominoKind.S;
            pieceBag[4] = TetrominoKind.Z;
            pieceBag[5] = TetrominoKind.J;
            pieceBag[6] = TetrominoKind.L;

            for (int i = pieceBag.Length - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (pieceBag[i], pieceBag[swapIndex]) = (pieceBag[swapIndex], pieceBag[i]);
            }

            pieceBagIndex = 0;
        }

        private void RefillSizeBag(System.Random random)
        {
            // Each ten-piece cycle contains 60% tetrominoes, 30% dominoes,
            // and 10% monominoes, while shuffling their order independently.
            for (int i = 0; i < sizeBag.Length; i++)
            {
                sizeBag[i] = i < 6 ? (byte)4 : i < 9 ? (byte)2 : (byte)1;
            }

            for (int i = sizeBag.Length - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (sizeBag[i], sizeBag[swapIndex]) = (sizeBag[swapIndex], sizeBag[i]);
            }

            sizeBagIndex = 0;
        }

        private void TryVisitNeighbor(int index, CellColor targetColor, int visitStamp, ref int stackCount)
        {
            if (bridgeVisitStamps[index] == visitStamp || sandGrid[index] != targetColor)
            {
                return;
            }

            bridgeVisitStamps[index] = visitStamp;
            bridgeStack[stackCount++] = index;
        }

        private int NextBridgeVisitStamp()
        {
            if (bridgeVisitStamp == int.MaxValue)
            {
                Array.Clear(bridgeVisitStamps, 0, bridgeVisitStamps.Length);
                bridgeVisitStamp = 1;
            }
            else
            {
                bridgeVisitStamp += 1;
            }

            return bridgeVisitStamp;
        }
    }
}
