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
        [SerializeField] private float spacing = 1.08f;
        [SerializeField] private float moveAnimDuration = 0.18f;
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
        private GameKind _generatedKind = GameKind.Quixo;
        private Vector3 _initialLocalScale = Vector3.one;
        private bool _hasInitialLocalScale;
        private bool _hasGeneratedBoard;
        private int _renderVersion;

        private const float CubeWidth = 0.94f;
        private const float CubeHeight = 0.38f;
        private const float CubeTop = 0.4f;
        private const float QometSocketY = 0.155f;
        private const float QometPieceY = 0.235f;
        private static readonly Color QometBaseColor = new(0.018f, 0.020f, 0.026f, 1f);
        private static readonly Color QometSurfaceColor = new(0.055f, 0.061f, 0.071f, 1f);
        private static readonly Color QometTrimColor = new(0.125f, 0.130f, 0.142f, 1f);
        private static readonly Color QometRailColor = new(0.250f, 0.258f, 0.275f, 1f);
        private static readonly Color QometInlayColor = new(0.098f, 0.106f, 0.122f, 1f);
        private static readonly Color QometSocketColor = new(0.145f, 0.154f, 0.172f, 1f);
        private static readonly Color QometSocketAccentColor = new(0.310f, 0.325f, 0.350f, 1f);
        private static readonly Color QometGoldColor = new(1.000f, 0.700f, 0.155f, 1f);
        private static readonly Color QometGoldTopColor = new(1.000f, 0.835f, 0.330f, 1f);
        private static readonly Color QometRedColor = new(0.500f, 0.050f, 0.082f, 1f);
        private static readonly Color QometRedTopColor = new(0.720f, 0.115f, 0.155f, 1f);
        private static readonly Color QometSelectionColor = new(1.000f, 0.785f, 0.235f, 1f);

        private void Awake()
        {
            CaptureInitialScale();
        }

        public void Initialize(int size, Action<int, int> onCellClick, GameKind kind = GameKind.Quixo)
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
            CaptureInitialScale();
            transform.localScale = renderKind == GameKind.Qomet ? Vector3.one * 0.52f : _initialLocalScale;
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
                    view.Initialize(r, c, _onCellClick);
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
                        cell.SetState(state.Cells[r, c], selected);
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
                    cell.Initialize(r, c, _onCellClick);
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

        private GameObject CreateGeneratedCell()
        {
            var cell = new GameObject("Cell");
            cell.transform.SetParent(boardRoot, false);

            var collider = cell.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, CubeHeight * 0.5f, 0f);
            collider.size = new Vector3(CubeWidth, CubeHeight, CubeWidth);

            var visualRoot = new GameObject("TileVisual");
            visualRoot.transform.SetParent(cell.transform, false);

            var body = CreatePrimitiveChild(visualRoot.transform, "Body", generatedCellColor);
            body.transform.localPosition = new Vector3(0f, CubeHeight * 0.5f, 0f);
            body.transform.localScale = new Vector3(CubeWidth, CubeHeight, CubeWidth);
            var renderer = body.GetComponent<MeshRenderer>();

            var top = CreatePrimitiveChild(visualRoot.transform, "TopFace", generatedTopColor);
            top.transform.localPosition = new Vector3(0f, CubeTop + 0.004f, 0f);
            top.transform.localScale = new Vector3(CubeWidth * 0.9f, 0.014f, CubeWidth * 0.9f);

            var text = CreateMarkText(visualRoot.transform);

            var marker = CreateSelectionMarker(visualRoot.transform);
            var view = cell.AddComponent<BoardCellView>();
            view.ConfigureReferences(renderer, text, marker, visualRoot.transform);
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
                boardWidth = 13.6f;
                boardDepth = 7.6f;
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
                bool isLongDiagonal = distance > 2.6f;
                Color color = isLongDiagonal ? QometInlayColor : QometRailColor;
                float width = isLongDiagonal ? 0.052f : 0.070f;
                float height = isLongDiagonal ? 0.030f : 0.044f;
                CreateQometRail($"QometRail_{index:00}", GetQometNodePosition(edge.A.x, edge.A.y, size), GetQometNodePosition(edge.B.x, edge.B.y, size), width, height, color, 0.090f);
                index++;
            }
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
    }
}
