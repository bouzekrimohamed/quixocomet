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
            SetImageColor("Background", palette.MenuBackground);
            SetImageColor("AuthPanel", palette.MenuPanel);
            SetTextColor("Title", palette.UiText);
            SetTextColor("Subtitle", palette.UiMuted);
            ApplyInput(emailInput, palette);
            ApplyInput(passwordInput, palette);
            ApplyInput(usernameInput, palette);
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

        private static void ApplyInput(TMP_InputField input, GameplayPalette palette)
        {
            if (input == null)
            {
                return;
            }

            Color inputColor = Color.Lerp(palette.UiPanel, palette.CubeTop, 0.18f);
            if (input.targetGraphic != null)
            {
                input.targetGraphic.color = inputColor;
            }

            var colors = input.colors;
            colors.normalColor = inputColor;
            colors.highlightedColor = Color.Lerp(inputColor, Color.white, 0.12f);
            colors.selectedColor = Color.Lerp(inputColor, palette.Selection, 0.18f);
            colors.disabledColor = new Color(inputColor.r, inputColor.g, inputColor.b, 0.48f);
            colors.fadeDuration = 0.10f;
            input.colors = colors;

            if (input.textComponent != null)
            {
                input.textComponent.color = palette.UiText;
            }

            if (input.placeholder is TextMeshProUGUI placeholder)
            {
                placeholder.color = palette.UiMuted;
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

        private static void SetTextColor(string objectName, Color color)
        {
            var label = GameObject.Find(objectName)?.GetComponent<TextMeshProUGUI>();
            if (label != null)
            {
                label.color = color;
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
