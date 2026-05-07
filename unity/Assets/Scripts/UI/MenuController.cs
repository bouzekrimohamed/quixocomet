using QuixoUnity.Core;
using QuixoUnity.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QuixoUnity.UI
{
    public sealed class MenuController : MonoBehaviour
    {
        private const string GameplaySceneName = "GameplayScene";

        [SerializeField] private GameKind nextGame = GameKind.Quixo;
        [SerializeField] private Button themeButton;

        private TextMeshProUGUI _themeButtonLabel;
        private bool _loadingGameplay;

        private void Awake()
        {
            SceneTransit.SelectedTheme = VisualThemeCatalog.ActiveTheme;
            ResolveThemeButton();
            BindThemeButton();
            ApplyMenuTheme();
            RefreshThemeButton();
        }

        public void StartQuixo()
        {
            StartGame(GameKind.Quixo);
        }

        public void StartQomet()
        {
            StartGame(GameKind.Qomet);
        }

        public void Quit()
        {
            Application.Quit();
        }

        public void CycleTheme()
        {
            VisualThemeCatalog.ActiveTheme = VisualThemeCatalog.Next(VisualThemeCatalog.ActiveTheme);
            SceneTransit.SelectedTheme = VisualThemeCatalog.ActiveTheme;
            ApplyMenuTheme();
            RefreshThemeButton();
        }

        private void StartGame(GameKind kind)
        {
            if (_loadingGameplay)
            {
                return;
            }

            nextGame = kind;
            SceneTransit.SelectedGame = nextGame;
            SceneTransit.SelectedTheme = VisualThemeCatalog.ActiveTheme;

            if (Application.CanStreamedLevelBeLoaded(GameplaySceneName))
            {
                _loadingGameplay = true;
                SceneManager.LoadScene(GameplaySceneName);
                return;
            }

            Debug.LogError($"Scene '{GameplaySceneName}' introuvable. Ajoutez-la aux Build Settings.", this);
        }

        private void ResolveThemeButton()
        {
            if (themeButton == null)
            {
                var themeObject = GameObject.Find("ThemeButton");
                if (themeObject != null)
                {
                    themeButton = themeObject.GetComponent<Button>();
                }
            }

            if (themeButton != null)
            {
                _themeButtonLabel = themeButton.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        private void BindThemeButton()
        {
            if (themeButton == null)
            {
                return;
            }

            themeButton.onClick.RemoveListener(CycleTheme);
            themeButton.onClick.AddListener(CycleTheme);
        }

        private void RefreshThemeButton()
        {
            if (_themeButtonLabel == null && themeButton != null)
            {
                _themeButtonLabel = themeButton.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (_themeButtonLabel != null)
            {
                _themeButtonLabel.text = $"Thème : {VisualThemeCatalog.DisplayName(VisualThemeCatalog.ActiveTheme)}";
            }
        }

        private void ApplyMenuTheme()
        {
            var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);
            SetImageColor("Background", palette.MenuBackground);
            SetImageColor("MenuPanel", palette.MenuPanel);
            ApplyButtonTheme(GameObject.Find("QuixoButton")?.GetComponent<Button>(), palette.UiButton, palette.UiText, palette.UiButtonDisabled);
            ApplyButtonTheme(GameObject.Find("QometButton")?.GetComponent<Button>(), palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            ApplyButtonTheme(themeButton, palette.UiButton, palette.UiText, palette.UiButtonDisabled);
            ApplyButtonTheme(GameObject.Find("QuitButton")?.GetComponent<Button>(), palette.UiButtonSecondary, palette.UiText, palette.UiButtonDisabled);
            SetTextColor("Title", palette.UiText);
            SetTextColor("Subtitle", palette.UiMuted);
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

        private static void ApplyButtonTheme(Button button, Color normalColor, Color textColor, Color disabledColor)
        {
            if (button == null)
            {
                return;
            }

            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = normalColor;
            }

            var colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = disabledColor;
            button.colors = colors;

            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.color = textColor;
            }
        }
    }

    public static class SceneTransit
    {
        public static GameKind SelectedGame = GameKind.Quixo;
        public static GameplayTheme SelectedTheme = VisualThemeCatalog.DefaultTheme;
    }
}
