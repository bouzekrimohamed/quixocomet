using System;
using System.Collections;
using QuixoUnity.Core;
using UnityEngine;

namespace QuixoUnity.UI
{
    public sealed class BoardViewRenderer : MonoBehaviour
    {
        [SerializeField] private Transform boardRoot = null!;
        [SerializeField] private GameObject cellPrefab = null!;
        [SerializeField] private float spacing = 1.05f;
        [SerializeField] private float moveAnimDuration = 0.2f;

        private BoardCellView[,] _cells = null!;
        private Action<int, int> _onCellClick = null!;

        public void Initialize(int size, Action<int, int> onCellClick)
        {
            _onCellClick = onCellClick;
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
                    var go = Instantiate(cellPrefab, boardRoot);
                    go.name = $"Cell_{r}_{c}";
                    go.transform.localPosition = new Vector3(c * spacing - offset, 0f, -(r * spacing - offset));
                    var view = go.GetComponent<BoardCellView>();
                    view.Initialize(r, c, _onCellClick);
                    _cells[r, c] = view;
                }
            }
        }

        public void Render(BoardState state, Vector2Int? selectedCell)
        {
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
                DestroyImmediate(boardRoot.GetChild(i).gameObject);
            }
        }
    }
}
