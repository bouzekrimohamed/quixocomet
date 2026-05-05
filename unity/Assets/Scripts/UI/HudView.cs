using System.Collections;
using System.Collections.Generic;
using QuixoUnity.Core;
using QuixoUnity.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace QuixoUnity.UI
{
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI turnLabel = null!;
        [SerializeField] private TextMeshProUGUI infoLabel = null!;
        [SerializeField] private Button restartButton = null!;
        [SerializeField] private Button menuButton = null!;
        [SerializeField] private Button upButton = null!;
        [SerializeField] private Button downButton = null!;
        [SerializeField] private Button leftButton = null!;
        [SerializeField] private Button rightButton = null!;
        [Header("Animation settings")]
        [SerializeField] private float pulseDuration = 0.18f;
        [SerializeField] private float fadeDuration = 0.2f;
        [SerializeField] private float directionScaleOnEnable = 1.08f;

        private GameFlowController _controller = null!;
        private CanvasGroup _infoCanvasGroup = null!;
        private Coroutine _infoRoutine = null!;
        private Coroutine _turnPulseRoutine = null!;
        private readonly Dictionary<Button, Coroutine> _directionRoutines = new();
        private readonly Dictionary<Button, bool> _directionState = new();
        private readonly Dictionary<Button, ColorBlock> _directionBaseColors = new();
        private readonly Dictionary<Button, Vector3> _directionBaseScales = new();
        private Vector3 _turnBaseScale = Vector3.one;

        private static readonly Color TurnPlayer1Color = new(0.27f, 0.65f, 0.99f);
        private static readonly Color TurnPlayer2Color = new(1f, 0.49f, 0.2f);

        private void Awake()
        {
            if (turnLabel != null)
            {
                _turnBaseScale = turnLabel.rectTransform.localScale;
            }

            if (infoLabel != null)
            {
                _infoCanvasGroup = infoLabel.GetComponent<CanvasGroup>();
                if (_infoCanvasGroup == null)
                {
                    _infoCanvasGroup = infoLabel.gameObject.AddComponent<CanvasGroup>();
                }
            }
            else
            {
                Debug.LogError("HudView: infoLabel is not assigned.", this);
            }

            CacheDirectionButton(upButton);
            CacheDirectionButton(downButton);
            CacheDirectionButton(leftButton);
            CacheDirectionButton(rightButton);
        }

        private void OnDisable()
        {
            StopRunningCoroutine(ref _infoRoutine);
            StopRunningCoroutine(ref _turnPulseRoutine);

            foreach (var pair in _directionRoutines)
            {
                if (pair.Value != null)
                {
                    StopCoroutine(pair.Value);
                }
            }
        }

        public void Bind(GameFlowController controller)
        {
            if (controller == null)
            {
                Debug.LogError("HudView: controller is not assigned.", this);
                return;
            }

            _controller = controller;
            BindButton(restartButton, controller.RestartGame);
            BindButton(menuButton, controller.ReturnToMenu);
            BindButton(upButton, PlayUp);
            BindButton(downButton, PlayDown);
            BindButton(leftButton, PlayLeft);
            BindButton(rightButton, PlayRight);
        }

        public void SetTurn(PlayerMark player)
        {
            if (turnLabel == null)
            {
                return;
            }

            turnLabel.text = $"Tour: {(player == PlayerMark.Player1 ? "Joueur 1 (X)" : "Joueur 2 (O)")}";
            turnLabel.color = player == PlayerMark.Player1 ? TurnPlayer1Color : TurnPlayer2Color;

            if (_turnPulseRoutine != null)
            {
                StopCoroutine(_turnPulseRoutine);
            }

            if (isActiveAndEnabled)
            {
                _turnPulseRoutine = StartCoroutine(PulseText(turnLabel.rectTransform, _turnBaseScale, pulseDuration));
            }
        }

        public void SetInfo(string message)
        {
            if (infoLabel == null)
            {
                return;
            }

            infoLabel.text = message;

            if (_infoRoutine != null)
            {
                StopCoroutine(_infoRoutine);
            }

            if (_infoCanvasGroup == null || !isActiveAndEnabled || fadeDuration <= 0f)
            {
                if (_infoCanvasGroup != null)
                {
                    _infoCanvasGroup.alpha = 1f;
                }

                return;
            }

            _infoRoutine = StartCoroutine(FadeInfoLabel());
        }

        public void SetDirections(IReadOnlyList<MoveDirection> allowed)
        {
            bool has = allowed != null;
            var set = has ? new HashSet<MoveDirection>(allowed) : new HashSet<MoveDirection>();
            SetDirectionState(upButton, set.Contains(MoveDirection.Up));
            SetDirectionState(downButton, set.Contains(MoveDirection.Down));
            SetDirectionState(leftButton, set.Contains(MoveDirection.Left));
            SetDirectionState(rightButton, set.Contains(MoveDirection.Right));
        }

        private void CacheDirectionButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            _directionRoutines[button] = null;
            _directionState[button] = button.interactable;
            _directionBaseColors[button] = button.colors;
            _directionBaseScales[button] = button.transform.localScale;
        }

        private void SetDirectionState(Button button, bool isInteractable)
        {
            if (button == null)
            {
                return;
            }

            bool previous = _directionState.TryGetValue(button, out bool oldState) && oldState;
            button.interactable = isInteractable;
            _directionState[button] = isInteractable;

            if (previous == isInteractable)
            {
                return;
            }

            if (_directionRoutines.TryGetValue(button, out Coroutine running) && running != null)
            {
                StopCoroutine(running);
            }

            if (isActiveAndEnabled)
            {
                _directionRoutines[button] = StartCoroutine(AnimateDirectionButton(button, isInteractable));
            }
        }

        private IEnumerator FadeInfoLabel()
        {
            _infoCanvasGroup.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                _infoCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            _infoCanvasGroup.alpha = 1f;
        }

        private IEnumerator PulseText(RectTransform target, Vector3 baseScale, float duration)
        {
            if (target == null || duration <= 0f)
            {
                yield break;
            }

            Vector3 peak = baseScale * 1.08f;
            float half = duration * 0.5f;
            float elapsed = 0f;

            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                target.localScale = Vector3.Lerp(baseScale, peak, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                target.localScale = Vector3.Lerp(peak, baseScale, t);
                yield return null;
            }

            target.localScale = baseScale;
        }

        private IEnumerator AnimateDirectionButton(Button button, bool enabledNow)
        {
            RectTransform rect = button.transform as RectTransform;
            if (rect == null)
            {
                yield break;
            }

            if (!_directionBaseScales.TryGetValue(button, out Vector3 baseScale))
            {
                baseScale = Vector3.one;
            }

            if (_directionBaseColors.TryGetValue(button, out ColorBlock baseColors))
            {
                button.colors = baseColors;
                if (button.targetGraphic != null)
                {
                    Color targetColor = enabledNow ? baseColors.normalColor : baseColors.disabledColor;
                    button.targetGraphic.CrossFadeColor(targetColor, fadeDuration, true, true);
                }
            }

            Vector3 fromScale = enabledNow ? baseScale * 0.96f : baseScale;
            Vector3 toScale = enabledNow ? baseScale * directionScaleOnEnable : baseScale * 0.96f;

            float elapsed = 0f;
            while (elapsed < pulseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / pulseDuration);
                rect.localScale = Vector3.Lerp(fromScale, toScale, t);
                yield return null;
            }

            rect.localScale = baseScale;
        }

        private void BindButton(Button button, UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void PlayUp()
        {
            _controller?.PlayDirection(MoveDirection.Up);
        }

        private void PlayDown()
        {
            _controller?.PlayDirection(MoveDirection.Down);
        }

        private void PlayLeft()
        {
            _controller?.PlayDirection(MoveDirection.Left);
        }

        private void PlayRight()
        {
            _controller?.PlayDirection(MoveDirection.Right);
        }

        private void StopRunningCoroutine(ref Coroutine routine)
        {
            if (routine == null)
            {
                return;
            }

            StopCoroutine(routine);
            routine = null;
        }
    }
}
