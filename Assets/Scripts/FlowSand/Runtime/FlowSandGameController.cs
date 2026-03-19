using System;
using System.Collections.Generic;
using FlowSand.Audio;
using FlowSand.Core;
using FlowSand.UI;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FlowSand.Runtime
{
    public sealed class FlowSandGameController : MonoBehaviour
    {
        private const int CoarseCols = 10;
        private const int CoarseRows = 20;
        private const int GrainScale = 8;
        private const string HighScoreKey = "FlowSand.HighScore";

        private readonly Color32 backgroundColor = new(18, 20, 44, 255);
        private readonly Color32 borderColor = new(62, 201, 255, 255);
        private readonly Dictionary<CellColor, Color32> palette = new()
        {
            { CellColor.Empty, new Color32(18, 20, 44, 255) },
            { CellColor.Coral, new Color32(255, 73, 124, 255) },
            { CellColor.Mint, new Color32(79, 220, 124, 255) },
            { CellColor.Gold, new Color32(255, 202, 64, 255) },
            { CellColor.Sky, new Color32(69, 164, 241, 255) },
            { CellColor.Violet, new Color32(162, 111, 255, 255) },
        };

        private FlowSandBoard board;
        private System.Random random;
        private Texture2D boardTexture;
        private Texture2D nextTexture;
        private Color32[] boardPixels;
        private Color32[] nextPixels;
        private RawImage boardImage;
        private RawImage nextImage;
        private Text titleText;
        private Text subtitleText;
        private Text scoreText;
        private Text highScoreText;
        private Text levelText;
        private Text messageText;
        private Text startButtonText;
        private Text pauseButtonText;
        private GameObject overlayPanel;
        private GameObject pauseDimmer;
        private Button startButton;
        private Button pauseButton;
        private FlowSandSfxPlayer sfxPlayer;
        private Font uiFont;

        private GamePhase phase = GamePhase.Title;
        private float pieceFallTimer;
        private float sandTimer;
        private float clearTimer;
        private bool softDropHeld;
        private int score;
        private int highScore;
        private int combo;
        private float elapsedTime;
        private readonly List<int> pendingClearIndices = new();
        private readonly HashSet<int> pendingClearLookup = new();
        private bool flashVisible = true;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            random = new System.Random();
            board = new FlowSandBoard(CoarseCols, CoarseRows, GrainScale);
            highScore = PlayerPrefs.GetInt(HighScoreKey, 0);

            EnsureEventSystem();
            ConfigureCamera();
            BuildRuntimeUi();

            sfxPlayer = gameObject.AddComponent<FlowSandSfxPlayer>();

            int boardWidth = board.SandCols + 2;
            int boardHeight = board.SandRows + 2;
            boardTexture = new Texture2D(boardWidth, boardHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            boardPixels = new Color32[boardWidth * boardHeight];
            boardImage.texture = boardTexture;

            nextTexture = new Texture2D(34, 34, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            nextPixels = new Color32[nextTexture.width * nextTexture.height];
            nextImage.texture = nextTexture;

            ShowTitleScreen();
            RefreshAllVisuals();
        }

        private void Update()
        {
            HandleKeyboardShortcuts();
            if (phase != GamePhase.Playing)
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            elapsedTime += deltaTime;

            UpdatePieceFall(deltaTime);
            UpdateSand(deltaTime);
            UpdateClears(deltaTime);

            if (!board.HasActivePiece && pendingClearIndices.Count == 0)
            {
                SpawnNextPieceOrEnd();
            }

            RefreshHud();
            RedrawBoard();
        }

        private void HandleKeyboardShortcuts()
        {
            if (Input.GetKeyDown(KeyCode.Return) && phase is GamePhase.Title or GamePhase.GameOver)
            {
                StartGame();
            }

            if (Input.GetKeyDown(KeyCode.P) && phase is GamePhase.Playing or GamePhase.Paused)
            {
                TogglePause();
            }

            if (phase != GamePhase.Playing)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                TryMove(-1);
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                TryMove(1);
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space))
            {
                TryRotate();
            }

            softDropHeld = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
        }

        private void UpdatePieceFall(float deltaTime)
        {
            if (!board.HasActivePiece || pendingClearIndices.Count > 0)
            {
                return;
            }

            pieceFallTimer += deltaTime;
            float stepInterval = GetCurrentDropInterval();
            if (softDropHeld)
            {
                stepInterval *= 0.16f;
            }

            while (pieceFallTimer >= stepInterval)
            {
                pieceFallTimer -= stepInterval;
                if (board.TryStepDown())
                {
                    continue;
                }

                board.LockCurrentPiece();
                combo = 0;
                sfxPlayer.PlayLock();
                break;
            }
        }

        private void UpdateSand(float deltaTime)
        {
            sandTimer += deltaTime;
            const float sandStepInterval = 0.035f;

            while (sandTimer >= sandStepInterval)
            {
                sandTimer -= sandStepInterval;
                board.StepSand(random);
            }
        }

        private void UpdateClears(float deltaTime)
        {
            if (pendingClearIndices.Count > 0)
            {
                clearTimer -= deltaTime;
                flashVisible = Mathf.FloorToInt(clearTimer / 0.08f) % 2 == 0;
                if (clearTimer > 0f)
                {
                    return;
                }

                board.ClearCells(pendingClearIndices);
                int cleared = pendingClearIndices.Count;
                pendingClearIndices.Clear();
                pendingClearLookup.Clear();
                combo += 1;
                score += cleared * 2 * combo;
                if (score > highScore)
                {
                    highScore = score;
                    PlayerPrefs.SetInt(HighScoreKey, highScore);
                    PlayerPrefs.Save();
                }

                sfxPlayer.PlayClear();
                return;
            }

            IReadOnlyList<int> clearCells = board.FindBridgeClearCells();
            if (clearCells.Count == 0)
            {
                flashVisible = true;
                return;
            }

            pendingClearIndices.Clear();
            pendingClearLookup.Clear();
            for (int i = 0; i < clearCells.Count; i++)
            {
                int index = clearCells[i];
                pendingClearIndices.Add(index);
                pendingClearLookup.Add(index);
            }
            clearTimer = 0.28f;
            flashVisible = true;
        }

        private float GetCurrentDropInterval()
        {
            float difficulty = Mathf.Clamp01((elapsedTime / 120f) + (score / 6000f));
            return Mathf.Lerp(0.7f, 0.17f, difficulty);
        }

        private void StartGame()
        {
            board.Reset(random);
            score = 0;
            combo = 0;
            elapsedTime = 0f;
            pieceFallTimer = 0f;
            sandTimer = 0f;
            clearTimer = 0f;
            pendingClearIndices.Clear();
            pendingClearLookup.Clear();
            flashVisible = true;
            softDropHeld = false;
            phase = GamePhase.Playing;

            pauseDimmer.SetActive(false);
            overlayPanel.SetActive(false);
            pauseButton.gameObject.SetActive(true);

            SpawnNextPieceOrEnd();
            sfxPlayer.PlayStart();
            RefreshAllVisuals();
        }

        private void SpawnNextPieceOrEnd()
        {
            if (board.HasActivePiece)
            {
                return;
            }

            if (board.SpawnNextPiece(random))
            {
                return;
            }

            phase = GamePhase.GameOver;
            pauseButton.gameObject.SetActive(false);
            pauseDimmer.SetActive(true);
            overlayPanel.SetActive(true);
            titleText.text = "ROUND OVER";
            subtitleText.text = "The sand pile blocked the spawn lane.\nTap to rebuild the board.";
            startButtonText.text = "RESTART";
            messageText.text = $"Final Score {score}\nBest {highScore}";
            sfxPlayer.PlayGameOver();
        }

        private void TogglePause()
        {
            if (phase == GamePhase.Title || phase == GamePhase.GameOver)
            {
                return;
            }

            if (phase == GamePhase.Playing)
            {
                phase = GamePhase.Paused;
                pauseDimmer.SetActive(true);
                overlayPanel.SetActive(true);
                titleText.text = "PAUSED";
                subtitleText.text = "Flow keeps its state. Jump back in when ready.";
                messageText.text = "Press P or tap Resume";
                startButtonText.text = "RESUME";
                pauseButtonText.text = "Resume";
                return;
            }

            phase = GamePhase.Playing;
            pauseDimmer.SetActive(false);
            overlayPanel.SetActive(false);
            pauseButtonText.text = "Pause";
        }

        private void TryMove(int delta)
        {
            if (phase != GamePhase.Playing || pendingClearIndices.Count > 0)
            {
                return;
            }

            if (board.TryMoveHorizontal(delta))
            {
                sfxPlayer.PlayMove();
                RedrawBoard();
            }
        }

        private void TryRotate()
        {
            if (phase != GamePhase.Playing || pendingClearIndices.Count > 0)
            {
                return;
            }

            if (board.TryRotate())
            {
                sfxPlayer.PlayRotate();
                RedrawBoard();
            }
        }

        private void ShowTitleScreen()
        {
            phase = GamePhase.Title;
            pauseDimmer.SetActive(true);
            overlayPanel.SetActive(true);
            pauseButton.gameObject.SetActive(false);
            titleText.text = "FLOW SAND\nTETRIS";
            subtitleText.text = "Drop blocks, let them crumble into sand,\nand bridge one color from left to right.";
            messageText.text = "Mobile portrait prototype\nButtons: move, rotate, soft drop";
            startButtonText.text = "START";
        }

        private void RefreshAllVisuals()
        {
            RefreshHud();
            RedrawBoard();
            RedrawNext();
        }

        private void RefreshHud()
        {
            scoreText.text = $"Score\n{score}";
            highScoreText.text = $"Best\n{highScore}";
            levelText.text = $"Speed\n{Mathf.RoundToInt(Mathf.Lerp(1f, 10f, 1f - GetCurrentDropInterval() / 0.7f))}";
            RedrawNext();
        }

        private void RedrawBoard()
        {
            int width = boardTexture.width;
            int height = boardTexture.height;
            Array.Fill(boardPixels, backgroundColor);

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
                    CellColor cell = board.GetSand(x, y);
                    if (cell == CellColor.Empty)
                    {
                        continue;
                    }

                    int index = board.ToIndex(x, y);
                    bool isFlashing = pendingClearLookup.Contains(index);
                    Color32 color = GetGrainColor(cell, x, y, isFlashing);
                    SetBoardPixel(x + 1, y + 1, color);
                }
            }

            if (board.CurrentPiece.HasValue)
            {
                ActivePiece piece = board.CurrentPiece.Value;
                Vector2Int[] cells = TetrominoLibrary.GetCells(piece.Kind, piece.Rotation);
                Color32 pieceColor = palette[piece.Color];

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

            boardTexture.SetPixels32(boardPixels);
            boardTexture.Apply(false, false);
        }

        private void RedrawNext()
        {
            Array.Fill(nextPixels, new Color32(17, 19, 36, 255));
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
            Color32 color = palette[next.Color];

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

            nextTexture.SetPixels32(nextPixels);
            nextTexture.Apply(false, false);
        }

        private Color32 GetGrainColor(CellColor cell, int x, int y, bool flashing)
        {
            Color32 baseColor = palette[cell];
            if (flashing)
            {
                return flashVisible ? new Color32(255, 255, 255, 255) : baseColor;
            }

            int jitter = ((x * 13) + (y * 7)) % 18;
            int lift = jitter - 8;
            return new Color32(
                (byte)Mathf.Clamp(baseColor.r + lift, 0, 255),
                (byte)Mathf.Clamp(baseColor.g + lift, 0, 255),
                (byte)Mathf.Clamp(baseColor.b + lift, 0, 255),
                255);
        }

        private void SetBoardPixel(int x, int y, Color32 color)
        {
            int width = boardTexture.width;
            boardPixels[(y * width) + x] = color;
        }

        private void ConfigureCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 5f;
            mainCamera.backgroundColor = new Color32(7, 9, 20, 255);
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
        }

        private void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemGo = new("EventSystem");
            eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildRuntimeUi()
        {
            GameObject canvasGo = new("FlowSandCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject root = CreatePanel("Root", canvas.transform, new Color(0.03f, 0.04f, 0.1f, 1f));
            SetStretch((RectTransform)root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            pauseDimmer = CreatePanel("Dimmer", canvas.transform, new Color(0f, 0f, 0f, 0.62f));
            SetStretch((RectTransform)pauseDimmer.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            CreateBoardFrame(root.transform);
            CreateTopHud(root.transform);
            CreateControls(root.transform);
            CreateOverlay(canvas.transform);
        }

        private void CreateBoardFrame(Transform parent)
        {
            GameObject frame = CreatePanel("BoardFrame", parent, new Color32(11, 14, 29, 255));
            RectTransform frameRect = (RectTransform)frame.transform;
            frameRect.anchorMin = new Vector2(0.09f, 0.22f);
            frameRect.anchorMax = new Vector2(0.71f, 0.88f);
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;

            Outline outline = frame.AddComponent<Outline>();
            outline.effectColor = new Color32(55, 206, 255, 255);
            outline.effectDistance = new Vector2(4f, -4f);

            GameObject boardGo = new("BoardTexture");
            boardGo.transform.SetParent(frame.transform, false);
            boardImage = boardGo.AddComponent<RawImage>();
            boardImage.color = Color.white;
            RectTransform boardRect = boardImage.rectTransform;
            boardRect.anchorMin = new Vector2(0.04f, 0.03f);
            boardRect.anchorMax = new Vector2(0.96f, 0.97f);
            boardRect.offsetMin = Vector2.zero;
            boardRect.offsetMax = Vector2.zero;
        }

        private void CreateTopHud(Transform parent)
        {
            scoreText = CreateText("Score", parent, 48, TextAnchor.UpperLeft);
            RectTransform scoreRect = scoreText.rectTransform;
            scoreRect.anchorMin = new Vector2(0.08f, 0.88f);
            scoreRect.anchorMax = new Vector2(0.32f, 0.98f);
            scoreRect.offsetMin = Vector2.zero;
            scoreRect.offsetMax = Vector2.zero;

            highScoreText = CreateText("Best", parent, 36, TextAnchor.UpperLeft);
            RectTransform bestRect = highScoreText.rectTransform;
            bestRect.anchorMin = new Vector2(0.08f, 0.82f);
            bestRect.anchorMax = new Vector2(0.32f, 0.88f);
            bestRect.offsetMin = Vector2.zero;
            bestRect.offsetMax = Vector2.zero;

            levelText = CreateText("Speed", parent, 36, TextAnchor.UpperLeft);
            RectTransform levelRect = levelText.rectTransform;
            levelRect.anchorMin = new Vector2(0.72f, 0.82f);
            levelRect.anchorMax = new Vector2(0.92f, 0.88f);
            levelRect.offsetMin = Vector2.zero;
            levelRect.offsetMax = Vector2.zero;

            Text nextLabel = CreateText("NEXT", parent, 34, TextAnchor.MiddleCenter);
            RectTransform nextLabelRect = nextLabel.rectTransform;
            nextLabelRect.anchorMin = new Vector2(0.74f, 0.9f);
            nextLabelRect.anchorMax = new Vector2(0.92f, 0.95f);
            nextLabelRect.offsetMin = Vector2.zero;
            nextLabelRect.offsetMax = Vector2.zero;

            GameObject previewFrame = CreatePanel("NextFrame", parent, new Color32(10, 12, 28, 255));
            RectTransform previewRect = (RectTransform)previewFrame.transform;
            previewRect.anchorMin = new Vector2(0.75f, 0.74f);
            previewRect.anchorMax = new Vector2(0.91f, 0.88f);
            previewRect.offsetMin = Vector2.zero;
            previewRect.offsetMax = Vector2.zero;
            previewFrame.AddComponent<Outline>().effectColor = new Color32(55, 206, 255, 255);

            GameObject nextGo = new("NextTexture");
            nextGo.transform.SetParent(previewFrame.transform, false);
            nextImage = nextGo.AddComponent<RawImage>();
            RectTransform nextRect = nextImage.rectTransform;
            nextRect.anchorMin = new Vector2(0.1f, 0.1f);
            nextRect.anchorMax = new Vector2(0.9f, 0.9f);
            nextRect.offsetMin = Vector2.zero;
            nextRect.offsetMax = Vector2.zero;

            pauseButton = CreateButton("PauseButton", parent, "Pause", TogglePause);
            pauseButtonText = pauseButton.GetComponentInChildren<Text>();
            RectTransform pauseRect = (RectTransform)pauseButton.transform;
            pauseRect.anchorMin = new Vector2(0.74f, 0.93f);
            pauseRect.anchorMax = new Vector2(0.92f, 0.985f);
            pauseRect.offsetMin = Vector2.zero;
            pauseRect.offsetMax = Vector2.zero;
        }

        private void CreateControls(Transform parent)
        {
            CreateHoldButton("LeftButton", parent, "LEFT", new Vector2(0.08f, 0.05f), new Vector2(0.29f, 0.17f), () => TryMove(-1), () => TryMove(-1), null);
            CreateHoldButton("RightButton", parent, "RIGHT", new Vector2(0.31f, 0.05f), new Vector2(0.52f, 0.17f), () => TryMove(1), () => TryMove(1), null);
            CreateHoldButton("DropButton", parent, "DROP", new Vector2(0.74f, 0.05f), new Vector2(0.92f, 0.17f), () => softDropHeld = true, null, () => softDropHeld = false);

            Button rotateButton = CreateButton("RotateButton", parent, "ROTATE", TryRotate);
            RectTransform rotateRect = (RectTransform)rotateButton.transform;
            rotateRect.anchorMin = new Vector2(0.53f, 0.05f);
            rotateRect.anchorMax = new Vector2(0.73f, 0.17f);
            rotateRect.offsetMin = Vector2.zero;
            rotateRect.offsetMax = Vector2.zero;
        }

        private void CreateOverlay(Transform parent)
        {
            overlayPanel = CreatePanel("Overlay", parent, new Color32(12, 16, 33, 235));
            RectTransform overlayRect = (RectTransform)overlayPanel.transform;
            overlayRect.anchorMin = new Vector2(0.12f, 0.22f);
            overlayRect.anchorMax = new Vector2(0.88f, 0.78f);
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Outline outline = overlayPanel.AddComponent<Outline>();
            outline.effectColor = new Color32(55, 206, 255, 255);
            outline.effectDistance = new Vector2(4f, -4f);

            titleText = CreateText("Title", overlayPanel.transform, 86, TextAnchor.MiddleCenter);
            RectTransform titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0.08f, 0.63f);
            titleRect.anchorMax = new Vector2(0.92f, 0.92f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            subtitleText = CreateText("Subtitle", overlayPanel.transform, 36, TextAnchor.MiddleCenter);
            RectTransform subtitleRect = subtitleText.rectTransform;
            subtitleRect.anchorMin = new Vector2(0.08f, 0.42f);
            subtitleRect.anchorMax = new Vector2(0.92f, 0.64f);
            subtitleRect.offsetMin = Vector2.zero;
            subtitleRect.offsetMax = Vector2.zero;

            messageText = CreateText("Message", overlayPanel.transform, 30, TextAnchor.MiddleCenter);
            RectTransform messageRect = messageText.rectTransform;
            messageRect.anchorMin = new Vector2(0.08f, 0.25f);
            messageRect.anchorMax = new Vector2(0.92f, 0.4f);
            messageRect.offsetMin = Vector2.zero;
            messageRect.offsetMax = Vector2.zero;

            startButton = CreateButton("OverlayButton", overlayPanel.transform, "START", OnOverlayButtonPressed);
            startButtonText = startButton.GetComponentInChildren<Text>();
            RectTransform startRect = (RectTransform)startButton.transform;
            startRect.anchorMin = new Vector2(0.27f, 0.08f);
            startRect.anchorMax = new Vector2(0.73f, 0.2f);
            startRect.offsetMin = Vector2.zero;
            startRect.offsetMax = Vector2.zero;
        }

        private void OnOverlayButtonPressed()
        {
            if (phase == GamePhase.Paused)
            {
                TogglePause();
                return;
            }

            StartGame();
        }

        private HoldButton CreateHoldButton(string name, Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Action onPress, Action onRepeat, Action onRelease)
        {
            Button button = CreateButton(name, parent, label, null);
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            HoldButton holdButton = button.gameObject.AddComponent<HoldButton>();
            holdButton.OnPressed = onPress;
            holdButton.OnRepeated = onRepeat;
            holdButton.OnReleased = onRelease;
            return holdButton;
        }

        private Button CreateButton(string name, Transform parent, string label, Action onClick)
        {
            GameObject buttonGo = CreatePanel(name, parent, new Color32(18, 31, 62, 255));
            Button button = buttonGo.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color32(22, 35, 74, 255);
            colors.highlightedColor = new Color32(34, 65, 112, 255);
            colors.pressedColor = new Color32(64, 161, 241, 255);
            colors.selectedColor = colors.highlightedColor;
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.targetGraphic = buttonGo.GetComponent<Image>();
            button.onClick.AddListener(() => onClick?.Invoke());

            Outline outline = buttonGo.AddComponent<Outline>();
            outline.effectColor = new Color32(62, 201, 255, 255);
            outline.effectDistance = new Vector2(3f, -3f);

            Text text = CreateText(label, buttonGo.transform, 34, TextAnchor.MiddleCenter);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 20;
            text.resizeTextMaxSize = 34;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }

        private GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject go = new(name);
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.color = color;
            return go;
        }

        private Text CreateText(string content, Transform parent, int fontSize, TextAnchor alignment)
        {
            GameObject go = new(content + "Text");
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = uiFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = new Color32(236, 248, 255, 255);
            return text;
        }

        private void SetStretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private enum GamePhase
        {
            Title,
            Playing,
            Paused,
            GameOver,
        }
    }
}
