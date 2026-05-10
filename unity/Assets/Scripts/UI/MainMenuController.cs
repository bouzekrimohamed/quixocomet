using QuixoUnity.Auth;
using QuixoUnity.Core;
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
        [SerializeField] private Button friendsButton;
        [SerializeField] private Button themeButton;
        [SerializeField] private Button logoutButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject friendsPanel;
        [SerializeField] private FriendsView friendsView;
        [SerializeField] private AuthService authService;
        [SerializeField] private FriendService friendService;

        private TextMeshProUGUI _themeButtonLabel;
        private bool _loadingGameplay;

        private void Awake()
        {
            SceneTransit.SelectedTheme = VisualThemeCatalog.ActiveTheme;
            ResolveReferences();
            BindButtons();
            ApplyTheme();
            RefreshSessionDisplay();
            if (friendsPanel != null)
            {
                friendsPanel.SetActive(false);
            }
        }

        private void Start()
        {
            if (SessionManager.IsOnline && authService != null)
            {
                authService.FetchCurrentProfile(result =>
                {
                    if (result != null && result.Success)
                    {
                        RefreshSessionDisplay();
                    }
                });
            }
        }

        public void StartQuixo()
        {
            StartGame(GameKind.Quixo);
        }

        public void StartQomet()
        {
            StartGame(GameKind.Qomet);
        }

        public void ToggleFriends()
        {
            if (friendsPanel == null)
            {
                SetStatus("Panneau amis introuvable.");
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

            connectedLabel ??= FindChild<TextMeshProUGUI>("ConnectedLabel");
            statusLabel ??= FindChild<TextMeshProUGUI>("MenuStatusLabel");
            quixoButton ??= FindChild<Button>("QuixoButton");
            qometButton ??= FindChild<Button>("QometButton");
            friendsButton ??= FindChild<Button>("FriendsButton");
            themeButton ??= FindChild<Button>("ThemeButton");
            logoutButton ??= FindChild<Button>("LogoutButton");
            quitButton ??= FindChild<Button>("QuitButton");
            friendsView ??= FindObjectOfType<FriendsView>(true);

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
            ApplyButton(friendsButton, palette.UiButton, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(themeButton, palette.UiButton, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(logoutButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(quitButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            RefreshThemeLabel();
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
