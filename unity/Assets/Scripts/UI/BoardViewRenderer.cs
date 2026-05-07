using System;
using System.Collections;
using QuixoUnity.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

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

        private BoardCellView[,] _cells = null!;
        private Action<int, int> _onCellClick = null!;
        private int _renderVersion;

        private const float CubeWidth = 0.94f;
        private const float CubeHeight = 0.38f;
        private const float CubeTop = 0.4f;

        public void Initialize(int size, Action<int, int> onCellClick)
        {
            if (size <= 0)
            {
                Debug.LogError("BoardViewRenderer: board size must be positive.", this);
                return;
            }

            StopAllCoroutines();
            _renderVersion++;
            _onCellClick = onCellClick;
            if (boardRoot == null)
            {
                boardRoot = transform;
            }

            EnsureInputSupport();

            if (_cells != null && _cells.GetLength(0) == size)
            {
                ResetCellsLayout(size);
                return;
            }

            ClearChildren();
            CreateBoardBase(size);
            _cells = new BoardCellView[size, size];
            float offset = (size - 1) * spacing * 0.5f;

            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    var go = CreateCellInstance();
                    go.name = $"Cell_{r}_{c}";
                    go.transform.localPosition = new Vector3(c * spacing - offset, 0f, -(r * spacing - offset));
                    var view = PrepareCellView(go);
                    view.Initialize(r, c, _onCellClick);
                    _cells[r, c] = view;
                }
            }
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

            float offset = (state.Size - 1) * spacing * 0.5f;
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

                        Vector3 target = new Vector3(c * spacing - offset, 0f, -(r * spacing - offset));
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
            float offset = (size - 1) * spacing * 0.5f;
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    var cell = _cells[r, c];
                    if (cell == null)
                    {
                        continue;
                    }

                    cell.transform.localPosition = new Vector3(c * spacing - offset, 0f, -(r * spacing - offset));
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

            return CreateGeneratedCell();
        }

        private BoardCellView PrepareCellView(GameObject cell)
        {
            var view = cell.GetComponent<BoardCellView>();
            if (view == null)
            {
                view = cell.AddComponent<BoardCellView>();
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
            return view;
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
            return cell;
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

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            var material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.18f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.18f);
            }

            return material;
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
        }

        private GameObject CreatePrimitiveChild(Transform parent, string name, Color color)
        {
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.name = name;
            child.transform.SetParent(parent, false);

            var collider = child.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            var renderer = child.GetComponent<MeshRenderer>();
            renderer.material = CreateMaterial(color);
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
