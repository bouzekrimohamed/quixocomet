using QuixoUnity.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QuixoUnity.UI
{
    public sealed class AuthView : MonoBehaviour
    {
        private const string MenuSceneName = "MenuScene";

        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button registerButton;
        [SerializeField] private Button resetPasswordButton;
        [SerializeField] private Button offlineButton;
        [SerializeField] private TextMeshProUGUI messageLabel;
        [SerializeField] private AuthService authService;

        private bool _busy;

        private void Awake()
        {
            ResolveReferences();
            BindButtons();
            ApplyTheme();

            if (!SupabaseSettings.IsConfigured)
            {
                SetMessage("Supabase non configure. Vous pouvez continuer hors ligne.", false);
            }
        }

        public void Login()
        {
            if (_busy)
            {
                return;
            }

            SetBusy(true);
            SetMessage("Connexion...", true);
            authService.Login(Read(emailInput), Read(passwordInput), OnAuthComplete);
        }

        public void ResetPassword()
        {
            if (_busy)
            {
                return;
            }

            SetBusy(true);
            SetMessage("Envoi de l'email...", true);
            authService.SendPasswordReset(Read(emailInput), result =>
            {
                SetBusy(false);
                if (result == null)
                {
                    SetMessage("Operation impossible.", false);
                    return;
                }

                SetMessage(result.Message, result.Success);
            });
        }

        public void Register()
        {
            if (_busy)
            {
                return;
            }

            SetBusy(true);
            SetMessage("Inscription...", true);
            authService.Register(Read(emailInput), Read(passwordInput), Read(usernameInput), OnAuthComplete);
        }

        public void ContinueOffline()
        {
            if (_busy)
            {
                return;
            }

            SessionManager.StartOffline();
            LoadMenu();
        }

        private void OnAuthComplete(AuthOperationResult result)
        {
            SetBusy(false);
            if (result == null)
            {
                SetMessage("Operation impossible.", false);
                return;
            }

            SetMessage(result.Message, result.Success);
            if (result.Success && SessionManager.HasSession)
            {
                LoadMenu();
            }
        }

        private void LoadMenu()
        {
            if (Application.CanStreamedLevelBeLoaded(MenuSceneName))
            {
                SceneManager.LoadScene(MenuSceneName);
                return;
            }

            SetMessage("MenuScene introuvable. Regenerer les scenes.", false);
        }

        private void ResolveReferences()
        {
            authService ??= FindObjectOfType<AuthService>();
            if (authService == null)
            {
                authService = gameObject.AddComponent<AuthService>();
            }

            emailInput ??= FindChild<TMP_InputField>("EmailInput");
            passwordInput ??= FindChild<TMP_InputField>("PasswordInput");
            usernameInput ??= FindChild<TMP_InputField>("UsernameInput");
            loginButton ??= FindChild<Button>("LoginButton");
            registerButton ??= FindChild<Button>("RegisterButton");
            resetPasswordButton ??= FindChild<Button>("ResetPasswordButton");
            offlineButton ??= FindChild<Button>("OfflineButton");
            messageLabel ??= FindChild<TextMeshProUGUI>("MessageLabel");

            if (passwordInput != null)
            {
                passwordInput.contentType = TMP_InputField.ContentType.Password;
                passwordInput.ForceLabelUpdate();
            }
        }

        private void BindButtons()
        {
            Bind(loginButton, Login);
            Bind(registerButton, Register);
            Bind(resetPasswordButton, ResetPassword);
            Bind(offlineButton, ContinueOffline);
        }

        private void ApplyTheme()
        {
            var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);
            ApplyButton(loginButton, palette.UiButton, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(registerButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(resetPasswordButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(offlineButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            if (messageLabel != null)
            {
                messageLabel.color = palette.UiMuted;
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            SetInteractable(loginButton, !busy);
            SetInteractable(registerButton, !busy);
            SetInteractable(resetPasswordButton, !busy);
            SetInteractable(offlineButton, !busy);
        }

        private void SetMessage(string message, bool success)
        {
            if (messageLabel == null)
            {
                return;
            }

            var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);
            messageLabel.text = message;
            messageLabel.color = success ? palette.UiText : new Color(1f, 0.58f, 0.42f, 1f);
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

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private static string Read(TMP_InputField input)
        {
            return input != null ? input.text.Trim() : string.Empty;
        }

        private static void ApplyButton(Button button, Color normalColor, Color textColor, Color disabledColor)
        {
            if (button == null)
            {
                return;
            }

            var colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.18f);
            colors.disabledColor = disabledColor;
            button.colors = colors;

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
