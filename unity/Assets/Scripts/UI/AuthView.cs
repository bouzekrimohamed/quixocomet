using System.Collections;
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

        private enum AuthMode
        {
            SignIn,
            SignUp,
            Guest
        }

        [SerializeField] private TMP_InputField emailInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField signInCredentialInput;
        [SerializeField] private TMP_InputField signInPasswordInput;
        [SerializeField] private TMP_InputField signUpEmailInput;
        [SerializeField] private TMP_InputField signUpUsernameInput;
        [SerializeField] private TMP_InputField signUpPasswordInput;
        [SerializeField] private Button loginButton;
        [SerializeField] private Button registerButton;
        [SerializeField] private Button resetPasswordButton;
        [SerializeField] private Button offlineButton;
        [SerializeField] private Button showSignInButton;
        [SerializeField] private Button showSignUpButton;
        [SerializeField] private Button showGuestButton;
        [SerializeField] private Button createAccountButton;
        [SerializeField] private Button alreadyAccountButton;
        [SerializeField] private Button signInPasswordToggleButton;
        [SerializeField] private Button signUpPasswordToggleButton;
        [SerializeField] private CanvasGroup signInPanel;
        [SerializeField] private CanvasGroup signUpPanel;
        [SerializeField] private CanvasGroup guestPanel;
        [SerializeField] private float modeTransitionDuration = 0.16f;
        [SerializeField] private TextMeshProUGUI messageLabel;
        [SerializeField] private AuthService authService;

        private AuthMode _mode = AuthMode.SignIn;
        private bool _busy;
        private Coroutine _modeRoutine;
        private bool _signInPasswordVisible;
        private bool _signUpPasswordVisible;

        private void Awake()
        {
            ResolveReferences();
            BindButtons();
            ApplyTheme();
            SetMode(AuthMode.SignIn, false);

            if (!SupabaseSettings.IsConfigured)
            {
                SetMessage("Service en ligne indisponible. Vous pouvez continuer hors ligne.", false);
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
            authService.Login(Read(SignInCredential), Read(SignInPassword), OnAuthComplete);
        }

        public void ResetPassword()
        {
            if (_busy)
            {
                return;
            }

            SetBusy(true);
            SetMessage("Envoi de l'email...", true);
            authService.SendPasswordReset(Read(SignInCredential), result =>
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
            authService.Register(Read(SignUpEmail), Read(SignUpPassword), Read(SignUpUsername), OnAuthComplete);
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

        public void ShowSignIn()
        {
            SetMode(AuthMode.SignIn, true);
        }

        public void ShowSignUp()
        {
            SetMode(AuthMode.SignUp, true);
        }

        public void ShowGuest()
        {
            SetMode(AuthMode.Guest, true);
        }

        public void ToggleSignInPassword()
        {
            _signInPasswordVisible = !_signInPasswordVisible;
            SetPasswordVisibility(SignInPassword, signInPasswordToggleButton, _signInPasswordVisible);
        }

        public void ToggleSignUpPassword()
        {
            _signUpPasswordVisible = !_signUpPasswordVisible;
            SetPasswordVisibility(SignUpPassword, signUpPasswordToggleButton, _signUpPasswordVisible);
        }

        private TMP_InputField SignInCredential => signInCredentialInput != null ? signInCredentialInput : emailInput;
        private TMP_InputField SignInPassword => signInPasswordInput != null ? signInPasswordInput : passwordInput;
        private TMP_InputField SignUpEmail => signUpEmailInput != null ? signUpEmailInput : emailInput;
        private TMP_InputField SignUpUsername => signUpUsernameInput != null ? signUpUsernameInput : usernameInput;
        private TMP_InputField SignUpPassword => signUpPasswordInput != null ? signUpPasswordInput : passwordInput;

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
            signInCredentialInput ??= FindChild<TMP_InputField>("SignInCredentialInput");
            signInPasswordInput ??= FindChild<TMP_InputField>("SignInPasswordInput");
            signUpEmailInput ??= FindChild<TMP_InputField>("SignUpEmailInput");
            signUpUsernameInput ??= FindChild<TMP_InputField>("SignUpUsernameInput");
            signUpPasswordInput ??= FindChild<TMP_InputField>("SignUpPasswordInput");
            signInPanel ??= FindChild<CanvasGroup>("SignInPanel");
            signUpPanel ??= FindChild<CanvasGroup>("SignUpPanel");
            guestPanel ??= FindChild<CanvasGroup>("GuestPanel");
            loginButton ??= FindChild<Button>("LoginButton");
            registerButton ??= FindChild<Button>("RegisterButton");
            resetPasswordButton ??= FindChild<Button>("ResetPasswordButton");
            offlineButton ??= FindChild<Button>("OfflineButton");
            showSignInButton ??= FindChild<Button>("ShowSignInButton");
            showSignUpButton ??= FindChild<Button>("ShowSignUpButton");
            showGuestButton ??= FindChild<Button>("ShowGuestButton");
            createAccountButton ??= FindChild<Button>("CreateAccountButton");
            alreadyAccountButton ??= FindChild<Button>("AlreadyAccountButton");
            signInPasswordToggleButton ??= FindChild<Button>("SignInPasswordToggleButton");
            signUpPasswordToggleButton ??= FindChild<Button>("SignUpPasswordToggleButton");
            messageLabel ??= FindChild<TextMeshProUGUI>("MessageLabel");

            signInCredentialInput ??= emailInput;
            signInPasswordInput ??= passwordInput;
            signUpEmailInput ??= emailInput;
            signUpUsernameInput ??= usernameInput;
            signUpPasswordInput ??= passwordInput;

            PreparePasswordField(passwordInput);
            PreparePasswordField(signInPasswordInput);
            PreparePasswordField(signUpPasswordInput);
            SetPasswordVisibility(SignInPassword, signInPasswordToggleButton, false);
            SetPasswordVisibility(SignUpPassword, signUpPasswordToggleButton, false);
        }

        private void BindButtons()
        {
            Bind(loginButton, Login);
            Bind(registerButton, Register);
            Bind(resetPasswordButton, ResetPassword);
            Bind(offlineButton, ContinueOffline);
            Bind(showSignInButton, ShowSignIn);
            Bind(showSignUpButton, ShowSignUp);
            Bind(showGuestButton, ShowGuest);
            Bind(createAccountButton, ShowSignUp);
            Bind(alreadyAccountButton, ShowSignIn);
            Bind(signInPasswordToggleButton, ToggleSignInPassword);
            Bind(signUpPasswordToggleButton, ToggleSignUpPassword);
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
            ApplyInput(signInCredentialInput, palette);
            ApplyInput(signInPasswordInput, palette);
            ApplyInput(signUpEmailInput, palette);
            ApplyInput(signUpUsernameInput, palette);
            ApplyInput(signUpPasswordInput, palette);
            ApplyButton(loginButton, palette.UiButton, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(registerButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(resetPasswordButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(offlineButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(showSignInButton, palette.UiButton, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(showSignUpButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(showGuestButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(createAccountButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(alreadyAccountButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(signInPasswordToggleButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButton(signUpPasswordToggleButton, palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            if (messageLabel != null)
            {
                messageLabel.color = palette.UiMuted;
            }

            ApplySceneTextPalette(palette);
            UpdateModeButtonState();
        }

        private static void ApplySceneTextPalette(GameplayPalette palette)
        {
            foreach (var label in FindObjectsOfType<TextMeshProUGUI>(true))
            {
                bool muted = label.name.Contains("Subtitle") || label.name.Contains("Placeholder") || label.name.Contains("Message");
                label.color = muted ? palette.UiMuted : palette.UiText;
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            SetInteractable(loginButton, !busy);
            SetInteractable(registerButton, !busy);
            SetInteractable(resetPasswordButton, !busy);
            SetInteractable(offlineButton, !busy);
            SetInteractable(showSignInButton, !busy);
            SetInteractable(showSignUpButton, !busy);
            SetInteractable(showGuestButton, !busy);
            SetInteractable(createAccountButton, !busy);
            SetInteractable(alreadyAccountButton, !busy);
            SetInteractable(signInPasswordToggleButton, !busy);
            SetInteractable(signUpPasswordToggleButton, !busy);
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

        private void SetMode(AuthMode mode, bool animate)
        {
            _mode = mode;

            if (signInPanel == null && signUpPanel == null && guestPanel == null)
            {
                UpdateModeButtonState();
                return;
            }

            var target = PanelFor(mode);
            HidePanel(signInPanel, target);
            HidePanel(signUpPanel, target);
            HidePanel(guestPanel, target);

            if (target != null)
            {
                target.gameObject.SetActive(true);
                target.interactable = true;
                target.blocksRaycasts = true;

                if (_modeRoutine != null)
                {
                    StopCoroutine(_modeRoutine);
                    _modeRoutine = null;
                }

                if (animate && isActiveAndEnabled && modeTransitionDuration > 0f)
                {
                    _modeRoutine = StartCoroutine(AnimatePanelIn(target));
                }
                else
                {
                    target.alpha = 1f;
                    target.transform.localScale = Vector3.one;
                }
            }

            UpdateModeButtonState();
        }

        private CanvasGroup PanelFor(AuthMode mode)
        {
            return mode switch
            {
                AuthMode.SignUp => signUpPanel,
                AuthMode.Guest => guestPanel,
                _ => signInPanel,
            };
        }

        private static void HidePanel(CanvasGroup panel, CanvasGroup target)
        {
            if (panel == null || panel == target)
            {
                return;
            }

            panel.alpha = 0f;
            panel.interactable = false;
            panel.blocksRaycasts = false;
            panel.gameObject.SetActive(false);
        }

        private IEnumerator AnimatePanelIn(CanvasGroup panel)
        {
            panel.alpha = 0f;
            panel.transform.localScale = Vector3.one * 0.975f;
            float elapsed = 0f;
            while (elapsed < modeTransitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / modeTransitionDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                panel.alpha = eased;
                panel.transform.localScale = Vector3.Lerp(Vector3.one * 0.975f, Vector3.one, eased);
                yield return null;
            }

            panel.alpha = 1f;
            panel.transform.localScale = Vector3.one;
            _modeRoutine = null;
        }

        private void UpdateModeButtonState()
        {
            var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);
            ApplyModeButton(showSignInButton, _mode == AuthMode.SignIn, palette);
            ApplyModeButton(showSignUpButton, _mode == AuthMode.SignUp, palette);
            ApplyModeButton(showGuestButton, _mode == AuthMode.Guest, palette);
        }

        private static void ApplyModeButton(Button button, bool active, GameplayPalette palette)
        {
            if (button == null)
            {
                return;
            }

            ApplyButton(button, active ? palette.UiButton : palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
        }

        private static void PreparePasswordField(TMP_InputField input)
        {
            if (input == null)
            {
                return;
            }

            input.contentType = TMP_InputField.ContentType.Password;
            input.ForceLabelUpdate();
        }

        private static void SetPasswordVisibility(TMP_InputField input, Button toggleButton, bool visible)
        {
            if (input != null)
            {
                input.contentType = visible ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
                input.ForceLabelUpdate();
            }

            var label = toggleButton != null ? toggleButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (label != null)
            {
                label.text = visible ? "Masquer" : "Voir";
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
