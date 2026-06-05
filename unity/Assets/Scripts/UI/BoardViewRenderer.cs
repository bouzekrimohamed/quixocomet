using System;
using System.Collections;
using QuixoUnity.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace QuixoUnity.UI
{
    public sealed class BoardViewRenderer : MonoBehaviour
    {
        [SerializeField] private Transform boardRoot = null!;
        [SerializeField] private GameObject cellPrefab = null!;
        [SerializeField] private float spacing = 1.14f;
        [SerializeField] private float moveAnimDuration = 0.22f;
        [SerializeField] private Color generatedCellColor = new(0.92f, 0.82f, 0.62f);
        [SerializeField] private Color generatedTopColor = new(1f, 0.9f, 0.68f);
        [SerializeField] private Color generatedSelectedCellColor = new(1f, 0.9f, 0.62f);
        [SerializeField] private Color generatedBoardColor = new(0.68f, 0.51f, 0.29f);
        [SerializeField] private Color generatedBoardTrimColor = new(0.45f, 0.32f, 0.18f);
        [SerializeField] private Color generatedSelectionColor = new(1f, 0.79f, 0.18f);
        [SerializeField] private Color generatedPlayer1Color = new(0.04f, 0.14f, 0.34f);
        [SerializeField] private Color generatedPlayer2Color = new(0.62f, 0.18f, 0.09f);
        [SerializeField] private Color generatedTextShadowColor = new(0.16f, 0.1f, 0.05f);
        [SerializeField] private float generatedMarkFontSize = 4.45f;
        [SerializeField] private Shader generatedMaterialShader = null!;
        [SerializeField] private GameKind renderKind = GameKind.Quixo;

        private BoardCellView[,] _cells = null!;
        private Action<int, int> _onCellClick = null!;
        private Action<int, int, Vector2> _onCellDrag = null!;
        private GameKind _generatedKind = GameKind.Quixo;
        private Vector3 _initialLocalScale = Vector3.one;
        private bool _hasInitialLocalScale;
        private bool _hasGeneratedBoard;
        private int _renderVersion;
        private Transform _teamLabelsRoot;

        private const float CubeWidth = 0.88f;
        private const float CubeHeight = 0.42f;
        private const float CubeTop = 0.445f;
        private const float QometSocketY = 0.155f;
        private const float QometPieceY = 0.235f;
        private static readonly Color QometBaseColor = new(0.015f, 0.017f, 0.023f, 1f);
        private static readonly Color QometSurfaceColor = new(0.060f, 0.066f, 0.078f, 1f);
        private static readonly Color QometTrimColor = new(0.150f, 0.155f, 0.168f, 1f);
        private static readonly Color QometRailColor = new(0.305f, 0.318f, 0.340f, 1f);
        private static readonly Color QometInlayColor = new(0.105f, 0.115f, 0.135f, 1f);
        private static readonly Color QometWellColor = new(0.028f, 0.032f, 0.040f, 1f);
        private static readonly Color QometWellRimColor = new(0.205f, 0.216f, 0.238f, 1f);
        private static readonly Color QometSocketColor = new(0.115f, 0.126f, 0.148f, 1f);
        private static readonly Color QometSocketAccentColor = new(0.385f, 0.400f, 0.430f, 1f);
        private static readonly Color QometGoldColor = new(1.000f, 0.700f, 0.155f, 1f);
        private static readonly Color QometGoldTopColor = new(1.000f, 0.835f, 0.330f, 1f);
        private static readonly Color QometRedColor = new(0.500f, 0.050f, 0.082f, 1f);
        private static readonly Color QometRedTopColor = new(0.720f, 0.115f, 0.155f, 1f);
        private static readonly Color QometSelectionColor = new(1.000f, 0.785f, 0.235f, 1f);

        private void Awake()
        {
            CaptureInitialScale();
        }

        public void Initialize(int size, Action<int, int> onCellClick, GameKind kind = GameKind.Quixo, Action<int, int, Vector2> onCellDrag = null)
        {
            if (size <= 0)
            {
                Debug.LogError("BoardViewRenderer: board size must be positive.", this);
                return;
            }

            StopAllCoroutines();
            _renderVersion++;
            renderKind = kind;
            _onCellClick = onCellClick;
            _onCellDrag = onCellDrag;
            CaptureInitialScale();
            transform.localScale = renderKind == GameKind.Qomet ? Vector3.one * 0.84f : _initialLocalScale;
            if (boardRoot == null)
            {
                boardRoot = transform;
            }

            CenterBoardInView();
            ApplyActiveTheme();
            EnsureInputSupport();

            if (_cells != null && _cells.GetLength(0) == size && _hasGeneratedBoard && _generatedKind == renderKind)
            {
                ResetCellsLayout(size);
                return;
            }

            ClearChildren();
            if (renderKind == GameKind.Qomet)
            {
                CreateQometBoardBase(size);
            }
            else
            {
                CreateBoardBase(size);
            }

            _cells = new BoardCellView[size, size];
            _generatedKind = renderKind;
            _hasGeneratedBoard = true;

            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    if (renderKind == GameKind.Qomet && !QometGraph.IsValidNode(r, c))
                    {
                        continue;
                    }

                    var go = CreateCellInstance();
                    go.name = $"Cell_{r}_{c}";
                    go.transform.localPosition = GetCellLocalPosition(r, c, size);
                    var view = PrepareCellView(go);
                    view.Initialize(r, c, _onCellClick, _onCellDrag);
                    _cells[r, c] = view;
                }
            }
        }

        private Vector3 GetCellLocalPosition(int row, int col, int size)
        {
            return renderKind == GameKind.Qomet
                ? GetQometNodePosition(row, col, size)
                : GetGridCellPosition(row, col, size);
        }

        private Vector3 GetGridCellPosition(int row, int col, int size)
        {
            float offset = (size - 1) * spacing * 0.5f;
            return new Vector3(col * spacing - offset, 0f, -(row * spacing - offset));
        }

        private Vector3 GetQometNodePosition(int row, int col, int size)
        {
            if (size != QometGraph.BoardSize)
            {
                return GetGridCellPosition(row, col, size);
            }

            Vector2 visualPosition = QometGraph.GetVisualPosition(row, col);
            return new Vector3(visualPosition.x, 0f, visualPosition.y);
        }

        public void Render(BoardState state, Vector2Int? selectedCell)
        {
            if (state == null || _cells == null)
            {
                return;
            }

            if (_cells.GetLength(0) < state.Size || _cells.GetLength(1) < state.Size)
            {
                Debug.LogWarning("BoardViewRenderer: board state size does not match generated cells.", this);
                return;
            }

            for (int r = 0; r < state.Size; r++)
            {
                for (int c = 0; c < state.Size; c++)
                {
                    bool selected = selectedCell.HasValue && selectedCell.Value.x == r && selectedCell.Value.y == c;
                    var cell = _cells[r, c];
                    if (cell != null)
                    {
                        QuixoDotOwner dotOwner = renderKind == GameKind.Quixo ? state.DotOwners[r, c] : QuixoDotOwner.None;
                        cell.SetState(state.Cells[r, c], selected, dotOwner);
                    }
                }
            }
        }

        public void AnimateBoardChange(BoardState state, Vector2Int originCell)
        {
            if (state == null || _cells == null)
            {
                return;
            }

            StopAllCoroutines();
            int version = _renderVersion;
            if (IsInBounds(state.Size, originCell) && _cells[originCell.x, originCell.y] != null)
            {
                _cells[originCell.x, originCell.y].PlayMoveFeedback(moveAnimDuration);
            }

            StartCoroutine(AnimateBoardChangeRoutine(state, version));
        }

        public void SetTeamPositionLabels(string bottom, string right, string top, string left, bool visible)
        {
            if (!visible || renderKind != GameKind.Quixo)
            {
                SetObjectActive(_teamLabelsRoot != null ? _teamLabelsRoot.gameObject : null, false);
                return;
            }

            EnsureTeamLabelsRoot();
            SetObjectActive(_teamLabelsRoot.gameObject, true);
            SetTeamLabel("BottomPositionLabel", string.IsNullOrWhiteSpace(bottom) ? "J1 - Bas" : bottom, new Vector3(0f, 0.18f, -3.25f));
            SetTeamLabel("RightPositionLabel", string.IsNullOrWhiteSpace(right) ? "J2 - Droite" : right, new Vector3(3.32f, 0.18f, 0f));
            SetTeamLabel("TopPositionLabel", string.IsNullOrWhiteSpace(top) ? "J3 - Haut" : top, new Vector3(0f, 0.18f, 3.25f));
            SetTeamLabel("LeftPositionLabel", string.IsNullOrWhiteSpace(left) ? "J4 - Gauche" : left, new Vector3(-3.32f, 0.18f, 0f));
        }

        private IEnumerator AnimateBoardChangeRoutine(BoardState state, int version)
        {
            float elapsed = 0f;
            Vector3[,] start = new Vector3[state.Size, state.Size];
            for (int r = 0; r < state.Size; r++)
            {
                for (int c = 0; c < state.Size; c++)
                {
                    start[r, c] = _cells[r, c] != null ? _cells[r, c].transform.localPosition : Vector3.zero;
                }
            }

            while (elapsed < moveAnimDuration)
            {
                if (version != _renderVersion)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / moveAnimDuration);
                for (int r = 0; r < state.Size; r++)
                {
                    for (int c = 0; c < state.Size; c++)
                    {
                        if (_cells[r, c] == null)
                        {
                            continue;
                        }

                        Vector3 target = GetCellLocalPosition(r, c, state.Size);
                        _cells[r, c].transform.localPosition = Vector3.Lerp(start[r, c], target, EaseOutCubic(t));
                    }
                }

                yield return null;
            }

            if (version != _renderVersion)
            {
                yield break;
            }

            Render(state, null);
        }

        private static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        private void ClearChildren()
        {
            if (boardRoot == null)
            {
                return;
            }

            for (int i = boardRoot.childCount - 1; i >= 0; i--)
            {
                var child = boardRoot.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }

            _teamLabelsRoot = null;
        }

        private void ResetCellsLayout(int size)
        {
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    var cell = _cells[r, c];
                    if (cell == null)
                    {
                        continue;
                    }

                    cell.transform.localPosition = GetCellLocalPosition(r, c, size);
                    cell.Initialize(r, c, _onCellClick, _onCellDrag);
                    cell.ResetInteractionState();
                }
            }
        }

        private GameObject CreateCellInstance()
        {
            if (cellPrefab != null)
            {
                return Instantiate(cellPrefab, boardRoot);
            }

            return renderKind == GameKind.Qomet ? CreateGeneratedQometCell() : CreateGeneratedCell();
        }

        private BoardCellView PrepareCellView(GameObject cell)
        {
            var view = cell.GetComponent<BoardCellView>();
            if (view == null)
            {
                view = cell.AddComponent<BoardCellView>();
            }

            if (renderKind == GameKind.Qomet)
            {
                var qometVisual = FindChildByName(cell.transform, "QometVisual");
                var qometSelectionMarker = FindChildByName(cell.transform, "SelectionMarker")?.gameObject;
                var emptyMarker = FindChildByName(cell.transform, "EmptyRosette")?.gameObject;
                var player1Token = FindChildByName(cell.transform, "Player1Token")?.gameObject;
                var player2Token = FindChildByName(cell.transform, "Player2Token")?.gameObject;
                var socketCore = FindChildByName(cell.transform, "SocketCore");
                var qometRenderer = socketCore != null ? socketCore.GetComponent<MeshRenderer>() : cell.GetComponentInChildren<MeshRenderer>();

                view.ConfigureReferences(qometRenderer, null, qometSelectionMarker, qometVisual != null ? qometVisual : cell.transform);
                view.ConfigureStyle(QometSocketColor, QometSelectionColor, QometGoldColor, QometRedColor);
                view.ConfigureInteractionStyle(1.025f, 0.035f);
                view.ConfigureTokenReferences(emptyMarker, player1Token, player2Token);
                return view;
            }

            var renderer = cell.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = cell.GetComponentInChildren<MeshRenderer>();
            }

            var marker = FindChildByName(cell.transform, "SelectionMarker")?.gameObject;
            var visual = FindChildByName(cell.transform, "TileVisual");
            var text = cell.GetComponentInChildren<TextMeshPro>(true);
            if (text == null)
            {
                text = CreateMarkText(visual != null ? visual : cell.transform);
            }
            if (marker == null)
            {
                marker = CreateSelectionMarker(visual != null ? visual : cell.transform);
            }

            view.ConfigureReferences(renderer, text, marker, visual);
            view.ConfigureStyle(generatedCellColor, generatedSelectedCellColor, generatedPlayer1Color, generatedPlayer2Color);
            view.ConfigureMarkFontSize(generatedMarkFontSize);
            return view;
        }

        private void CenterBoardInView()
        {
            if (transform.parent != null)
            {
                return;
            }

            var position = transform.position;
            if (Mathf.Abs(position.z) < 0.01f)
            {
                transform.position = new Vector3(position.x, position.y, -0.45f);
            }
        }

        private void CaptureInitialScale()
        {
            if (_hasInitialLocalScale)
            {
                return;
            }

            _initialLocalScale = transform.localScale;
            _hasInitialLocalScale = true;
        }

        private void ApplyActiveTheme()
        {
            GameplayTheme theme = SceneTransit.SelectedTheme;
            if (theme == VisualThemeCatalog.DefaultTheme)
            {
                theme = VisualThemeCatalog.ActiveTheme;
            }

            ApplyTheme(VisualThemeCatalog.Get(theme));
        }

        public void ApplyTheme(GameplayPalette palette)
        {
            generatedCellColor = palette.Cube;
            generatedTopColor = palette.CubeTop;
            generatedSelectedCellColor = palette.SelectedCube;
            generatedBoardColor = palette.Board;
            generatedBoardTrimColor = palette.BoardTrim;
            generatedSelectionColor = palette.Selection;
            generatedPlayer1Color = palette.Player1;
            generatedPlayer2Color = palette.Player2;
            generatedMarkFontSize = palette.MarkFontSize;

            if (renderKind == GameKind.Qomet)
            {
                generatedCellColor = QometSocketColor;
                generatedTopColor = QometSocketAccentColor;
                generatedSelectedCellColor = QometSelectionColor;
                generatedBoardColor = QometSurfaceColor;
                generatedBoardTrimColor = QometTrimColor;
                generatedSelectionColor = QometSelectionColor;
                generatedPlayer1Color = QometGoldColor;
                generatedPlayer2Color = QometRedColor;
                generatedMarkFontSize = 0f;
            }

            if (_cells != null)
            {
                foreach (var cell in _cells)
                {
                    if (cell == null)
                    {
                        continue;
                    }

                    cell.ConfigureStyle(generatedCellColor, generatedSelectedCellColor, generatedPlayer1Color, generatedPlayer2Color);
                    cell.ConfigureMarkFontSize(generatedMarkFontSize);
                }
            }

            Color ambient = renderKind == GameKind.Qomet ? new Color(0.42f, 0.39f, 0.34f, 1f) : palette.AmbientLight;
            Color cameraBackground = renderKind == GameKind.Qomet ? new Color(0.018f, 0.030f, 0.052f, 1f) : palette.CameraBackground;
            Color keyLight = renderKind == GameKind.Qomet ? new Color(1.00f, 0.855f, 0.620f, 1f) : palette.KeyLight;
            Color focus = renderKind == GameKind.Qomet ? Color.Lerp(cameraBackground, QometSelectionColor, 0.18f) : Color.Lerp(palette.CameraBackground, palette.Selection, 0.20f);
            Color trim = renderKind == GameKind.Qomet ? Color.Lerp(cameraBackground, QometTrimColor, 0.48f) : Color.Lerp(palette.CameraBackground, palette.BoardTrim, 0.32f);

            RenderSettings.ambientLight = ambient;
            UpdateSceneMaterial("BoardFocusMat", trim);
            UpdateSceneMaterial("BoardSoftHalo", focus);
            UpdateSceneMaterial("BoardGroundShadow", Color.Lerp(cameraBackground, Color.black, 0.28f));
            UpdateStageForCurrentGame();

            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.backgroundColor = cameraBackground;
            }

            var lights = FindObjectsOfType<Light>();
            foreach (var sceneLight in lights)
            {
                if (sceneLight != null && sceneLight.type == LightType.Directional)
                {
                    sceneLight.color = keyLight;
                    sceneLight.intensity = renderKind == GameKind.Qomet ? 1.72f : 1.55f;
                }
            }
        }

        private void UpdateSceneMaterial(string objectName, Color color)
        {
            var target = transform.Find(objectName);
            if (target == null)
            {
                return;
            }

            var renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = renderer.sharedMaterial;
                if (material == null)
                {
                    material = CreateMaterial(color);
                    renderer.sharedMaterial = material;
                    return;
                }

                ApplyMaterialColor(material, color);
            }
        }

        private void UpdateStageForCurrentGame()
        {
            if (renderKind == GameKind.Qomet)
            {
                UpdateSceneTransform("BoardFocusMat", new Vector3(9.40f, 0.045f, 9.40f), new Vector3(0f, -0.225f, 0f));
                UpdateSceneTransform("BoardSoftHalo", new Vector3(8.80f, 0.032f, 8.80f), new Vector3(0f, -0.188f, 0f));
                UpdateSceneTransform("BoardGroundShadow", new Vector3(8.25f, 0.024f, 8.25f), new Vector3(0f, -0.248f, 0.08f));
                return;
            }

            UpdateSceneTransform("BoardFocusMat", new Vector3(6.72f, 0.045f, 6.72f), new Vector3(0f, -0.220f, 0f));
            UpdateSceneTransform("BoardSoftHalo", new Vector3(6.24f, 0.032f, 6.24f), new Vector3(0f, -0.185f, 0f));
            UpdateSceneTransform("BoardGroundShadow", new Vector3(5.82f, 0.024f, 5.82f), new Vector3(0f, -0.245f, 0.08f));
        }

        private void UpdateSceneTransform(string objectName, Vector3 localScale, Vector3 localPosition)
        {
            var target = transform.Find(objectName);
            if (target == null)
            {
                return;
            }

            target.localScale = localScale;
            target.localPosition = localPosition;
        }

        private GameObject CreateGeneratedCell()
        {
            var cell = new GameObject("Cell");
            cell.transform.SetParent(boardRoot, false);

            var collider = cell.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, CubeHeight * 0.5f, 0f);
            collider.size = new Vector3(CubeWidth, CubeHeight, CubeWidth);

            var visualRoot = new GameObject("TileVisual");
            visualRoot.transform.SetParent(cell.transform, false);

            var shadow = CreatePrimitiveChild(visualRoot.transform, "CubeContactShadow", Color.Lerp(generatedBoardTrimColor, Color.black, 0.35f), PrimitiveType.Cube, 0.08f, 0f);
            shadow.transform.localPosition = new Vector3(0.030f, 0.018f, -0.035f);
            shadow.transform.localScale = new Vector3(CubeWidth * 1.04f, 0.018f, CubeWidth * 1.04f);

            var body = CreatePrimitiveChild(visualRoot.transform, "Body", generatedCellColor);
            body.transform.localPosition = new Vector3(0f, CubeHeight * 0.5f, 0f);
            body.transform.localScale = new Vector3(CubeWidth, CubeHeight, CubeWidth);
            var renderer = body.GetComponent<MeshRenderer>();

            var frontShadow = CreatePrimitiveChild(visualRoot.transform, "FrontFaceShade", Color.Lerp(generatedCellColor, generatedBoardTrimColor, 0.46f), PrimitiveType.Cube, 0.10f, 0f);
            frontShadow.transform.localPosition = new Vector3(0f, CubeHeight * 0.45f, -CubeWidth * 0.505f);
            frontShadow.transform.localScale = new Vector3(CubeWidth * 0.92f, CubeHeight * 0.70f, 0.020f);

            var rightShade = CreatePrimitiveChild(visualRoot.transform, "RightFaceShade", Color.Lerp(generatedCellColor, generatedBoardTrimColor, 0.32f), PrimitiveType.Cube, 0.10f, 0f);
            rightShade.transform.localPosition = new Vector3(CubeWidth * 0.505f, CubeHeight * 0.46f, 0f);
            rightShade.transform.localScale = new Vector3(0.020f, CubeHeight * 0.68f, CubeWidth * 0.88f);

            var top = CreatePrimitiveChild(visualRoot.transform, "TopFace", generatedTopColor);
            top.transform.localPosition = new Vector3(0f, CubeTop + 0.006f, 0f);
            top.transform.localScale = new Vector3(CubeWidth * 0.86f, 0.016f, CubeWidth * 0.86f);

            var topHighlight = CreatePrimitiveChild(visualRoot.transform, "TopHighlight", Color.Lerp(generatedTopColor, Color.white, 0.20f), PrimitiveType.Cube, 0.20f, 0f);
            topHighlight.transform.localPosition = new Vector3(-CubeWidth * 0.10f, CubeTop + 0.020f, CubeWidth * 0.10f);
            topHighlight.transform.localScale = new Vector3(CubeWidth * 0.48f, 0.010f, CubeWidth * 0.030f);

            var topGroove = CreatePrimitiveChild(visualRoot.transform, "TopInsetGroove", Color.Lerp(generatedTopColor, generatedBoardTrimColor, 0.24f), PrimitiveType.Cube, 0.10f, 0f);
            topGroove.transform.localPosition = new Vector3(0f, CubeTop + 0.024f, 0f);
            topGroove.transform.localScale = new Vector3(CubeWidth * 0.70f, 0.008f, CubeWidth * 0.70f);

            var text = CreateMarkText(visualRoot.transform);
            var dot = CreateDotMarker(visualRoot.transform);

            var marker = CreateSelectionMarker(visualRoot.transform);
            var view = cell.AddComponent<BoardCellView>();
            view.ConfigureReferences(renderer, text, marker, visualRoot.transform);
            view.ConfigureDotReference(dot);
            view.ConfigureStyle(generatedCellColor, generatedSelectedCellColor, generatedPlayer1Color, generatedPlayer2Color);
            view.ConfigureMarkFontSize(generatedMarkFontSize);
            return cell;
        }

        private GameObject CreateGeneratedQometCell()
        {
            var cell = new GameObject("QometCell");
            cell.transform.SetParent(boardRoot, false);

            var collider = cell.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.22f, 0f);
            collider.size = new Vector3(0.72f, 0.46f, 0.72f);

            var visualRoot = new GameObject("QometVisual");
            visualRoot.transform.SetParent(cell.transform, false);

            var emptyRosette = CreateQometRosette(visualRoot.transform, "EmptyRosette", QometSocketColor, QometSocketAccentColor, QometSocketY, 0.72f);
            var player1 = CreateQometToken(visualRoot.transform, "Player1Token", QometGoldColor, QometGoldTopColor);
            var player2 = CreateQometToken(visualRoot.transform, "Player2Token", QometRedColor, QometRedTopColor);
            var marker = CreateQometSelectionMarker(visualRoot.transform);

            player1.SetActive(false);
            player2.SetActive(false);

            var socketCore = FindChildByName(emptyRosette.transform, "SocketCore")?.GetComponent<MeshRenderer>();
            var view = cell.AddComponent<BoardCellView>();
            view.ConfigureReferences(socketCore, null, marker, visualRoot.transform);
            view.ConfigureStyle(QometSocketColor, QometSelectionColor, QometGoldColor, QometRedColor);
            view.ConfigureInteractionStyle(1.025f, 0.035f);
            view.ConfigureTokenReferences(emptyRosette, player1, player2);
            return cell;
        }

        private GameObject CreateQometRosette(Transform parent, string name, Color centerColor, Color accentColor, float y, float scale)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            var center = CreatePrimitiveChild(root.transform, "SocketCore", centerColor, PrimitiveType.Cylinder, 0.34f, 0f);
            center.transform.localPosition = new Vector3(0f, y, 0f);
            center.transform.localScale = new Vector3(0.32f * scale, 0.010f, 0.32f * scale);

            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                var arm = CreatePrimitiveChild(root.transform, $"SocketArm_{i:00}", accentColor, PrimitiveType.Cube, 0.30f, 0f);
                arm.transform.localPosition = Quaternion.Euler(0f, angle, 0f) * new Vector3(0.20f * scale, y + 0.012f, 0f);
                arm.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                arm.transform.localScale = new Vector3(0.22f * scale, 0.022f, 0.048f * scale);
            }

            return root;
        }

        private GameObject CreateQometSelectionMarker(Transform parent)
        {
            var marker = new GameObject("SelectionMarker");
            marker.transform.SetParent(parent, false);
            marker.SetActive(false);

            var glow = CreatePrimitiveChild(marker.transform, "SelectionGlow", new Color(QometSelectionColor.r, QometSelectionColor.g, QometSelectionColor.b, 0.52f), PrimitiveType.Cylinder, 0.36f, 0f);
            glow.transform.localPosition = new Vector3(0f, QometSocketY + 0.010f, 0f);
            glow.transform.localScale = new Vector3(0.58f, 0.008f, 0.58f);

            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                var ray = CreatePrimitiveChild(marker.transform, $"SelectionRay_{i:00}", QometSelectionColor, PrimitiveType.Cube, 0.32f, 0f);
                ray.transform.localPosition = Quaternion.Euler(0f, angle, 0f) * new Vector3(0.43f, QometSocketY + 0.030f, 0f);
                ray.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                ray.transform.localScale = new Vector3(0.22f, 0.028f, 0.040f);
            }

            return marker;
        }

        private GameObject CreateQometToken(Transform parent, string name, Color baseColor, Color topColor)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            var baseDisc = CreatePrimitiveChild(root.transform, "TokenBase", baseColor, PrimitiveType.Cylinder, 0.48f, 0.02f);
            baseDisc.transform.localPosition = new Vector3(0f, QometPieceY, 0f);
            baseDisc.transform.localScale = new Vector3(0.50f, 0.054f, 0.50f);

            var topDisc = CreatePrimitiveChild(root.transform, "TokenTop", topColor, PrimitiveType.Cylinder, 0.54f, 0.02f);
            topDisc.transform.localPosition = new Vector3(0f, QometPieceY + 0.075f, 0f);
            topDisc.transform.localScale = new Vector3(0.34f, 0.030f, 0.34f);

            var crown = CreateQometRosette(root.transform, "TokenRosette", topColor, Color.Lerp(topColor, Color.white, 0.28f), QometPieceY + 0.125f, 0.70f);
            crown.transform.localScale = new Vector3(0.92f, 1f, 0.92f);
            return root;
        }

        private TextMeshPro CreateMarkText(Transform parent)
        {
            var textObject = new GameObject("MarkText");
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = new Vector3(0f, CubeTop + 0.075f, 0f);
            textObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            textObject.transform.localScale = Vector3.one;

            var text = textObject.AddComponent<TextMeshPro>();
            text.name = "MarkText";
            text.text = string.Empty;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = false;
            text.fontStyle = FontStyles.Bold;
            text.fontSize = generatedMarkFontSize;
            text.color = generatedTextShadowColor;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableCulling = false;
            text.gameObject.SetActive(true);
            return text;
        }

        private GameObject CreateDotMarker(Transform parent)
        {
            var dot = CreatePrimitiveChild(parent, "DotMarker", Color.Lerp(generatedTextShadowColor, Color.white, 0.18f), PrimitiveType.Cylinder, 0.20f, 0f);
            dot.transform.localPosition = new Vector3(0f, CubeTop + 0.055f, 0f);
            dot.transform.localScale = new Vector3(0.070f, 0.010f, 0.070f);
            dot.SetActive(false);
            return dot;
        }

        private GameObject CreateSelectionMarker(Transform parent)
        {
            var marker = new GameObject("SelectionMarker");
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(0f, CubeTop + 0.04f, 0f);
            marker.SetActive(false);

            float edge = CubeWidth * 0.55f;
            float length = CubeWidth * 1.16f;
            CreateMarkerBar(marker.transform, "Top", new Vector3(0f, 0f, edge), new Vector3(length, 0.03f, 0.04f));
            CreateMarkerBar(marker.transform, "Bottom", new Vector3(0f, 0f, -edge), new Vector3(length, 0.03f, 0.04f));
            CreateMarkerBar(marker.transform, "Left", new Vector3(-edge, 0f, 0f), new Vector3(0.04f, 0.03f, length));
            CreateMarkerBar(marker.transform, "Right", new Vector3(edge, 0f, 0f), new Vector3(0.04f, 0.03f, length));
            return marker;
        }

        private void CreateMarkerBar(Transform parent, string name, Vector3 localPosition, Vector3 localScale)
        {
            var bar = CreatePrimitiveChild(parent, name, generatedSelectionColor);
            bar.transform.localPosition = localPosition;
            bar.transform.localScale = localScale;
        }

        private void EnsureTeamLabelsRoot()
        {
            if (_teamLabelsRoot != null)
            {
                return;
            }

            var root = new GameObject("TeamPositionLabels");
            root.transform.SetParent(boardRoot, false);
            _teamLabelsRoot = root.transform;
        }

        private void SetTeamLabel(string name, string text, Vector3 localPosition)
        {
            EnsureTeamLabelsRoot();
            Transform existing = FindChildByName(_teamLabelsRoot, name);
            TextMeshPro label;
            if (existing == null)
            {
                var labelObject = new GameObject(name);
                labelObject.transform.SetParent(_teamLabelsRoot, false);
                label = labelObject.AddComponent<TextMeshPro>();
                label.name = name;
                label.alignment = TextAlignmentOptions.Center;
                label.fontStyle = FontStyles.Bold;
                label.fontSize = 0.24f;
                label.enableWordWrapping = false;
                label.overflowMode = TextOverflowModes.Overflow;
                label.rectTransform.sizeDelta = new Vector2(3.2f, 0.5f);
                var textRenderer = label.GetComponent<Renderer>();
                if (textRenderer != null)
                {
                    textRenderer.sortingOrder = 5;
                }

                existing = labelObject.transform;
            }
            else
            {
                label = existing.GetComponent<TextMeshPro>();
            }

            existing.localPosition = localPosition;
            existing.localRotation = Quaternion.Euler(90f, 0f, 0f);
            if (label != null)
            {
                label.text = text;
                label.color = Color.Lerp(generatedSelectionColor, Color.white, 0.28f);
            }
        }

        private Material CreateMaterial(Color color, float smoothness = 0.18f, float metallic = 0f)
        {
            var shader = GetSafeShader();
            if (shader == null)
            {
                Debug.LogError("BoardViewRenderer: no compatible shader found for generated board materials.");
                return null;
            }

            var material = new Material(shader)
            {
                name = "QuixoGeneratedMaterial",
            };

            ApplyMaterialColor(material, color);
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            return material;
        }

        private Shader GetSafeShader()
        {
            if (generatedMaterialShader != null)
            {
                return generatedMaterialShader;
            }

            if (IsUsingScriptableRenderPipeline())
            {
                return Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard")
                    ?? Shader.Find("Unlit/Color")
                    ?? Shader.Find("Sprites/Default")
                    ?? Shader.Find("UI/Default")
                    ?? Shader.Find("Hidden/Internal-Colored");
            }

            return Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("UI/Default")
                ?? Shader.Find("Hidden/Internal-Colored");
        }

        private static bool IsUsingScriptableRenderPipeline()
        {
            return GraphicsSettings.currentRenderPipeline != null || QualitySettings.renderPipeline != null;
        }

        private static void ApplyMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            material.color = color;
        }

        private void CreateBoardBase(int size)
        {
            if (cellPrefab != null)
            {
                return;
            }

            float boardWidth = (size - 1) * spacing + CubeWidth + 0.42f;

            var trim = CreatePrimitiveChild(boardRoot, "BoardBase", generatedBoardTrimColor);
            trim.transform.localPosition = new Vector3(0f, -0.08f, 0f);
            trim.transform.localScale = new Vector3(boardWidth, 0.14f, boardWidth);

            var surface = CreatePrimitiveChild(boardRoot, "BoardSurface", generatedBoardColor);
            surface.transform.localPosition = new Vector3(0f, -0.005f, 0f);
            surface.transform.localScale = new Vector3(boardWidth - 0.18f, 0.045f, boardWidth - 0.18f);

            float offset = (size - 1) * spacing * 0.5f;
            var wellColor = Color.Lerp(generatedBoardTrimColor, Color.black, 0.18f);
            var wellRimColor = Color.Lerp(generatedBoardColor, generatedTopColor, 0.16f);
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    Vector3 cellPosition = GetGridCellPosition(r, c, size);
                    var rim = CreatePrimitiveChild(boardRoot, $"CubeWellRim_{r}_{c}", wellRimColor, PrimitiveType.Cube, 0.12f, 0f);
                    rim.transform.localPosition = new Vector3(cellPosition.x, 0.022f, cellPosition.z);
                    rim.transform.localScale = new Vector3(CubeWidth * 1.17f, 0.010f, CubeWidth * 1.17f);

                    var well = CreatePrimitiveChild(boardRoot, $"CubeWell_{r}_{c}", wellColor, PrimitiveType.Cube, 0.10f, 0f);
                    well.transform.localPosition = new Vector3(cellPosition.x, 0.040f, cellPosition.z);
                    well.transform.localScale = new Vector3(CubeWidth * 1.07f, 0.020f, CubeWidth * 1.07f);
                }
            }

            var grooveColor = Color.Lerp(generatedBoardTrimColor, generatedBoardColor, 0.18f);
            for (int i = 1; i < size; i++)
            {
                float position = -offset + (i - 0.5f) * spacing;
                var vertical = CreatePrimitiveChild(boardRoot, $"GrooveVertical_{i}", grooveColor);
                vertical.transform.localPosition = new Vector3(position, 0.032f, 0f);
                vertical.transform.localScale = new Vector3(0.035f, 0.026f, boardWidth - 0.54f);

                var horizontal = CreatePrimitiveChild(boardRoot, $"GrooveHorizontal_{i}", grooveColor);
                horizontal.transform.localPosition = new Vector3(0f, 0.033f, -position);
                horizontal.transform.localScale = new Vector3(boardWidth - 0.54f, 0.026f, 0.035f);
            }
        }

        private void CreateQometBoardBase(int size)
        {
            if (cellPrefab != null)
            {
                return;
            }

            float boardWidth;
            float boardDepth;
            if (size == QometGraph.BoardSize)
            {
                boardWidth = 8.85f;
                boardDepth = 8.85f;
            }
            else
            {
                boardWidth = (size - 1) * spacing + 1.58f;
                boardDepth = boardWidth;
            }

            float surfaceWidth = boardWidth - 0.34f;
            float surfaceDepth = boardDepth - 0.34f;

            var baseBlock = CreatePrimitiveChild(boardRoot, "QometBoardBase", QometBaseColor, PrimitiveType.Cube, 0.42f, 0.03f);
            baseBlock.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            baseBlock.transform.localScale = new Vector3(boardWidth, 0.32f, boardDepth);

            var surface = CreatePrimitiveChild(boardRoot, "QometBoardSurface", QometSurfaceColor, PrimitiveType.Cube, 0.36f, 0.01f);
            surface.transform.localPosition = new Vector3(0f, 0.005f, 0f);
            surface.transform.localScale = new Vector3(surfaceWidth, 0.070f, surfaceDepth);

            CreateQometEdge("QometEdgeTop", new Vector3(0f, 0.105f, surfaceDepth * 0.5f), new Vector3(surfaceWidth, 0.18f, 0.24f));
            CreateQometEdge("QometEdgeBottom", new Vector3(0f, 0.105f, -surfaceDepth * 0.5f), new Vector3(surfaceWidth, 0.18f, 0.24f));
            CreateQometEdge("QometEdgeLeft", new Vector3(-surfaceWidth * 0.5f, 0.105f, 0f), new Vector3(0.24f, 0.18f, surfaceDepth));
            CreateQometEdge("QometEdgeRight", new Vector3(surfaceWidth * 0.5f, 0.105f, 0f), new Vector3(0.24f, 0.18f, surfaceDepth));
            CreateQometCorner("QometCornerTL", new Vector3(-surfaceWidth * 0.5f, 0.112f, surfaceDepth * 0.5f));
            CreateQometCorner("QometCornerTR", new Vector3(surfaceWidth * 0.5f, 0.112f, surfaceDepth * 0.5f));
            CreateQometCorner("QometCornerBL", new Vector3(-surfaceWidth * 0.5f, 0.112f, -surfaceDepth * 0.5f));
            CreateQometCorner("QometCornerBR", new Vector3(surfaceWidth * 0.5f, 0.112f, -surfaceDepth * 0.5f));

            if (size == QometGraph.BoardSize)
            {
                CreateQometFrame("QometOuterInset", 3.78f, 0.122f, QometWellRimColor, 0.085f, 0.050f);
                CreateQometFrame("QometMiddleInset", 2.56f, 0.124f, Color.Lerp(QometRailColor, QometWellRimColor, 0.34f), 0.060f, 0.038f);
                CreateQometFrame("QometInnerInset", 1.34f, 0.126f, QometInlayColor, 0.044f, 0.028f);
                CreateQometNodeWells(size);
                CreateQometNetwork(size);
                return;
            }

            for (int r = 0; r < size; r++)
            {
                Vector3 start = GetGridCellPosition(r, 0, size);
                Vector3 end = GetGridCellPosition(r, size - 1, size);
                CreateQometRail($"QometRailHorizontal_{r}", start, end, 0.060f, 0.038f);
            }

            for (int c = 0; c < size; c++)
            {
                Vector3 start = GetGridCellPosition(0, c, size);
                Vector3 end = GetGridCellPosition(size - 1, c, size);
                CreateQometRail($"QometRailVertical_{c}", start, end, 0.060f, 0.038f);
            }
        }

        private void CreateQometNetwork(int size)
        {
            int index = 0;
            foreach (var edge in QometGraph.AllEdges)
            {
                Vector2 a = QometGraph.GetVisualPosition(edge.A.x, edge.A.y);
                Vector2 b = QometGraph.GetVisualPosition(edge.B.x, edge.B.y);
                float distance = (a - b).magnitude;
                bool isDiagonal = Mathf.Abs(a.x - b.x) > 0.05f && Mathf.Abs(a.y - b.y) > 0.05f;
                bool isLongDiagonal = isDiagonal && distance > 2.25f;
                bool isLongStraight = !isDiagonal && distance > 2.55f;
                Color color = isLongDiagonal
                    ? Color.Lerp(QometSurfaceColor, QometInlayColor, 0.55f)
                    : isLongStraight ? Color.Lerp(QometRailColor, QometInlayColor, 0.45f) : QometRailColor;
                float width = isLongDiagonal ? 0.034f : isLongStraight ? 0.052f : 0.070f;
                float height = isLongDiagonal ? 0.020f : isLongStraight ? 0.030f : 0.044f;
                float y = isLongDiagonal ? 0.078f : isLongStraight ? 0.084f : 0.090f;
                CreateQometRail($"QometRail_{index:00}", GetQometNodePosition(edge.A.x, edge.A.y, size), GetQometNodePosition(edge.B.x, edge.B.y, size), width, height, color, y);
                index++;
            }
        }

        private void CreateQometNodeWells(int size)
        {
            foreach (var node in QometGraph.AllNodes)
            {
                Vector3 position = GetQometNodePosition(node.Row, node.Col, size);
                var rim = CreatePrimitiveChild(boardRoot, $"QometWellRim_{node.Id}", QometWellRimColor, PrimitiveType.Cube, 0.22f, 0.01f);
                rim.transform.localPosition = new Vector3(position.x, 0.062f, position.z);
                rim.transform.localScale = new Vector3(0.86f, 0.030f, 0.86f);

                var well = CreatePrimitiveChild(boardRoot, $"QometWell_{node.Id}", QometWellColor, PrimitiveType.Cube, 0.12f, 0f);
                well.transform.localPosition = new Vector3(position.x, 0.084f, position.z);
                well.transform.localScale = new Vector3(0.68f, 0.030f, 0.68f);
            }
        }

        private void CreateQometFrame(string name, float halfExtent, float y, Color color, float width, float height)
        {
            CreateQometRail($"{name}_Top", new Vector3(-halfExtent, 0f, halfExtent), new Vector3(halfExtent, 0f, halfExtent), width, height, color, y);
            CreateQometRail($"{name}_Bottom", new Vector3(-halfExtent, 0f, -halfExtent), new Vector3(halfExtent, 0f, -halfExtent), width, height, color, y);
            CreateQometRail($"{name}_Left", new Vector3(-halfExtent, 0f, -halfExtent), new Vector3(-halfExtent, 0f, halfExtent), width, height, color, y);
            CreateQometRail($"{name}_Right", new Vector3(halfExtent, 0f, -halfExtent), new Vector3(halfExtent, 0f, halfExtent), width, height, color, y);
        }

        private void CreateQometRailChain(string prefix, Vector2Int[] nodes, int size, float width, float height, Color color, float y = 0.096f)
        {
            for (int i = 0; i < nodes.Length - 1; i++)
            {
                Vector3 start = GetQometNodePosition(nodes[i].x, nodes[i].y, size);
                Vector3 end = GetQometNodePosition(nodes[i + 1].x, nodes[i + 1].y, size);
                CreateQometRail($"{prefix}_{i:00}", start, end, width, height, color, y);
            }
        }

        private void CreateQometEdge(string name, Vector3 position, Vector3 scale)
        {
            var edge = CreatePrimitiveChild(boardRoot, name, QometTrimColor, PrimitiveType.Cube, 0.40f, 0.03f);
            edge.transform.localPosition = position;
            edge.transform.localScale = scale;
        }

        private void CreateQometCorner(string name, Vector3 position)
        {
            var corner = CreatePrimitiveChild(boardRoot, name, QometTrimColor, PrimitiveType.Cylinder, 0.42f, 0.03f);
            corner.transform.localPosition = position;
            corner.transform.localScale = new Vector3(0.44f, 0.090f, 0.44f);
        }

        private void CreateQometRail(string name, Vector3 start, Vector3 end, float width, float height)
        {
            CreateQometRail(name, start, end, width, height, QometRailColor, 0.088f);
        }

        private void CreateQometRail(string name, Vector3 start, Vector3 end, float width, float height, Color color, float y)
        {
            Vector3 delta = end - start;
            float length = delta.magnitude + 0.24f;
            if (length <= 0.01f)
            {
                return;
            }

            var rail = CreatePrimitiveChild(boardRoot, name, color, PrimitiveType.Cube, 0.36f, 0.01f);
            rail.transform.localPosition = (start + end) * 0.5f + Vector3.up * y;
            rail.transform.localRotation = Quaternion.Euler(0f, Mathf.Atan2(-delta.z, delta.x) * Mathf.Rad2Deg, 0f);
            rail.transform.localScale = new Vector3(length, height, width);
        }

        private GameObject CreatePrimitiveChild(Transform parent, string name, Color color)
        {
            return CreatePrimitiveChild(parent, name, color, PrimitiveType.Cube, 0.18f, 0f);
        }

        private GameObject CreatePrimitiveChild(Transform parent, string name, Color color, PrimitiveType primitiveType, float smoothness = 0.18f, float metallic = 0f)
        {
            var child = GameObject.CreatePrimitive(primitiveType);
            child.name = name;
            child.transform.SetParent(parent, false);

            var collider = child.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            var renderer = child.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            var material = CreateMaterial(color, smoothness, metallic);
            if (material != null)
            {
                renderer.material = material;
            }

            return child;
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root.name == childName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var result = FindChildByName(root.GetChild(i), childName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static void EnsureInputSupport()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            var mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.GetComponent<PhysicsRaycaster>() == null)
            {
                mainCamera.gameObject.AddComponent<PhysicsRaycaster>();
            }
        }

        private static bool IsInBounds(int size, Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < size && cell.y >= 0 && cell.y < size;
        }

        private static void SetObjectActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
