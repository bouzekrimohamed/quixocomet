using QuixoUnity.Auth;
using QuixoUnity.Core;
using QuixoUnity.Online;
using QuixoUnity.Social;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QuixoUnity.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        private const string GameplaySceneName = "GameplayScene";
        private const string AuthSceneName = "AuthScene";

        [SerializeField] private TextMeshProUGUI connectedLabel;
        [SerializeField] private TextMeshProUGUI statusLabel;
        [SerializeField] private Button quixoButton;
        [SerializeField] private Button qometButton;
        [SerializeField] private Button playButton;
        [SerializeField] private Button localButton;
        [SerializeField] private Button onlineButton;
        [SerializeField] private Button modeBackButton;
        [SerializeField] private Button gameBackButton;
        [SerializeField] private Button quixoOnlineButton;
        [SerializeField] private Button qometOnlineButton;
        [SerializeField] private Button cancelOnlineButton;
        [SerializeField] private Button friendsButton;
        [SerializeField] private Button themeButton;
        [SerializeField] private Button logoutButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject friendsPanel;
        [SerializeField] private GameObject mainActionsPanel;
        [SerializeField] private GameObject modePanel;
        [SerializeField] private GameObject gamePanel;
        [SerializeField] private FriendsView friendsView;
        [SerializeField] private AuthService authService;
        [SerializeField] private FriendService friendService;
        [SerializeField] private OnlineMatchService onlineMatchService;
        [SerializeField] private OnlinePresenceService onlinePresenceService;

        private TextMeshProUGUI _themeButtonLabel;
        private bool _loadingGameplay;
        private bool _searchingOnline;
        private GameKind _searchingKind;

        private void Awake()
        {
            // Entrer dans le menu ne doit jamais reprendre une partie transitoire.
            OnlineSessionTransit.Clear();
            SceneTransit.SelectedTheme = VisualThemeCatalog.ActiveTheme;
            ResolveReferences();
            BindButtons();
            ApplyTheme();
            RefreshSessionDisplay();
            RefreshOnlineAvailability();
            if (friendsPanel != null)
            {
                friendsPanel.SetActive(false);
            }
            ShowMainActions();
        }

        private void Start()
        {
            RefreshOnlineAvailability();
            if (SessionManager.IsOnline && authService != null)
            {
                onlinePresenceService?.StartPresence();
                authService.FetchCurrentProfile(result =>
                {
                    if (result != null && result.Success)
                    {
                        RefreshSessionDisplay();
                    }
                });
            }
            else if (SessionManager.IsOffline)
            {
                SetStatus("Mode invite : online indisponible. Connectez-vous pour jouer en ligne.");
            }
        }

        private void OnDestroy()
        {
            onlinePresenceService?.StopPresence();
        }

        public void StartQuixo()
        {
            StartGame(GameKind.Quixo);
        }

        public void StartQomet()
        {
            StartGame(GameKind.Qomet);
        }

        public void StartOnlineQuixo()
        {
            if (!RequireOnlineAccount())
            {
                return;
            }

            StartOnlineGame(GameKind.Quixo);
        }

        public void StartOnlineQomet()
        {
            if (!RequireOnlineAccount())
            {
                return;
            }

            StartOnlineGame(GameKind.Qomet);
        }

        public void ShowModeChoice()
        {
            SetPanelState(false, true, false);
        }

        public void ShowLocalChoice()
        {
            SetPanelState(false, false, true);
            SetActive(quixoButton, true);
            SetActive(qometButton, true);
            SetActive(quixoOnlineButton, false);
            SetActive(qometOnlineButton, false);
            SetInteractable(quixoButton, true);
            SetInteractable(qometButton, true);
        }

        public void ShowOnlineChoice()
        {
            if (!RequireOnlineAccount())
            {
                return;
            }

            SetPanelState(false, false, true);
            SetActive(quixoButton, false);
            SetActive(qometButton, false);
            SetActive(quixoOnlineButton, true);
            SetActive(qometOnlineButton, true);
            SetInteractable(quixoOnlineButton, true);
            SetInteractable(qometOnlineButton, true);
        }

        public void ShowMainActions()
        {
            SetPanelState(true, false, false);
        }

        private bool RequireOnlineAccount()
        {
            if (SessionManager.IsOnline)
            {
                return true;
            }

            SetStatus("Connectez-vous pour jouer en ligne.");
            RefreshOnlineAvailability();
            return false;
        }

        private void RefreshOnlineAvailability()
        {
            bool online = SessionManager.IsOnline;
            SetInteractable(quixoOnlineButton, online && !_searchingOnline);
            SetInteractable(qometOnlineButton, online && !_searchingOnline);
            SetInteractable(friendsButton, online && !_searchingOnline);
            if (!online && SessionManager.IsOffline && statusLabel != null && string.IsNullOrEmpty(statusLabel.text))
            {
                SetStatus("Connectez-vous pour jouer en ligne.");
            }
        }

        public void CancelOnlineSearch()
        {
            if (!_searchingOnline || onlineMatchService == null)
            {
                SetOnlineSearching(false);
                return;
            }

            onlineMatchService.CancelMatchmaking(_searchingKind, result =>
            {
                SetOnlineSearching(false);
                SetStatus(result != null ? result.Message : "Recherche annulee.");
            });
        }

        public void ToggleFriends()
        {
            if (friendsPanel == null)
            {
                SetStatus("Panneau amis introuvable.");
                return;
            }

            if (!SessionManager.IsOnline)
            {
                SetStatus("Connectez-vous pour utiliser les amis et le online.");
                return;
            }

            bool next = !friendsPanel.activeSelf;
            friendsPanel.SetActive(next);
            if (next && friendsView != null)
            {
                friendsView.Refresh();
            }
        }

        public void CycleTheme()
        {
            VisualThemeCatalog.ActiveTheme = VisualThemeCatalog.Next(VisualThemeCatalog.ActiveTheme);
            SceneTransit.SelectedTheme = VisualThemeCatalog.ActiveTheme;
            ApplyTheme();
            RefreshThemeLabel();
            if (friendsView != null)
            {
                friendsView.ApplyTheme();
            }
        }

        public void Logout()
        {
            authService?.Logout();
            onlinePresenceService?.StopPresence();
            OnlineSessionTransit.Clear();
            SessionManager.ClearSession();
            if (Application.CanStreamedLevelBeLoaded(AuthSceneName))
            {
                SceneManager.LoadScene(AuthSceneName);
                return;
            }

            SetStatus("AuthScene introuvable. Regenerer les scenes.");
        }

        public void Quit()
        {
            Application.Quit();
        }

        private void StartGame(GameKind kind)
        {
            if (_loadingGameplay)
            {
                return;
            }

            OnlineSessionTransit.Clear();
            SceneTransit.SelectedGame = kind;
            SceneTransit.SelectedTheme = VisualThemeCatalog.ActiveTheme;

            if (Application.CanStreamedLevelBeLoaded(GameplaySceneName))
            {
                _loadingGameplay = true;
                SceneManager.LoadScene(GameplaySceneName);
                return;
            }

            SetStatus("GameplayScene introuvable. Regenerer les scenes.");
        }

        private void StartOnlineGame(GameKind kind)
        {
            if (_loadingGameplay || _searchingOnline)
            {
                return;
            }

            if (!SessionManager.IsOnline)
            {
                SetStatus("Connectez-vous pour jouer en ligne.");
                return;
            }

            onlineMatchService ??= FindObjectOfType<OnlineMatchService>();
            if (onlineMatchService == null)
            {
                onlineMatchService = gameObject.AddComponent<OnlineMatchService>();
            }

            _searchingKind = kind;
            SetOnlineSearching(true);
            SetStatus($"Recherche d'un joueur {OnlineSessionTransit.GameKindName(kind)}...");
            onlineMatchService.StartMatchmaking(kind, result =>
            {
                if (result == null || !result.Success || result.Match == null)
                {
                    SetOnlineSearching(false);
                    SetStatus(result != null ? result.Message : "Matchmaking impossible.");
                    return;
                }

                if (!OnlineSessionTransit.IsValidForLocalPlayer(result.Match, SessionManager.UserId))
                {
                    SetOnlineSearching(false);
                    SetStatus("Match invalide ou inaccessible.");
                    return;
                }

                OnlineSessionTransit.Start(result.Match, SessionManager.UserId);
                SceneTransit.SelectedGame = OnlineSessionTransit.SelectedGameKind;
                SceneTransit.SelectedTheme = VisualThemeCatalog.ActiveTheme;
                LoadGameplay();
            }, SetStatus);
        }

        private void LoadGameplay()
        {
            if (Application.CanStreamedLevelBeLoaded(GameplaySceneName))
            {
                _loadingGameplay = true;
                SceneManager.LoadScene(GameplaySceneName);
                return;
            }

            SetOnlineSearching(false);
            SetStatus("GameplayScene introuvable. Regenerer les scenes.");
        }

        private void ResolveReferences()
        {
            authService ??= FindObjectOfType<AuthService>();
            if (authService == null)
            {
                authService = gameObject.AddComponent<AuthService>();
            }

            friendService ??= FindObjectOfType<FriendService>();
            if (friendService == null)
            {
                friendService = gameObject.AddComponent<FriendService>();
            }

            onlineMatchService ??= FindObjectOfType<OnlineMatchService>();
            if (onlineMatchService == null)
            {
                onlineMatchService = gameObject.AddComponent<OnlineMatchService>();
            }

            onlinePresenceService ??= FindObjectOfType<OnlinePresenceService>();
            if (onlinePresenceService == null)
            {
                onlinePresenceService = gameObject.AddComponent<OnlinePresenceService>();
            }

            connectedLabel ??= FindChild<TextMeshProUGUI>("ConnectedLabel");
            statusLabel ??= FindChild<TextMeshProUGUI>("MenuStatusLabel");
            quixoButton ??= FindChild<Button>("QuixoButton");
            qometButton ??= FindChild<Button>("QometButton");
            playButton ??= FindChild<Button>("PlayButton");
            localButton ??= FindChild<Button>("LocalButton");
            onlineButton ??= FindChild<Button>("OnlineButton");
            modeBackButton ??= FindChild<Button>("ModeBackButton");
            gameBackButton ??= FindChild<Button>("GameBackButton");
            quixoOnlineButton ??= FindChild<Button>("QuixoOnlineButton");
            qometOnlineButton ??= FindChild<Button>("QometOnlineButton");
            cancelOnlineButton ??= FindChild<Button>("CancelOnlineButton");
            friendsButton ??= FindChild<Button>("FriendsButton");
            themeButton ??= FindChild<Button>("ThemeButton");
            logoutButton ??= FindChild<Button>("LogoutButton");
            quitButton ??= FindChild<Button>("QuitButton");
            friendsView ??= FindObjectOfType<FriendsView>(true);
            mainActionsPanel ??= FindChild<Transform>("MainActionsPanel")?.gameObject;
            modePanel ??= FindChild<Transform>("ModePanel")?.gameObject;
            gamePanel ??= FindChild<Transform>("GamePanel")?.gameObject;

            if (friendsPanel == null && friendsView != null)
            {
                friendsPanel = friendsView.gameObject;
            }

            if (themeButton != null)
            {
                _themeButtonLabel = themeButton.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        private void BindButtons()
        {
            Bind(quixoButton, StartQuixo);
            Bind(qometButton, StartQomet);
            Bind(playButton, ShowModeChoice);
            Bind(localButton, ShowLocalChoice);
            Bind(onlineButton, ShowOnlineChoice);
            Bind(modeBackButton, ShowMainActions);
            Bind(gameBackButton, ShowModeChoice);
            Bind(quixoOnlineButton, StartOnlineQuixo);
            Bind(qometOnlineButton, StartOnlineQomet);
            Bind(cancelOnlineButton, CancelOnlineSearch);
            Bind(friendsButton, ToggleFriends);
            Bind(themeButton, CycleTheme);
            Bind(logoutButton, Logout);
            Bind(quitButton, Quit);
        }

        private void RefreshSessionDisplay()
        {
            if (connectedLabel == null)
            {
                return;
            }

            string username = string.IsNullOrWhiteSpace(SessionManager.Username) ? "Invite" : SessionManager.Username;
            string suffix = SessionManager.IsOffline ? " (hors ligne)" : string.Empty;
            connectedLabel.text = $"Connecte : {username}{suffix}";
        }

        private void ApplyTheme()
        {
            var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);
            SetImageColor("Background", palette.MenuBackground);
            SetImageColor("MenuPanel", palette.MenuPanel);
            SetImageColor("FriendsPanel", palette.MenuPanel);
            SetTextColor("Title", palette.UiText);
            SetTextColor("Subtitle", palette.UiMuted);
            if (connectedLabel != null)
            {
                connectedLabel.color = palette.UiText;
            }

            if (statusLabel != null)
            {
                statusLabel.color = palette.UiMuted;
            }

            ApplyButton(quixoButton, palette.UiButton, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(qometButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(playButton, palette.UiButton, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(localButton, palette.UiButton, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(onlineButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(modeBackButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(gameBackButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(quixoOnlineButton, palette.UiButton, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(qometOnlineButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(cancelOnlineButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(friendsButton, palette.UiButton, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(themeButton, palette.UiButton, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(logoutButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(quitButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplySceneTextPalette(palette);
            RefreshThemeLabel();
            SetOnlineSearching(_searchingOnline);
        }

        private static void ApplySceneTextPalette(GameplayPalette palette)
        {
            foreach (var label in FindObjectsOfType<TextMeshProUGUI>(true))
            {
                bool muted = label.name.Contains("Subtitle") || label.name.Contains("Status") || label.name.Contains("Placeholder");
                label.color = muted ? palette.UiMuted : palette.UiText;
            }
        }

        private void RefreshThemeLabel()
        {
            if (_themeButtonLabel == null && themeButton != null)
            {
                _themeButtonLabel = themeButton.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (_themeButtonLabel != null)
            {
                _themeButtonLabel.text = $"Theme : {VisualThemeCatalog.DisplayName(VisualThemeCatalog.ActiveTheme)}";
            }
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }

        private void SetOnlineSearching(bool searching)
        {
            _searchingOnline = searching;
            bool online = SessionManager.IsOnline;
            SetInteractable(quixoButton, !searching);
            SetInteractable(qometButton, !searching);
            SetInteractable(quixoOnlineButton, !searching && online);
            SetInteractable(qometOnlineButton, !searching && online);
            SetInteractable(friendsButton, !searching && online);
            if (friendsButton != null)
            {
                friendsButton.gameObject.SetActive(!searching);
            }

            SetInteractable(themeButton, !searching);
            SetInteractable(logoutButton, !searching);
            if (cancelOnlineButton != null)
            {
                cancelOnlineButton.gameObject.SetActive(searching);
                cancelOnlineButton.interactable = searching;
            }
        }

        private void SetPanelState(bool main, bool mode, bool game)
        {
            if (mainActionsPanel != null) mainActionsPanel.SetActive(main);
            if (modePanel != null) modePanel.SetActive(mode);
            if (gamePanel != null) gamePanel.SetActive(game);
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private static void SetImageColor(string objectName, Color color)
        {
            var image = GameObject.Find(objectName)?.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
        }

        private static void SetTextColor(string objectName, Color color)
        {
            var label = GameObject.Find(objectName)?.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                label.color = color;
            }
        }

        private static void ApplyButton(Button button, Color normalColor, Color textColor, Color disabledColor)
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
                label.color = textColor;
            }
        }

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private static void SetActive(Button button, bool active)
        {
            if (button != null)
            {
                button.gameObject.SetActive(active);
            }
        }

        private T FindChild<T>(string childName) where T : Component
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
