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
        [SerializeField] private float markFlipDuration = 0.24f;

        private int _row;
        private int _col;
        private Action<int, int> _onClick = null!;
        private Vector3 _baseScale;
        private Vector3 _baseVisualScale;
        private Vector3 _baseVisualPosition;
        private Quaternion _baseVisualRotation;
        private PlayerMark _currentMark = PlayerMark.None;
        private bool _hovered;
        private bool _selected;
        private bool _hasRenderedState;
        private bool _markAnimating;
        private Coroutine _feedbackRoutine = null!;
        private Coroutine _markRoutine = null!;

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

        public void ConfigureMarkFontSize(float fontSize)
        {
            if (markText != null && fontSize > 0f)
            {
                markText.fontSize = fontSize;
                markText.ForceMeshUpdate();
            }
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

            Color markColor = mark switch
            {
                PlayerMark.Player1 => player1Color,
                PlayerMark.Player2 => player2Color,
                _ => Color.clear,
            };

            bool shouldFlip = _hasRenderedState && _currentMark == PlayerMark.None && mark != PlayerMark.None;
            _currentMark = mark;
            _hasRenderedState = true;

            if (markText != null)
            {
                if (shouldFlip && isActiveAndEnabled)
                {
                    PlayMarkFlip(text, markColor);
                }
                else
                {
                    StopMarkFlip();
                    ApplyMarkText(text, markColor);
                }
            }

            if (tileRenderer != null)
            {
                ApplyRendererColor(tileRenderer, selected ? selectedColor : emptyColor);
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
            StopMarkFlip();
            _hovered = false;
            _selected = false;
            _currentMark = PlayerMark.None;
            _hasRenderedState = false;
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

        private void OnDisable()
        {
            StopFeedback();
            StopMarkFlip();
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

        private void PlayMarkFlip(string text, Color color)
        {
            StopFeedback();
            StopMarkFlip();
            ApplyMarkText(string.Empty, Color.clear);
            _markRoutine = StartCoroutine(MarkFlipRoutine(text, color, markFlipDuration));
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

        private IEnumerator MarkFlipRoutine(string text, Color color, float duration)
        {
            var target = VisualTarget;
            if (target == null || duration <= 0f)
            {
                ApplyMarkText(text, color);
                yield break;
            }

            _markAnimating = true;
            float half = Mathf.Max(duration * 0.5f, 0.01f);
            float elapsed = 0f;
            const float peakAngle = 82f;

            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                ApplyFlipPose(target, Mathf.Lerp(0f, peakAngle, EaseOutCubic(t)), Mathf.Lerp(1f, 1.06f, t));
                yield return null;
            }

            ApplyMarkText(text, color);

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                ApplyFlipPose(target, Mathf.Lerp(peakAngle, 0f, EaseOutCubic(t)), Mathf.Lerp(1.06f, 1f, t));
                yield return null;
            }

            _markAnimating = false;
            _markRoutine = null;
            ApplyVisualPose();
        }

        private void StopMarkFlip()
        {
            if (_markRoutine != null)
            {
                StopCoroutine(_markRoutine);
                _markRoutine = null;
            }

            _markAnimating = false;
            var target = VisualTarget;
            if (target != null)
            {
                target.localRotation = _baseVisualRotation;
            }
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
            _baseVisualRotation = target.localRotation;
        }

        private void ApplyVisualPose()
        {
            var target = VisualTarget;
            float scale = _hovered ? hoverScale : 1f;
            target.localScale = _baseVisualScale * scale;
            target.localPosition = _baseVisualPosition + (_selected ? Vector3.up * selectedLift : Vector3.zero);
            if (!_markAnimating)
            {
                target.localRotation = _baseVisualRotation;
            }
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

        private void ApplyMarkText(string text, Color color)
        {
            if (markText == null)
            {
                return;
            }

            PrepareMarkText();
            markText.gameObject.SetActive(true);
            markText.text = text;
            markText.color = color;
            markText.ForceMeshUpdate();
        }

        private void ApplyFlipPose(Transform target, float angle, float scale)
        {
            target.localScale = _baseVisualScale * scale;
            target.localPosition = _baseVisualPosition + (_selected ? Vector3.up * selectedLift : Vector3.zero);
            target.localRotation = _baseVisualRotation * Quaternion.Euler(0f, angle, 0f);
        }

        private static float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        private static void ApplyRendererColor(Renderer targetRenderer, Color color)
        {
            if (targetRenderer == null || targetRenderer.material == null)
            {
                return;
            }

            var material = targetRenderer.material;
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
    }
}
