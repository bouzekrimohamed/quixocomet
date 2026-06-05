using System;
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
        [SerializeField] private TextMeshProUGUI timerLabel = null!;
        [SerializeField] private Image timerFill = null!;
        [SerializeField] private Button restartButton = null!;
        [SerializeField] private Button menuButton = null!;
        [SerializeField] private Button upButton = null!;
        [SerializeField] private Button downButton = null!;
        [SerializeField] private Button leftButton = null!;
        [SerializeField] private Button rightButton = null!;
        [SerializeField] private GameObject quixoHelpPanel = null!;
        [SerializeField] private GameObject team2v2Panel = null!;
        [SerializeField] private TextMeshProUGUI team2v2InfoLabel = null!;
        [SerializeField] private Button dotSelfButton = null!;
        [SerializeField] private Button dotTeammateButton = null!;
        [SerializeField] private GameObject gameOverPanel = null!;
        [SerializeField] private TextMeshProUGUI gameOverHeadingLabel = null!;
        [SerializeField] private TextMeshProUGUI gameOverTitleLabel = null!;
        [SerializeField] private TextMeshProUGUI gameOverSubtitleLabel = null!;
        [SerializeField] private Button gameOverMenuButton = null!;
        [SerializeField] private Button gameOverReplayButton = null!;
        [Header("Animation settings")]
        [SerializeField] private float pulseDuration = 0.18f;
        [SerializeField] private float fadeDuration = 0.2f;
        [SerializeField] private float directionScaleOnEnable = 1.08f;
        [SerializeField] private Color turnPlayer1Color = new(0.04f, 0.14f, 0.34f);
        [SerializeField] private Color turnPlayer2Color = new(0.62f, 0.18f, 0.09f);
        [SerializeField] private GameKind gameKind = GameKind.Quixo;

        // Event leve quand le timer du joueur courant atteint 0.
        // GameFlowController s'y abonne pour declencher la perte par inactivite.
        public event Action TurnTimedOut;

        private float _turnTimerTotalSeconds;
        private float _turnTimerRemaining;
        private bool _turnTimerRunning;
        private bool _turnTimerExpiredFired;
        private bool _turnTimerForLocalPlayer = true;
        private string _turnTimerOwnerLabel = string.Empty;
        private string _turnTimerCadenceLabel = string.Empty;
        // Couleurs de base du timer capturees une fois depuis le theme. Permet de revenir
        // a la couleur normale apres etre passe en rouge (sinon le label restait rouge le
        // tour suivant).
        private Color _timerLabelBaseColor = Color.white;
        private Color _timerFillBaseColor = Color.white;
        private bool _timerBaseColorsCaptured;
        private static readonly Color TimerWarningTextColor = new(0.95f, 0.30f, 0.30f, 1f);
        private static readonly Color TimerWarningFillColor = new(0.95f, 0.30f, 0.30f, 0.92f);

        private GameFlowController _controller = null!;
        private CanvasGroup _infoCanvasGroup = null!;
        private Coroutine _infoRoutine = null!;
        private Coroutine _turnPulseRoutine = null!;
        private readonly Dictionary<Button, Coroutine> _directionRoutines = new();
        private readonly Dictionary<Button, bool> _directionState = new();
        private readonly Dictionary<Button, ColorBlock> _directionBaseColors = new();
        private readonly Dictionary<Button, Vector3> _directionBaseScales = new();
        private Vector3 _turnBaseScale = Vector3.one;
        private bool _gameOverVisible;
        private bool _dotTowardTeammate;

        public bool DotTowardTeammate => _dotTowardTeammate;

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
            EnsureAuxiliaryPanels();
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

            // On garde le timer en memoire mais on l'arrete pour eviter qu'il tique en arriere-plan.
            _turnTimerRunning = false;
        }

        private void Update()
        {
            if (!_turnTimerRunning)
            {
                return;
            }

            // Tic basique en temps reel. Time.unscaledDeltaTime evite qu'un Time.timeScale=0
            // ne fige le timer (utile en cas de pause UI).
            _turnTimerRemaining -= Time.unscaledDeltaTime;
            if (_turnTimerRemaining <= 0f)
            {
                _turnTimerRemaining = 0f;
                _turnTimerRunning = false;
                RenderTimer();
                if (!_turnTimerExpiredFired)
                {
                    _turnTimerExpiredFired = true;
                    TurnTimedOut?.Invoke();
                }

                return;
            }

            RenderTimer();
        }

        /// <summary>
        /// Demarre ou redemarre le timer pour le tour courant. seconds <= 0 = sans limite.
        /// isLocalTurn precise s'il s'agit du tour du joueur local (couleur differente).
        /// </summary>
        public void StartTurnTimer(int seconds, bool isLocalTurn)
        {
            StartTurnTimer(seconds, isLocalTurn, string.Empty, string.Empty);
        }

        public void StartTurnTimer(float seconds, bool isLocalTurn, string ownerLabel, string cadenceLabel)
        {
            ResolveReferences();
            _turnTimerForLocalPlayer = isLocalTurn;
            _turnTimerOwnerLabel = ownerLabel ?? string.Empty;
            _turnTimerCadenceLabel = cadenceLabel ?? string.Empty;
            _turnTimerExpiredFired = false;
            if (seconds <= 0)
            {
                _turnTimerTotalSeconds = 0;
                _turnTimerRemaining = 0f;
                _turnTimerRunning = false;
                RenderTimerUnlimited();
                return;
            }

            _turnTimerTotalSeconds = Mathf.Max(1f, seconds);
            _turnTimerRemaining = seconds;
            _turnTimerRunning = true;
            RenderTimer();
        }

        public void StopTurnTimer()
        {
            _turnTimerRunning = false;
            _turnTimerRemaining = 0f;
            _turnTimerExpiredFired = false;
            RenderTimerCleared();
        }

        public bool IsTurnTimerRunning => _turnTimerRunning;
        public float CurrentTurnTimeRemaining => _turnTimerRemaining;

        private void RenderTimer()
        {
            if (timerLabel != null)
            {
                int displaySeconds = Mathf.CeilToInt(_turnTimerRemaining);
                if (displaySeconds < 0)
                {
                    displaySeconds = 0;
                }

                string owner = string.IsNullOrWhiteSpace(_turnTimerOwnerLabel)
                    ? (_turnTimerForLocalPlayer ? "Vous" : "Adversaire")
                    : _turnTimerOwnerLabel;
                string cadence = string.IsNullOrWhiteSpace(_turnTimerCadenceLabel)
                    ? string.Empty
                    : $" ({_turnTimerCadenceLabel})";
                timerLabel.text = $"{owner} : {FormatClock(displaySeconds)}{cadence}";

                // Passe en rouge quand il reste moins de 6s pour avertir le joueur local,
                // sinon revient a la couleur de base (sinon le label restait rouge le tour
                // suivant).
                timerLabel.color = _turnTimerForLocalPlayer && displaySeconds <= 5
                    ? TimerWarningTextColor
                    : _timerLabelBaseColor;
            }

            if (timerFill != null && _turnTimerTotalSeconds > 0)
            {
                float ratio = Mathf.Clamp01(_turnTimerRemaining / _turnTimerTotalSeconds);
                timerFill.fillAmount = ratio;
                timerFill.color = _turnTimerForLocalPlayer && ratio < 0.25f
                    ? TimerWarningFillColor
                    : _timerFillBaseColor;
            }
        }

        private void RenderTimerUnlimited()
        {
            if (timerLabel != null)
            {
                timerLabel.text = "Cadence : sans limite";
                timerLabel.color = _timerLabelBaseColor;
            }

            if (timerFill != null)
            {
                timerFill.fillAmount = 1f;
                timerFill.color = _timerFillBaseColor;
            }
        }

        private void RenderTimerCleared()
        {
            if (timerLabel != null)
            {
                timerLabel.text = string.Empty;
                timerLabel.color = _timerLabelBaseColor;
            }

            if (timerFill != null)
            {
                timerFill.fillAmount = 0f;
                timerFill.color = _timerFillBaseColor;
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
            BindButton(gameOverMenuButton, controller.ReturnToMenu);
            BindButton(gameOverReplayButton, controller.RestartGame);
            HideGameOver();
        }

        public void SetGameKind(GameKind kind)
        {
            gameKind = kind;
            EnsureAuxiliaryPanels();
            ApplyActiveTheme();
            SetDirectionControlsVisible(gameKind != GameKind.Qomet);
            SetObjectVisible(quixoHelpPanel, gameKind == GameKind.Quixo);
            if (gameKind == GameKind.Qomet)
            {
                SetTeam2v2Hud(false, string.Empty);
            }
        }

        private static string FormatClock(int totalSeconds)
        {
            int safe = Mathf.Max(0, totalSeconds);
            int minutes = safe / 60;
            int seconds = safe % 60;
            return $"{minutes:00}:{seconds:00}";
        }

        public void SetTeam2v2Hud(bool visible, string info)
        {
            EnsureAuxiliaryPanels();
            SetObjectVisible(team2v2Panel, visible && gameKind == GameKind.Quixo);
            if (!visible)
            {
                _dotTowardTeammate = false;
            }

            if (team2v2InfoLabel != null)
            {
                team2v2InfoLabel.text = string.IsNullOrWhiteSpace(info) ? string.Empty : info;
            }

            RefreshDotChoiceButtons();
        }

        public void SetTurn(PlayerMark player)
        {
            SetTurn(player, null);
        }

        /// <summary>
        /// Affiche le joueur courant. Si customLabel est non vide, il remplace le texte standard
        /// (utile pour montrer "A vous de jouer" en online sans casser le code local).
        /// </summary>
        public void SetTurn(PlayerMark player, string customLabel)
        {
            if (turnLabel == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(customLabel))
            {
                turnLabel.text = customLabel;
            }
            else
            {
                turnLabel.text = gameKind == GameKind.Qomet
                    ? $"Tour : {(player == PlayerMark.Player1 ? "Joueur 1 jaune" : "Joueur 2 rouge")}"
                    : $"Tour : {(player == PlayerMark.Player1 ? "Joueur 1 (X)" : "Joueur 2 (O)")}";
            }

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

        public void SetRestartEnabled(bool enabled)
        {
            if (restartButton != null)
            {
                restartButton.interactable = enabled;
            }
        }

        public void ShowGameOver(string title, string subtitle, bool allowReplay)
        {
            ResolveReferences();
            if (_gameOverVisible)
            {
                return;
            }

            if (gameOverPanel == null)
            {
                Debug.LogWarning("HudView: game over panel is missing. Regenerate the scenes.", this);
                return;
            }

            _gameOverVisible = true;
            // On stoppe le timer des qu'une fin de partie est affichee : evite qu'il continue
            // a tiquer pendant que la popup est visible.
            StopTurnTimer();
            if (gameOverTitleLabel != null)
            {
                gameOverTitleLabel.text = string.IsNullOrWhiteSpace(title) ? "Partie terminee" : title;
            }

            if (gameOverSubtitleLabel != null)
            {
                gameOverSubtitleLabel.text = allowReplay
                    ? subtitle
                    : $"{subtitle}\nRejouer indisponible en ligne.";
            }

            if (gameOverReplayButton != null)
            {
                gameOverReplayButton.gameObject.SetActive(allowReplay);
                gameOverReplayButton.interactable = allowReplay;
            }

            gameOverPanel.SetActive(true);
        }

        public void HideGameOver()
        {
            _gameOverVisible = false;
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }

            // Sortie de game over : on remet le timer dans un etat propre. GameFlowController
            // appelle StartTurnTimer juste apres si une partie redemarre.
            StopTurnTimer();
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
            timerLabel ??= FindChildComponent<TextMeshProUGUI>("TimerLabel");
            timerFill ??= FindChildComponent<Image>("TimerFill");
            if (!_timerBaseColorsCaptured)
            {
                if (timerLabel != null)
                {
                    _timerLabelBaseColor = timerLabel.color;
                }

                if (timerFill != null)
                {
                    _timerFillBaseColor = timerFill.color;
                }

                _timerBaseColorsCaptured = timerLabel != null && timerFill != null;
            }

            restartButton ??= FindChildComponent<Button>("RestartButton");
            menuButton ??= FindChildComponent<Button>("MenuButton");
            upButton ??= FindChildComponent<Button>("UpButton");
            downButton ??= FindChildComponent<Button>("DownButton");
            leftButton ??= FindChildComponent<Button>("LeftButton");
            rightButton ??= FindChildComponent<Button>("RightButton");
            gameOverPanel ??= FindChildComponent<Transform>("GameOverPanel")?.gameObject;
            gameOverHeadingLabel ??= FindChildComponent<TextMeshProUGUI>("GameOverHeadingLabel");
            gameOverTitleLabel ??= FindChildComponent<TextMeshProUGUI>("GameOverTitleLabel");
            gameOverSubtitleLabel ??= FindChildComponent<TextMeshProUGUI>("GameOverSubtitleLabel");
            gameOverMenuButton ??= FindChildComponent<Button>("GameOverMenuButton");
            gameOverReplayButton ??= FindChildComponent<Button>("GameOverReplayButton");
            quixoHelpPanel ??= FindChildComponent<Transform>("QuixoHelpPanel")?.gameObject;
            team2v2Panel ??= FindChildComponent<Transform>("Team2v2HelpPanel")?.gameObject;
            team2v2InfoLabel ??= FindChildComponent<TextMeshProUGUI>("Team2v2InfoLabel");
            dotSelfButton ??= FindChildComponent<Button>("DotSelfButton");
            dotTeammateButton ??= FindChildComponent<Button>("DotTeammateButton");

            CacheDirectionButton(upButton);
            CacheDirectionButton(downButton);
            CacheDirectionButton(leftButton);
            CacheDirectionButton(rightButton);
            EnsureAuxiliaryPanels();
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
            if (gameKind == GameKind.Qomet)
            {
                turnPlayer1Color = new Color(1.000f, 0.700f, 0.155f, 1f);
                turnPlayer2Color = new Color(0.720f, 0.115f, 0.155f, 1f);
            }

            if (Luminance(palette.UiPanel) < 0.35f)
            {
                turnPlayer1Color = EnsureReadableOnDark(turnPlayer1Color);
                turnPlayer2Color = EnsureReadableOnDark(turnPlayer2Color);
            }

            SetImageColor("StatusPanel", WithAlpha(palette.UiPanel, Mathf.Max(0.52f, palette.UiPanel.a * 0.86f)));
            SetImageColor("DirectionsPanel", WithAlpha(palette.UiPanel, 0.34f));

            if (turnLabel != null)
            {
                turnLabel.color = turnPlayer1Color;
            }

            if (infoLabel != null)
            {
                infoLabel.color = palette.UiMuted;
            }

            ApplyButtonTheme(restartButton, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButtonTheme(menuButton, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButtonTheme(upButton, palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButtonTheme(downButton, palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButtonTheme(leftButton, palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButtonTheme(rightButton, palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButtonTheme(dotSelfButton, palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButtonTheme(dotTeammateButton, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButtonTheme(gameOverMenuButton, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButtonTheme(gameOverReplayButton, palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyAuxiliaryPanelTheme(palette);
            RefreshDotChoiceButtons();
            if (gameOverTitleLabel != null)
            {
                gameOverTitleLabel.color = palette.UiText;
            }

            if (gameOverSubtitleLabel != null)
            {
                gameOverSubtitleLabel.color = palette.UiMuted;
            }

            if (gameOverHeadingLabel != null)
            {
                gameOverHeadingLabel.color = palette.UiMuted;
            }

            SetDirectionControlsVisible(gameKind != GameKind.Qomet);
        }

        private static Color EnsureReadableOnDark(Color color)
        {
            return Luminance(color) >= 0.48f ? color : Color.Lerp(color, Color.white, 0.64f);
        }

        private static float Luminance(Color color)
        {
            return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        }

        private void ApplyButtonTheme(Button button, Color normalColor, Color textColor, Color disabledColor)
        {
            if (button == null)
            {
                return;
            }

            var colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.24f);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.20f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = disabledColor;
            colors.fadeDuration = 0.10f;
            button.colors = colors;

            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = button.interactable ? normalColor : disabledColor;
            }

            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                Color background = button.interactable ? normalColor : disabledColor;
                label.color = VisualThemeCatalog.GetButtonTextColor(background, VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme));
            }

            if (_directionBaseColors.ContainsKey(button))
            {
                _directionBaseColors[button] = colors;
            }
        }

        private static void SetImageColor(string objectName, Color color)
        {
            var image = GameObject.Find(objectName)?.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        private void SetDirectionControlsVisible(bool visible)
        {
            Transform panel = upButton != null ? upButton.transform.parent : null;
            if (panel != null && panel.name == "DirectionsPanel")
            {
                panel.gameObject.SetActive(visible);
                return;
            }

            SetButtonObjectVisible(upButton, visible);
            SetButtonObjectVisible(downButton, visible);
            SetButtonObjectVisible(leftButton, visible);
            SetButtonObjectVisible(rightButton, visible);
        }

        private static void SetButtonObjectVisible(Button button, bool visible)
        {
            if (button != null)
            {
                button.gameObject.SetActive(visible);
            }
        }

        private void EnsureAuxiliaryPanels()
        {
            EnsureQuixoHelpPanel();
            BindDotChoiceButtons();
        }

        private void EnsureQuixoHelpPanel()
        {
            if (quixoHelpPanel != null)
            {
                return;
            }

            var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);
            quixoHelpPanel = new GameObject("QuixoHelpPanel", typeof(RectTransform), typeof(Image));
            quixoHelpPanel.transform.SetParent(transform, false);

            var rect = quixoHelpPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(286f, 356f);

            var image = quixoHelpPanel.GetComponent<Image>();
            image.color = WithAlpha(palette.UiPanel, 0.72f);
            image.raycastTarget = false;

            var layout = quixoHelpPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 14, 14);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreatePanelText(quixoHelpPanel.transform, "QuixoHelpTitle", "COMMENT JOUER", 17f, FontStyles.Bold, palette.UiText, 28f);
            CreatePanelText(
                quixoHelpPanel.transform,
                "QuixoHelpText",
                "1. Choisissez un cube en bordure.\n2. Il doit etre neutre ou a votre marque.\n3. Jouez avec les boutons, les fleches ou un glissement.\n4. Le cube pousse prend votre marque.\n5. Alignez 5 cubes pour gagner.",
                13.5f,
                FontStyles.Normal,
                palette.UiMuted,
                112f);
            CreatePanelText(
                quixoHelpPanel.transform,
                "Quixo2v2RulesText",
                "En 2v2 : le point indique quel equipier peut reprendre le cube.",
                13f,
                FontStyles.Bold,
                palette.UiText,
                42f);

            CreateTeam2v2Panel(quixoHelpPanel.transform, palette);
            SetObjectVisible(quixoHelpPanel, gameKind == GameKind.Quixo);
        }

        private void CreateTeam2v2Panel(Transform parent, GameplayPalette palette)
        {
            team2v2Panel = new GameObject("Team2v2HelpPanel", typeof(RectTransform), typeof(Image));
            team2v2Panel.transform.SetParent(parent, false);

            var image = team2v2Panel.GetComponent<Image>();
            image.color = WithAlpha(palette.UiPanel, 0.38f);
            image.raycastTarget = false;

            var layout = team2v2Panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var element = team2v2Panel.AddComponent<LayoutElement>();
            element.preferredHeight = 132f;
            element.minHeight = 112f;

            team2v2InfoLabel = CreatePanelText(team2v2Panel.transform, "Team2v2InfoLabel", "", 12.5f, FontStyles.Normal, palette.UiMuted, 48f);

            var row = new GameObject("DotChoiceRow", typeof(RectTransform));
            row.transform.SetParent(team2v2Panel.transform, false);
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            var rowElement = row.AddComponent<LayoutElement>();
            rowElement.preferredHeight = 38f;

            dotSelfButton = CreatePanelButton(row.transform, "DotSelfButton", "Point vers moi", palette.UiButton);
            dotTeammateButton = CreatePanelButton(row.transform, "DotTeammateButton", "Point vers coequipier", palette.UiButtonSecondary);
            SetObjectVisible(team2v2Panel, false);
        }

        private TextMeshProUGUI CreatePanelText(Transform parent, string name, string text, float fontSize, FontStyles style, Color color, float height)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var label = obj.AddComponent<TextMeshProUGUI>();
            label.name = name;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;

            var element = obj.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = Mathf.Min(height, 28f);
            return label;
        }

        private Button CreatePanelButton(Transform parent, string name, string labelText, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.color = color;

            var button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            var element = obj.AddComponent<LayoutElement>();
            element.preferredHeight = 38f;
            element.minHeight = 36f;
            element.flexibleWidth = 1f;

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(obj.transform, false);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = labelText;
            text.fontSize = 11.5f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.color = VisualThemeCatalog.GetReadableTextColor(color);
            text.raycastTarget = false;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(4f, 0f);
            text.rectTransform.offsetMax = new Vector2(-4f, 0f);
            return button;
        }

        private void BindDotChoiceButtons()
        {
            if (dotSelfButton != null)
            {
                dotSelfButton.onClick.RemoveAllListeners();
                dotSelfButton.onClick.AddListener(() =>
                {
                    _dotTowardTeammate = false;
                    RefreshDotChoiceButtons();
                });
            }

            if (dotTeammateButton != null)
            {
                dotTeammateButton.onClick.RemoveAllListeners();
                dotTeammateButton.onClick.AddListener(() =>
                {
                    _dotTowardTeammate = true;
                    RefreshDotChoiceButtons();
                });
            }
        }

        private void RefreshDotChoiceButtons()
        {
            var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);
            ApplyButtonTheme(dotSelfButton, _dotTowardTeammate ? palette.UiButtonSecondary : palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButtonTheme(dotTeammateButton, _dotTowardTeammate ? palette.UiButton : palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
        }

        private void ApplyAuxiliaryPanelTheme(GameplayPalette palette)
        {
            SetImageColor(quixoHelpPanel, WithAlpha(palette.UiPanel, 0.72f));
            SetImageColor(team2v2Panel, WithAlpha(palette.UiPanel, 0.38f));
            SetChildTextColor("QuixoHelpTitle", palette.UiText);
            SetChildTextColor("QuixoHelpText", palette.UiMuted);
            SetChildTextColor("Quixo2v2RulesText", palette.UiText);
            if (team2v2InfoLabel != null)
            {
                team2v2InfoLabel.color = palette.UiMuted;
            }
        }

        private void SetChildTextColor(string objectName, Color color)
        {
            var label = FindChildComponent<TextMeshProUGUI>(objectName);
            if (label != null)
            {
                label.color = color;
            }
        }

        private static void SetObjectVisible(GameObject target, bool visible)
        {
            if (target != null)
            {
                target.SetActive(visible);
            }
        }

        private static void SetImageColor(GameObject target, Color color)
        {
            var image = target != null ? target.GetComponent<Image>() : null;
            if (image != null)
            {
                image.color = color;
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
