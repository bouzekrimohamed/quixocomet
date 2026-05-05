using System;
using System.Collections;
using QuixoUnity.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QuixoUnity.UI
{
    public sealed class BoardCellView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private MeshRenderer tileRenderer = null!;
        [SerializeField] private TextMeshPro markText = null!;
        [SerializeField] private Image selectionRing = null!;
        [SerializeField] private GameObject selectionMarker = null!;
        [SerializeField] private Color emptyColor = new(0.85f, 0.83f, 0.78f);
        [SerializeField] private Color player1Color = new(0.25f, 0.35f, 0.95f);
        [SerializeField] private Color player2Color = new(0.95f, 0.45f, 0.3f);
        [SerializeField] private float hoverScale = 1.06f;

        private int _row;
        private int _col;
        private Action<int, int> _onClick = null!;
        private Vector3 _baseScale;
        private Coroutine _feedbackRoutine = null!;

        private void Awake()
        {
            ResolveReferences();
            _baseScale = transform.localScale;
        }

        public void Initialize(int row, int col, Action<int, int> onClick)
        {
            _row = row;
            _col = col;
            _onClick = onClick;
            _baseScale = transform.localScale;
        }

        public void ConfigureReferences(MeshRenderer renderer, TextMeshPro text, GameObject marker)
        {
            tileRenderer = renderer;
            markText = text;
            selectionMarker = marker;

            if (selectionMarker != null && selectionRing == null)
            {
                selectionRing = selectionMarker.GetComponentInChildren<Image>();
            }

            ResolveReferences();
        }

        public void SetState(PlayerMark mark, bool selected)
        {
            string text = mark switch
            {
                PlayerMark.Player1 => "X",
                PlayerMark.Player2 => "O",
                _ => string.Empty,
            };
            if (markText != null)
            {
                markText.text = text;
            }

            var color = mark switch
            {
                PlayerMark.Player1 => player1Color,
                PlayerMark.Player2 => player2Color,
                _ => emptyColor,
            };

            if (tileRenderer != null)
            {
                tileRenderer.material.color = color;
            }

            if (selectionRing != null)
            {
                selectionRing.enabled = selected;
            }

            if (selectionMarker != null)
            {
                selectionMarker.SetActive(selected);
            }
        }

        public void PlayMoveFeedback(float duration)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (_feedbackRoutine != null)
            {
                StopCoroutine(_feedbackRoutine);
            }

            _feedbackRoutine = StartCoroutine(FeedbackRoutine(duration));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onClick?.Invoke(_row, _col);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            transform.localScale = _baseScale * hoverScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = _baseScale;
        }

        private IEnumerator FeedbackRoutine(float duration)
        {
            if (duration <= 0f)
            {
                yield break;
            }

            Vector3 peak = _baseScale * 1.12f;
            float half = duration * 0.5f;
            float elapsed = 0f;

            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                transform.localScale = Vector3.Lerp(_baseScale, peak, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                transform.localScale = Vector3.Lerp(peak, _baseScale, t);
                yield return null;
            }

            transform.localScale = _baseScale;
        }

        private void ResolveReferences()
        {
            if (tileRenderer == null)
            {
                tileRenderer = GetComponent<MeshRenderer>();
                if (tileRenderer == null)
                {
                    tileRenderer = GetComponentInChildren<MeshRenderer>();
                }
            }

            if (markText == null)
            {
                markText = GetComponentInChildren<TextMeshPro>();
            }

            if (selectionMarker == null && selectionRing != null)
            {
                selectionMarker = selectionRing.gameObject;
            }

            if (selectionMarker != null)
            {
                selectionMarker.SetActive(false);
            }

            if (GetComponent<Collider>() == null)
            {
                gameObject.AddComponent<BoxCollider>();
            }
        }
    }
}
