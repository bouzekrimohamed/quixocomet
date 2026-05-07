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
        [SerializeField] private Transform visualRoot = null!;
        [SerializeField] private TextMeshPro markText = null!;
        [SerializeField] private Image selectionRing = null!;
        [SerializeField] private GameObject selectionMarker = null!;
        [SerializeField] private Color emptyColor = new(0.92f, 0.82f, 0.62f);
        [SerializeField] private Color selectedColor = new(1f, 0.9f, 0.64f);
        [SerializeField] private Color player1Color = new(0.04f, 0.14f, 0.34f);
        [SerializeField] private Color player2Color = new(0.62f, 0.18f, 0.09f);
        [SerializeField] private float hoverScale = 1.04f;
        [SerializeField] private float selectedLift = 0.09f;

        private int _row;
        private int _col;
        private Action<int, int> _onClick = null!;
        private Vector3 _baseScale;
        private Vector3 _baseVisualScale;
        private Vector3 _baseVisualPosition;
        private bool _hovered;
        private bool _selected;
        private Coroutine _feedbackRoutine = null!;

        private Transform VisualTarget => visualRoot != null ? visualRoot : transform;

        private void Awake()
        {
            ResolveReferences();
            _baseScale = transform.localScale;
            CacheVisualPose();
        }

        public void Initialize(int row, int col, Action<int, int> onClick)
        {
            _row = row;
            _col = col;
            _onClick = onClick;
            _baseScale = transform.localScale;
            CacheVisualPose();
        }

        public void ConfigureReferences(MeshRenderer renderer, TextMeshPro text, GameObject marker, Transform visualRootOverride = null)
        {
            tileRenderer = renderer;
            markText = text;
            selectionMarker = marker;
            if (visualRootOverride != null)
            {
                visualRoot = visualRootOverride;
            }

            if (selectionMarker != null && selectionRing == null)
            {
                selectionRing = selectionMarker.GetComponentInChildren<Image>();
            }

            ResolveReferences();
            CacheVisualPose();
        }

        public void ConfigureStyle(Color empty, Color selected, Color player1, Color player2)
        {
            emptyColor = empty;
            selectedColor = selected;
            player1Color = player1;
            player2Color = player2;
        }

        public void SetState(PlayerMark mark, bool selected)
        {
            _selected = selected;
            string text = mark switch
            {
                PlayerMark.Player1 => "X",
                PlayerMark.Player2 => "O",
                _ => string.Empty,
            };
            if (markText != null)
            {
                PrepareMarkText();
                markText.gameObject.SetActive(true);
                markText.text = text;
                markText.color = mark switch
                {
                    PlayerMark.Player1 => player1Color,
                    PlayerMark.Player2 => player2Color,
                    _ => Color.clear,
                };
                markText.ForceMeshUpdate();
            }

            if (tileRenderer != null)
            {
                tileRenderer.material.color = selected ? selectedColor : emptyColor;
            }

            if (selectionRing != null)
            {
                selectionRing.enabled = selected;
            }

            if (selectionMarker != null)
            {
                selectionMarker.SetActive(selected);
            }

            ApplyVisualPose();
        }

        public void ResetInteractionState()
        {
            StopFeedback();
            _hovered = false;
            _selected = false;
            if (selectionRing != null)
            {
                selectionRing.enabled = false;
            }

            if (selectionMarker != null)
            {
                selectionMarker.SetActive(false);
            }

            ApplyVisualPose();
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
            _hovered = true;
            ApplyVisualPose();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            ApplyVisualPose();
        }

        private IEnumerator FeedbackRoutine(float duration)
        {
            if (duration <= 0f)
            {
                yield break;
            }

            var target = VisualTarget;
            Vector3 startScale = target.localScale;
            Vector3 peak = _baseVisualScale * 1.12f;
            float half = duration * 0.5f;
            float elapsed = 0f;

            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                target.localScale = Vector3.Lerp(startScale, peak, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                target.localScale = Vector3.Lerp(peak, _baseVisualScale, t);
                yield return null;
            }

            ApplyVisualPose();
            _feedbackRoutine = null;
        }

        private void StopFeedback()
        {
            if (_feedbackRoutine == null)
            {
                return;
            }

            StopCoroutine(_feedbackRoutine);
            _feedbackRoutine = null;
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
                markText = GetComponentInChildren<TextMeshPro>(true);
            }
            if (markText != null)
            {
                PrepareMarkText();
            }

            if (visualRoot == null)
            {
                visualRoot = tileRenderer != null ? tileRenderer.transform : transform;
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

        private void CacheVisualPose()
        {
            var target = VisualTarget;
            _baseVisualScale = target.localScale;
            _baseVisualPosition = target.localPosition;
        }

        private void ApplyVisualPose()
        {
            var target = VisualTarget;
            float scale = _hovered ? hoverScale : 1f;
            target.localScale = _baseVisualScale * scale;
            target.localPosition = _baseVisualPosition + (_selected ? Vector3.up * selectedLift : Vector3.zero);
        }

        private void PrepareMarkText()
        {
            markText.enableCulling = false;
            markText.enableWordWrapping = false;
            markText.overflowMode = TextOverflowModes.Overflow;

            var textRenderer = markText.GetComponent<Renderer>();
            if (textRenderer != null)
            {
                textRenderer.sortingOrder = 2;
            }
        }
    }
}
