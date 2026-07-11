using System;
using FlowSand.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FlowSand.Runtime
{
    public sealed class FlowSandBoardRenderer : IDisposable
    {
        private const int ShadeVariants = 18;
        private static readonly Color32 FlashColor = new(255, 255, 255, 255);

        private readonly FlowSandBoard board;
        private readonly Color32[] palette;
        private readonly Color32[] grainColors;
        private readonly Color32 backgroundColor;
        private readonly Color32 borderColor;
        private readonly Texture2D boardTexture;
        private readonly Texture2D nextTexture;
        private NativeArray<Color32> boardPixels;
        private NativeArray<Color32> nextPixels;

        public FlowSandBoardRenderer(FlowSandBoard board, RawImage boardImage, RawImage nextImage, Color32[] palette, Color32 backgroundColor, Color32 borderColor)
        {
            this.board = board;
            this.palette = palette;
            this.backgroundColor = backgroundColor;
            this.borderColor = borderColor;
            grainColors = BuildGrainColors(palette);

            boardTexture = new Texture2D(board.SandCols + 2, board.SandRows + 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            boardPixels = boardTexture.GetRawTextureData<Color32>();
            boardImage.texture = boardTexture;

            nextTexture = new Texture2D(34, 34, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            nextPixels = nextTexture.GetRawTextureData<Color32>();
            nextImage.texture = nextTexture;
        }

        public void RedrawBoard(bool[] flashingCells, bool flashVisible)
        {
            int width = boardTexture.width;
            int height = boardTexture.height;
            Fill(boardPixels, backgroundColor);

            for (int x = 0; x < width; x++)
            {
                boardPixels[x] = borderColor;
                boardPixels[((height - 1) * width) + x] = borderColor;
            }

            for (int y = 0; y < height; y++)
            {
                boardPixels[y * width] = borderColor;
                boardPixels[(y * width) + width - 1] = borderColor;
            }

            for (int y = 0; y < board.SandRows; y++)
            {
                for (int x = 0; x < board.SandCols; x++)
                {
                    int index = board.ToIndex(x, y);
                    CellColor cell = board.GetSandByIndex(index);
                    if (cell == CellColor.Empty)
                    {
                        continue;
                    }

                    bool isFlashing = index < flashingCells.Length && flashingCells[index];
                    Color32 color = GetGrainColor(cell, x, y, isFlashing, flashVisible);
                    SetBoardPixel(x + 1, y + 1, color);
                }
            }

            if (board.CurrentPiece.HasValue)
            {
                DrawActivePiece(board.CurrentPiece.Value);
            }

            boardTexture.Apply(false, false);
        }

        public void RedrawNext()
        {
            Fill(nextPixels, new Color32(17, 19, 36, 255));
            int width = nextTexture.width;
            int height = nextTexture.height;

            for (int x = 0; x < width; x++)
            {
                nextPixels[x] = borderColor;
                nextPixels[((height - 1) * width) + x] = borderColor;
            }

            for (int y = 0; y < height; y++)
            {
                nextPixels[y * width] = borderColor;
                nextPixels[(y * width) + width - 1] = borderColor;
            }

            ActivePiece next = board.NextPiece;
            Vector2Int[] cells = TetrominoLibrary.GetCells(next.Kind, 0);
            BoardBounds bounds = TetrominoLibrary.GetBounds(next.Kind, 0);
            int offsetX = ((width - 2) - (bounds.Width * 6)) / 2 - (bounds.MinX * 6);
            int offsetY = ((height - 2) - (bounds.Height * 6)) / 2 - (bounds.MinY * 6);
            Color32 color = palette[(int)next.Color];

            for (int i = 0; i < cells.Length; i++)
            {
                for (int dx = 0; dx < 6; dx++)
                {
                    for (int dy = 0; dy < 6; dy++)
                    {
                        int px = 1 + offsetX + (cells[i].x * 6) + dx;
                        int py = 1 + offsetY + (cells[i].y * 6) + dy;
                        if (px <= 0 || px >= width - 1 || py <= 0 || py >= height - 1)
                        {
                            continue;
                        }

                        nextPixels[(py * width) + px] = color;
                    }
                }
            }

            nextTexture.Apply(false, false);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(boardTexture);
            UnityEngine.Object.Destroy(nextTexture);
        }

        private void DrawActivePiece(ActivePiece piece)
        {
            Vector2Int[] cells = TetrominoLibrary.GetCells(piece.Kind, piece.Rotation);
            Color32 pieceColor = palette[(int)piece.Color];

            for (int i = 0; i < cells.Length; i++)
            {
                Vector2Int cell = cells[i];
                int startX = (piece.Col + cell.x) * board.GrainScale;
                int startY = (piece.Row + cell.y) * board.GrainScale;

                for (int dx = 0; dx < board.GrainScale; dx++)
                {
                    for (int dy = 0; dy < board.GrainScale; dy++)
                    {
                        if (!board.IsInsideSand(startX + dx, startY + dy))
                        {
                            continue;
                        }

                        byte shadeBoost = (byte)(((dx + dy) % 3) * 5);
                        Color32 shaded = new(
                            (byte)Mathf.Clamp(pieceColor.r + shadeBoost, 0, 255),
                            (byte)Mathf.Clamp(pieceColor.g + shadeBoost, 0, 255),
                            (byte)Mathf.Clamp(pieceColor.b + shadeBoost, 0, 255),
                            255);
                        SetBoardPixel(startX + dx + 1, startY + dy + 1, shaded);
                    }
                }
            }
        }

        private Color32 GetGrainColor(CellColor cell, int x, int y, bool flashing, bool flashVisible)
        {
            Color32 baseColor = palette[(int)cell];
            if (flashing)
            {
                return flashVisible ? FlashColor : baseColor;
            }

            int jitter = ((x * 13) + (y * 7)) % 18;
            return grainColors[((int)cell * ShadeVariants) + jitter];
        }

        private void SetBoardPixel(int x, int y, Color32 color)
        {
            boardPixels[(y * boardTexture.width) + x] = color;
        }

        private static void Fill(NativeArray<Color32> pixels, Color32 color)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
        }

        private static Color32[] BuildGrainColors(Color32[] palette)
        {
            Color32[] colors = new Color32[palette.Length * ShadeVariants];
            for (int cell = 0; cell < palette.Length; cell++)
            {
                Color32 baseColor = palette[cell];
                for (int jitter = 0; jitter < ShadeVariants; jitter++)
                {
                    int lift = jitter - 8;
                    colors[(cell * ShadeVariants) + jitter] = new Color32(
                        (byte)Mathf.Clamp(baseColor.r + lift, 0, 255),
                        (byte)Mathf.Clamp(baseColor.g + lift, 0, 255),
                        (byte)Mathf.Clamp(baseColor.b + lift, 0, 255),
                        255);
                }
            }

            return colors;
        }
    }
}
