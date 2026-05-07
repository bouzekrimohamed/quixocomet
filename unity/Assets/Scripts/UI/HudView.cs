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
        [SerializeField] private Color turnPlayer1Color = new(0.04f, 0.14f, 0.34f);
        [SerializeField] private Color turnPlayer2Color = new(0.62f, 0.18f, 0.09f);

        private GameFlowController _controller = null!;
        private CanvasGroup _infoCanvasGroup = null!;
        private Coroutine _infoRoutine = null!;
        private Coroutine _turnPulseRoutine = null!;
        private readonly Dictionary<Button, Coroutine> _directionRoutines = new();
        private readonly Dictionary<Button, bool> _directionState = new();
        private readonly Dictionary<Button, ColorBlock> _directionBaseColors = new();
        private readonly Dictionary<Button, Vector3> _directionBaseScales = new();
        private Vector3 _turnBaseScale = Vector3.one;

        private void Awake()
        {
            ResolveReferences();
            ApplyActiveTheme();

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
            ResolveReferences();
            if (controller == null)
            {
                Debug.LogError("HudView: controller is not assigned.", this);
                return;
            }

            _controller = controller;
            ApplyActiveTheme();
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
            turnLabel.color = player == PlayerMark.Player1 ? turnPlayer1Color : turnPlayer2Color;

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

            if (_directionRoutines.ContainsKey(button))
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

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void PlayUp()
        {
            TryPlayDirection(upButton, MoveDirection.Up);
        }

        private void PlayDown()
        {
            TryPlayDirection(downButton, MoveDirection.Down);
        }

        private void PlayLeft()
        {
            TryPlayDirection(leftButton, MoveDirection.Left);
        }

        private void PlayRight()
        {
            TryPlayDirection(rightButton, MoveDirection.Right);
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

        private void TryPlayDirection(Button button, MoveDirection direction)
        {
            if (_controller == null || button == null || !button.interactable)
            {
                return;
            }

            _controller.PlayDirection(direction);
        }

        private void ResolveReferences()
        {
            turnLabel ??= FindChildComponent<TextMeshProUGUI>("TurnLabel");
            infoLabel ??= FindChildComponent<TextMeshProUGUI>("InfoLabel");
            restartButton ??= FindChildComponent<Button>("RestartButton");
            menuButton ??= FindChildComponent<Button>("MenuButton");
            upButton ??= FindChildComponent<Button>("UpButton");
            downButton ??= FindChildComponent<Button>("DownButton");
            leftButton ??= FindChildComponent<Button>("LeftButton");
            rightButton ??= FindChildComponent<Button>("RightButton");

            CacheDirectionButton(upButton);
            CacheDirectionButton(downButton);
            CacheDirectionButton(leftButton);
            CacheDirectionButton(rightButton);
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
            turnPlayer1Color = palette.Player1;
            turnPlayer2Color = palette.Player2;

            if (turnLabel != null)
            {
                turnLabel.color = turnPlayer1Color;
            }

            if (infoLabel != null)
            {
                infoLabel.color = palette.UiMuted;
            }

            ApplyButtonTheme(restartButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButtonTheme(menuButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButtonTheme(upButton, palette.UiButton, palette.UiText, palette.UiButtonDisabled);
            ApplyButtonTheme(downButton, palette.UiButton, palette.UiText, palette.UiButtonDisabled);
            ApplyButtonTheme(leftButton, palette.UiButton, palette.UiText, palette.UiButtonDisabled);
            ApplyButtonTheme(rightButton, palette.UiButton, palette.UiText, palette.UiButtonDisabled);
        }

        private void ApplyButtonTheme(Button button, Color normalColor, Color textColor, Color disabledColor)
        {
            if (button == null)
            {
                return;
            }

            var colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = disabledColor;
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = button.interactable ? normalColor : disabledColor;
            }

            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.color = textColor;
            }

            if (_directionBaseColors.ContainsKey(button))
            {
                _directionBaseColors[button] = colors;
            }
        }

        private T FindChildComponent<T>(string childName) where T : Component
        {
            var components = GetComponentsInChildren<T>(true);
            foreach (var component in components)
            {
                if (component.name == childName)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
