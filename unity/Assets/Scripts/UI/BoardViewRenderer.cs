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
        [SerializeField] private float spacing = 1.05f;
        [SerializeField] private float moveAnimDuration = 0.2f;
        [SerializeField] private Color generatedCellColor = new(0.85f, 0.83f, 0.78f);
        [SerializeField] private Color generatedSelectionColor = new(1f, 0.86f, 0.25f);

        private BoardCellView[,] _cells = null!;
        private Action<int, int> _onCellClick = null!;

        public void Initialize(int size, Action<int, int> onCellClick)
        {
            _onCellClick = onCellClick;
            if (boardRoot == null)
            {
                boardRoot = transform;
            }

            EnsureInputSupport();

            if (_cells != null && _cells.GetLength(0) == size)
            {
                return;
            }

            ClearChildren();
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
            if (_cells == null)
            {
                return;
            }

            for (int r = 0; r < state.Size; r++)
            {
                for (int c = 0; c < state.Size; c++)
                {
                    bool selected = selectedCell.HasValue && selectedCell.Value.x == r && selectedCell.Value.y == c;
                    _cells[r, c].SetState(state.Cells[r, c], selected);
                }
            }
        }

        public void AnimateBoardChange(BoardState state, Vector2Int originCell)
        {
            StopAllCoroutines();
            if (IsInBounds(state.Size, originCell))
            {
                _cells[originCell.x, originCell.y].PlayMoveFeedback(moveAnimDuration);
            }

            StartCoroutine(AnimateBoardChangeRoutine(state));
        }

        private IEnumerator AnimateBoardChangeRoutine(BoardState state)
        {
            float elapsed = 0f;
            Vector3[,] start = new Vector3[state.Size, state.Size];
            for (int r = 0; r < state.Size; r++)
            {
                for (int c = 0; c < state.Size; c++)
                {
                    start[r, c] = _cells[r, c].transform.localPosition;
                }
            }

            float offset = (state.Size - 1) * spacing * 0.5f;
            while (elapsed < moveAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / moveAnimDuration);
                for (int r = 0; r < state.Size; r++)
                {
                    for (int c = 0; c < state.Size; c++)
                    {
                        Vector3 target = new Vector3(c * spacing - offset, 0f, -(r * spacing - offset));
                        _cells[r, c].transform.localPosition = Vector3.Lerp(start[r, c], target, EaseOutCubic(t));
                    }
                }

                yield return null;
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

            var text = cell.GetComponentInChildren<TextMeshPro>();
            var marker = cell.transform.Find("SelectionMarker")?.gameObject;
            view.ConfigureReferences(renderer, text, marker);
            return view;
        }

        private GameObject CreateGeneratedCell()
        {
            var cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cell.transform.SetParent(boardRoot, false);
            cell.transform.localScale = new Vector3(0.92f, 0.16f, 0.92f);

            var renderer = cell.GetComponent<MeshRenderer>();
            renderer.material = CreateMaterial(generatedCellColor);

            var textObject = new GameObject("MarkText");
            textObject.transform.SetParent(cell.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            textObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var text = textObject.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 1f;
            text.fontSizeMax = 4f;
            text.color = Color.white;
            text.rectTransform.sizeDelta = new Vector2(1.2f, 1.2f);

            var marker = CreateSelectionMarker(cell.transform);
            var view = cell.AddComponent<BoardCellView>();
            view.ConfigureReferences(renderer, text, marker);
            return cell;
        }

        private GameObject CreateSelectionMarker(Transform parent)
        {
            var marker = new GameObject("SelectionMarker");
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(0f, 0.105f, 0f);
            marker.SetActive(false);

            CreateMarkerBar(marker.transform, "Top", new Vector3(0f, 0f, 0.48f), new Vector3(1.05f, 0.035f, 0.035f));
            CreateMarkerBar(marker.transform, "Bottom", new Vector3(0f, 0f, -0.48f), new Vector3(1.05f, 0.035f, 0.035f));
            CreateMarkerBar(marker.transform, "Left", new Vector3(-0.48f, 0f, 0f), new Vector3(0.035f, 0.035f, 1.05f));
            CreateMarkerBar(marker.transform, "Right", new Vector3(0.48f, 0f, 0f), new Vector3(0.035f, 0.035f, 1.05f));
            return marker;
        }

        private void CreateMarkerBar(Transform parent, string name, Vector3 localPosition, Vector3 localScale)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPosition;
            bar.transform.localScale = localScale;

            var collider = bar.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            var renderer = bar.GetComponent<MeshRenderer>();
            renderer.material = CreateMaterial(generatedSelectionColor);
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
            return material;
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
