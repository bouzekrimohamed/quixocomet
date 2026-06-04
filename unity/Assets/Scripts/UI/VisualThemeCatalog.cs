using System;
using UnityEngine;

namespace QuixoUnity.UI
{
    public enum GameplayTheme
    {
        ClassicWood,
        PremiumDark,
        CleanModern,
        MarineBlue,
        EmeraldGreen,
        RoyalPurple
    }

    public readonly struct GameplayPalette
    {
        public readonly Color AmbientLight;
        public readonly Color CameraBackground;
        public readonly Color MenuBackground;
        public readonly Color MenuPanel;
        public readonly Color Board;
        public readonly Color BoardTrim;
        public readonly Color Cube;
        public readonly Color CubeTop;
        public readonly Color SelectedCube;
        public readonly Color Selection;
        public readonly Color Player1;
        public readonly Color Player2;
        public readonly Color UiPanel;
        public readonly Color UiButton;
        public readonly Color UiButtonSecondary;
        public readonly Color UiButtonDisabled;
        public readonly Color UiText;
        public readonly Color UiMuted;
        public readonly Color KeyLight;
        public readonly float CameraSize;
        public readonly float BoardScale;
        public readonly float MarkFontSize;

        public GameplayPalette(
            Color ambientLight,
            Color cameraBackground,
            Color menuBackground,
            Color menuPanel,
            Color board,
            Color boardTrim,
            Color cube,
            Color cubeTop,
            Color selectedCube,
            Color selection,
            Color player1,
            Color player2,
            Color uiPanel,
            Color uiButton,
            Color uiButtonSecondary,
            Color uiButtonDisabled,
            Color uiText,
            Color uiMuted,
            Color keyLight,
            float cameraSize,
            float boardScale,
            float markFontSize)
        {
            AmbientLight = ambientLight;
            CameraBackground = cameraBackground;
            MenuBackground = menuBackground;
            MenuPanel = menuPanel;
            Board = board;
            BoardTrim = boardTrim;
            Cube = cube;
            CubeTop = cubeTop;
            SelectedCube = selectedCube;
            Selection = selection;
            Player1 = player1;
            Player2 = player2;
            UiPanel = uiPanel;
            UiButton = uiButton;
            UiButtonSecondary = uiButtonSecondary;
            UiButtonDisabled = uiButtonDisabled;
            UiText = uiText;
            UiMuted = uiMuted;
            KeyLight = keyLight;
            CameraSize = cameraSize;
            BoardScale = boardScale;
            MarkFontSize = markFontSize;
        }
    }

    public static class VisualThemeCatalog
    {
        public const GameplayTheme DefaultTheme = GameplayTheme.MarineBlue;

        private const string PlayerPrefsKey = "Quixo.ActiveTheme";

        public static GameplayTheme ActiveTheme
        {
            get
            {
                string savedTheme = PlayerPrefs.GetString(PlayerPrefsKey, DefaultTheme.ToString());
                return Enum.TryParse(savedTheme, out GameplayTheme theme) ? theme : DefaultTheme;
            }
            set
            {
                PlayerPrefs.SetString(PlayerPrefsKey, value.ToString());
                PlayerPrefs.Save();
            }
        }

        public static GameplayTheme Next(GameplayTheme theme)
        {
            return theme switch
            {
                GameplayTheme.MarineBlue => GameplayTheme.EmeraldGreen,
                GameplayTheme.EmeraldGreen => GameplayTheme.RoyalPurple,
                GameplayTheme.RoyalPurple => GameplayTheme.ClassicWood,
                GameplayTheme.ClassicWood => GameplayTheme.PremiumDark,
                GameplayTheme.PremiumDark => GameplayTheme.CleanModern,
                _ => GameplayTheme.MarineBlue
            };
        }

        public static string DisplayName(GameplayTheme theme)
        {
            return theme.ToString();
        }

        public static GameplayPalette Get(GameplayTheme theme)
        {
            return theme switch
            {
                GameplayTheme.PremiumDark => PremiumDark(),
                GameplayTheme.CleanModern => CleanModern(),
                GameplayTheme.EmeraldGreen => EmeraldGreen(),
                GameplayTheme.RoyalPurple => RoyalPurple(),
                GameplayTheme.ClassicWood => ClassicWood(),
                _ => MarineBlue()
            };
        }

        private static GameplayPalette MarineBlue()
        {
            return new GameplayPalette(
                new Color(0.64f, 0.68f, 0.72f, 1f),
                new Color(0.035f, 0.060f, 0.105f, 1f),
                new Color(0.035f, 0.060f, 0.105f, 1f),
                new Color(0.045f, 0.085f, 0.150f, 0.88f),
                new Color(0.70f, 0.50f, 0.27f, 1f),
                new Color(0.33f, 0.20f, 0.10f, 1f),
                new Color(0.92f, 0.87f, 0.72f, 1f),
                new Color(0.98f, 0.94f, 0.82f, 1f),
                new Color(0.94f, 0.82f, 0.47f, 1f),
                new Color(0.18f, 0.45f, 0.70f, 1f),
                new Color(0.04f, 0.15f, 0.32f, 1f),
                new Color(0.72f, 0.26f, 0.16f, 1f),
                new Color(0.045f, 0.085f, 0.150f, 0.72f),
                new Color(0.075f, 0.185f, 0.315f, 0.96f),
                new Color(0.58f, 0.41f, 0.22f, 0.96f),
                new Color(0.10f, 0.15f, 0.21f, 0.44f),
                new Color(0.98f, 0.92f, 0.78f, 1f),
                new Color(0.80f, 0.75f, 0.64f, 1f),
                new Color(1.00f, 0.88f, 0.62f, 1f),
                4.9f,
                1.44f,
                4.8f);
        }

        private static GameplayPalette EmeraldGreen()
        {
            return new GameplayPalette(
                new Color(0.58f, 0.65f, 0.58f, 1f),
                new Color(0.035f, 0.090f, 0.080f, 1f),
                new Color(0.035f, 0.090f, 0.080f, 1f),
                new Color(0.040f, 0.130f, 0.110f, 0.88f),
                new Color(0.72f, 0.53f, 0.30f, 1f),
                new Color(0.33f, 0.24f, 0.12f, 1f),
                new Color(0.92f, 0.89f, 0.76f, 1f),
                new Color(0.97f, 0.94f, 0.82f, 1f),
                new Color(0.80f, 0.90f, 0.58f, 1f),
                new Color(0.10f, 0.48f, 0.34f, 1f),
                new Color(0.03f, 0.34f, 0.24f, 1f),
                new Color(0.85f, 0.49f, 0.13f, 1f),
                new Color(0.035f, 0.130f, 0.110f, 0.76f),
                new Color(0.060f, 0.290f, 0.210f, 0.96f),
                new Color(0.62f, 0.44f, 0.19f, 0.96f),
                new Color(0.10f, 0.18f, 0.14f, 0.44f),
                new Color(0.96f, 0.92f, 0.76f, 1f),
                new Color(0.76f, 0.73f, 0.61f, 1f),
                new Color(1.00f, 0.87f, 0.57f, 1f),
                4.9f,
                1.44f,
                4.8f);
        }

        private static GameplayPalette RoyalPurple()
        {
            return new GameplayPalette(
                new Color(0.62f, 0.58f, 0.70f, 1f),
                new Color(0.075f, 0.045f, 0.135f, 1f),
                new Color(0.075f, 0.045f, 0.135f, 1f),
                new Color(0.130f, 0.070f, 0.205f, 0.88f),
                new Color(0.73f, 0.54f, 0.32f, 1f),
                new Color(0.35f, 0.22f, 0.13f, 1f),
                new Color(0.93f, 0.88f, 0.76f, 1f),
                new Color(0.98f, 0.93f, 0.82f, 1f),
                new Color(0.90f, 0.78f, 0.45f, 1f),
                new Color(0.38f, 0.20f, 0.62f, 1f),
                new Color(0.27f, 0.12f, 0.45f, 1f),
                new Color(0.87f, 0.60f, 0.18f, 1f),
                new Color(0.120f, 0.065f, 0.190f, 0.76f),
                new Color(0.255f, 0.115f, 0.430f, 0.96f),
                new Color(0.62f, 0.42f, 0.18f, 0.96f),
                new Color(0.16f, 0.10f, 0.22f, 0.44f),
                new Color(0.98f, 0.92f, 0.78f, 1f),
                new Color(0.78f, 0.71f, 0.62f, 1f),
                new Color(1.00f, 0.86f, 0.58f, 1f),
                4.9f,
                1.44f,
                4.8f);
        }

        private static GameplayPalette ClassicWood()
        {
            return new GameplayPalette(
                new Color(0.67f, 0.60f, 0.50f, 1f),
                new Color(0.78f, 0.74f, 0.65f, 1f),
                new Color(0.78f, 0.74f, 0.65f, 1f),
                new Color(0.95f, 0.90f, 0.78f, 0.96f),
                new Color(0.72f, 0.49f, 0.25f, 1f),
                new Color(0.37f, 0.20f, 0.09f, 1f),
                new Color(0.88f, 0.78f, 0.55f, 1f),
                new Color(0.97f, 0.90f, 0.70f, 1f),
                new Color(1.00f, 0.86f, 0.43f, 1f),
                new Color(0.18f, 0.46f, 0.72f, 1f),
                new Color(0.04f, 0.16f, 0.34f, 1f),
                new Color(0.67f, 0.18f, 0.10f, 1f),
                new Color(0.96f, 0.90f, 0.76f, 0.80f),
                new Color(0.43f, 0.24f, 0.11f, 0.95f),
                new Color(0.67f, 0.43f, 0.20f, 0.95f),
                new Color(0.40f, 0.31f, 0.23f, 0.32f),
                new Color(0.18f, 0.10f, 0.05f, 1f),
                new Color(0.46f, 0.33f, 0.21f, 1f),
                new Color(1.00f, 0.90f, 0.66f, 1f),
                4.85f,
                1.43f,
                4.7f);
        }

        private static GameplayPalette PremiumDark()
        {
            return new GameplayPalette(
                new Color(0.50f, 0.47f, 0.40f, 1f),
                new Color(0.045f, 0.050f, 0.065f, 1f),
                new Color(0.045f, 0.050f, 0.065f, 1f),
                new Color(0.095f, 0.100f, 0.120f, 0.86f),
                new Color(0.70f, 0.48f, 0.22f, 1f),
                new Color(0.36f, 0.22f, 0.10f, 1f),
                new Color(0.94f, 0.86f, 0.68f, 1f),
                new Color(1.00f, 0.94f, 0.76f, 1f),
                new Color(0.96f, 0.76f, 0.34f, 1f),
                new Color(0.88f, 0.58f, 0.22f, 1f),
                new Color(0.10f, 0.36f, 0.70f, 1f),
                new Color(0.86f, 0.38f, 0.10f, 1f),
                new Color(0.085f, 0.095f, 0.115f, 0.74f),
                new Color(0.155f, 0.175f, 0.215f, 0.96f),
                new Color(0.50f, 0.34f, 0.16f, 0.96f),
                new Color(0.14f, 0.14f, 0.16f, 0.44f),
                new Color(0.99f, 0.98f, 0.94f, 1f),
                new Color(0.84f, 0.82f, 0.76f, 1f),
                new Color(1.00f, 0.82f, 0.48f, 1f),
                4.85f,
                1.43f,
                4.7f);
        }

        private static GameplayPalette CleanModern()
        {
            return new GameplayPalette(
                new Color(0.78f, 0.79f, 0.77f, 1f),
                new Color(0.86f, 0.88f, 0.87f, 1f),
                new Color(0.86f, 0.88f, 0.87f, 1f),
                new Color(0.98f, 0.96f, 0.90f, 0.96f),
                new Color(0.73f, 0.54f, 0.31f, 1f),
                new Color(0.48f, 0.33f, 0.18f, 1f),
                new Color(0.91f, 0.86f, 0.74f, 1f),
                new Color(0.99f, 0.95f, 0.84f, 1f),
                new Color(1.00f, 0.89f, 0.45f, 1f),
                new Color(0.12f, 0.35f, 0.66f, 1f),
                new Color(0.02f, 0.16f, 0.38f, 1f),
                new Color(0.82f, 0.32f, 0.22f, 1f),
                new Color(0.98f, 0.96f, 0.90f, 0.78f),
                new Color(0.13f, 0.26f, 0.44f, 0.95f),
                new Color(0.61f, 0.43f, 0.24f, 0.95f),
                new Color(0.66f, 0.68f, 0.66f, 0.35f),
                new Color(0.12f, 0.13f, 0.12f, 1f),
                new Color(0.44f, 0.45f, 0.43f, 1f),
                new Color(1.00f, 0.90f, 0.62f, 1f),
                4.85f,
                1.43f,
                4.7f);
        }
    }
}
