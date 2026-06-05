using System;
using System.Collections;
using QuixoUnity.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QuixoUnity.UI
{
    public sealed class BoardCellView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private MeshRenderer tileRenderer = null!;
        [SerializeField] private Transform visualRoot = null!;
        [SerializeField] private TextMeshPro markText = null!;
        [SerializeField] private Image selectionRing = null!;
        [SerializeField] private GameObject selectionMarker = null!;
        [SerializeField] private GameObject dotMarker = null!;
        [SerializeField] private GameObject emptyTokenMarker = null!;
        [SerializeField] private GameObject player1Token = null!;
        [SerializeField] private GameObject player2Token = null!;
        [SerializeField] private bool useTokenVisuals;
        [SerializeField] private Color emptyColor = new(0.92f, 0.82f, 0.62f);
        [SerializeField] private Color selectedColor = new(1f, 0.9f, 0.64f);
        [SerializeField] private Color player1Color = new(0.04f, 0.14f, 0.34f);
        [SerializeField] private Color player2Color = new(0.62f, 0.18f, 0.09f);
        [SerializeField] private float hoverScale = 1.04f;
        [SerializeField] private float selectedLift = 0.09f;
        [SerializeField] private float markFlipDuration = 0.24f;
        [SerializeField] private float dragFeedbackDistance = 0.14f;

        private int _row;
        private int _col;
        private Action<int, int> _onClick = null!;
        private Action<int, int, Vector2> _onDragComplete = null!;
        private Vector3 _baseScale;
        private Vector3 _baseVisualScale;
        private Vector3 _baseVisualPosition;
        private Quaternion _baseVisualRotation;
        private PlayerMark _currentMark = PlayerMark.None;
        private QuixoDotOwner _dotOwner = QuixoDotOwner.None;
        private bool _hovered;
        private bool _selected;
        private bool _hasRenderedState;
        private bool _markAnimating;
        private bool _dragging;
        private Vector2 _dragStartPosition;
        private Vector2 _dragDelta;
        private Vector3 _dragVisualOffset;
        private Coroutine _feedbackRoutine = null!;
        private Coroutine _markRoutine = null!;

        private Transform VisualTarget => visualRoot != null ? visualRoot : transform;

        private void Awake()
        {
            ResolveReferences();
            _baseScale = transform.localScale;
            CacheVisualPose();
        }

        public void Initialize(int row, int col, Action<int, int> onClick, Action<int, int, Vector2> onDragComplete = null)
        {
            _row = row;
            _col = col;
            _onClick = onClick;
            _onDragComplete = onDragComplete;
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

        public void ConfigureTokenReferences(GameObject emptyMarker, GameObject player1, GameObject player2)
        {
            emptyTokenMarker = emptyMarker;
            player1Token = player1;
            player2Token = player2;
            useTokenVisuals = emptyTokenMarker != null || player1Token != null || player2Token != null;

            if (markText != null && useTokenVisuals)
            {
                markText.gameObject.SetActive(false);
            }

            ApplyTokenState(_currentMark);
        }

        public void ConfigureDotReference(GameObject marker)
        {
            dotMarker = marker;
            ApplyDotState(_currentMark, _dotOwner);
        }

        public void ConfigureInteractionStyle(float hoverScaleOverride, float selectedLiftOverride)
        {
            hoverScale = hoverScaleOverride;
            selectedLift = selectedLiftOverride;
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
            SetState(mark, selected, QuixoDotOwner.None);
        }

        public void SetState(PlayerMark mark, bool selected, QuixoDotOwner dotOwner)
        {
            _selected = selected;
            _dotOwner = dotOwner;
            if (useTokenVisuals)
            {
                StopMarkFlip();
                _currentMark = mark;
                _hasRenderedState = true;
                if (markText != null)
                {
                    markText.gameObject.SetActive(false);
                }

                ApplyTokenState(mark);
                ApplyDotState(mark, dotOwner);
                SetSelectionVisible(selected);
                ApplyVisualPose();
                return;
            }

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

            SetSelectionVisible(selected);
            ApplyDotState(mark, dotOwner);
            ApplyVisualPose();
        }

        public void ResetInteractionState()
        {
            StopFeedback();
            StopMarkFlip();
            _hovered = false;
            _selected = false;
            _dragging = false;
            _dragDelta = Vector2.zero;
            _dragVisualOffset = Vector3.zero;
            _currentMark = PlayerMark.None;
            _dotOwner = QuixoDotOwner.None;
            _hasRenderedState = false;
            ApplyTokenState(PlayerMark.None);
            ApplyDotState(PlayerMark.None, QuixoDotOwner.None);
            SetSelectionVisible(false);

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

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = true;
            _dragStartPosition = eventData != null ? eventData.position : Vector2.zero;
            _dragDelta = Vector2.zero;
            _dragVisualOffset = Vector3.zero;
            ApplyVisualPose();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || eventData == null)
            {
                return;
            }

            _dragDelta = eventData.position - _dragStartPosition;
            _dragVisualOffset = DragOffsetFromScreenDelta(_dragDelta);
            ApplyVisualPose();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }

            Vector2 finalDelta = eventData != null ? eventData.position - _dragStartPosition : _dragDelta;
            _dragging = false;
            _dragDelta = Vector2.zero;
            _dragVisualOffset = Vector3.zero;
            ApplyVisualPose();
            _onDragComplete?.Invoke(_row, _col, finalDelta);
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

            if (dotMarker != null)
            {
                dotMarker.SetActive(false);
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
            Vector3 selectionLift = _selected ? Vector3.up * selectedLift : Vector3.zero;
            Vector3 dragLift = _dragging ? Vector3.up * (selectedLift * 0.55f) : Vector3.zero;
            target.localPosition = _baseVisualPosition + selectionLift + dragLift + _dragVisualOffset;
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

        private void ApplyTokenState(PlayerMark mark)
        {
            if (!useTokenVisuals)
            {
                return;
            }

            SetObjectActive(emptyTokenMarker, mark == PlayerMark.None);
            SetObjectActive(player1Token, mark == PlayerMark.Player1);
            SetObjectActive(player2Token, mark == PlayerMark.Player2);
        }

        private void ApplyDotState(PlayerMark mark, QuixoDotOwner owner)
        {
            if (dotMarker == null)
            {
                return;
            }

            bool visible = mark != PlayerMark.None && owner != QuixoDotOwner.None;
            dotMarker.SetActive(visible);
            if (!visible)
            {
                return;
            }

            const float edge = 0.285f;
            const float y = 0.500f;
            Vector3 position = owner switch
            {
                QuixoDotOwner.Team1Player1 => new Vector3(0f, y, -edge),
                QuixoDotOwner.Team1Player2 => new Vector3(0f, y, edge),
                QuixoDotOwner.Team2Player1 => new Vector3(edge, y, 0f),
                QuixoDotOwner.Team2Player2 => new Vector3(-edge, y, 0f),
                _ => new Vector3(0f, y, 0f)
            };

            dotMarker.transform.localPosition = position;
        }

        private void SetSelectionVisible(bool selected)
        {
            if (selectionRing != null)
            {
                selectionRing.enabled = selected;
            }

            if (selectionMarker != null)
            {
                selectionMarker.SetActive(selected);
            }
        }

        private void ApplyFlipPose(Transform target, float angle, float scale)
        {
            target.localScale = _baseVisualScale * scale;
            target.localPosition = _baseVisualPosition + (_selected ? Vector3.up * selectedLift : Vector3.zero) + _dragVisualOffset;
            target.localRotation = _baseVisualRotation * Quaternion.Euler(0f, angle, 0f);
        }

        private Vector3 DragOffsetFromScreenDelta(Vector2 delta)
        {
            if (delta.sqrMagnitude < 1f)
            {
                return Vector3.zero;
            }

            Vector2 direction = delta.normalized;
            return new Vector3(direction.x, 0f, direction.y) * dragFeedbackDistance;
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

        private static void SetObjectActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
