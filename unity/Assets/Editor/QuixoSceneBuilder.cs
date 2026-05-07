using System.Collections.Generic;
using System.Linq;
using QuixoUnity.Gameplay;
using QuixoUnity.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace QuixoUnity.EditorTools
{
    public static class QuixoSceneBuilder
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
        private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
        private const GameplayTheme ActiveGameplayTheme = GameplayTheme.ClassicWood;

        private enum GameplayTheme
        {
            ClassicWood,
            PremiumDark,
            CleanModern,
        }

        private readonly struct GameplayPalette
        {
            public GameplayPalette(
                Color ambientLight,
                Color cameraBackground,
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

            public Color AmbientLight { get; }
            public Color CameraBackground { get; }
            public Color Board { get; }
            public Color BoardTrim { get; }
            public Color Cube { get; }
            public Color CubeTop { get; }
            public Color SelectedCube { get; }
            public Color Selection { get; }
            public Color Player1 { get; }
            public Color Player2 { get; }
            public Color UiPanel { get; }
            public Color UiButton { get; }
            public Color UiButtonSecondary { get; }
            public Color UiButtonDisabled { get; }
            public Color UiText { get; }
            public Color UiMuted { get; }
            public Color KeyLight { get; }
            public float CameraSize { get; }
            public float BoardScale { get; }
            public float MarkFontSize { get; }
        }

        private static readonly Color BackgroundColor = new(0.08f, 0.1f, 0.13f);
        private static readonly Color PanelColor = new(0.13f, 0.16f, 0.2f, 0.92f);
        private static readonly Color PrimaryColor = new(0.25f, 0.47f, 0.95f);
        private static readonly Color SecondaryColor = new(0.95f, 0.45f, 0.28f);
        private static readonly Color TextColor = new(0.94f, 0.96f, 0.98f);
        private static readonly Color MutedTextColor = new(0.72f, 0.77f, 0.84f);

        [MenuItem("Tools/Quixo/Create/Repair Scenes")]
        public static void CreateOrRepairScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Quixo",
                    "Stop Play Mode before creating or repairing scenes.",
                    "OK");
                return;
            }

            EnsureScenesFolder();
            CreateMenuScene();
            CreateGameplayScene();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "Quixo",
                "MenuScene et GameplayScene ont ete creees ou reparees. Ouvrez MenuScene puis appuyez sur Play.",
                "OK");
        }

        private static void CreateMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.3f, 0.32f, 0.36f);

            CreateEventSystem();

            var canvas = CreateCanvas("Canvas");
            CreateFullScreenImage(canvas.transform, "Background", BackgroundColor);

            var menuRoot = new GameObject("MenuRoot");
            var menuController = menuRoot.AddComponent<MenuController>();

            var panel = CreatePanel(canvas.transform, "MenuPanel", new Vector2(560f, 560f), PanelColor);
            SetAnchored(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(560f, 560f));

            CreateText(panel.transform, "Title", "Quixo / Qomet", 44f, TextAlignmentOptions.Center, TextColor,
                new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(480f, 72f));
            CreateText(panel.transform, "Subtitle", "Choisissez un mode de jeu local", 22f, TextAlignmentOptions.Center, MutedTextColor,
                new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(480f, 44f));

            var quixoButton = CreateButton(panel.transform, "QuixoButton", "Jouer Quixo", PrimaryColor,
                new Vector2(0.5f, 1f), new Vector2(0f, -235f), new Vector2(360f, 68f));
            var qometButton = CreateButton(panel.transform, "QometButton", "Jouer Qomet", SecondaryColor,
                new Vector2(0.5f, 1f), new Vector2(0f, -325f), new Vector2(360f, 68f));
            var quitButton = CreateButton(panel.transform, "QuitButton", "Quitter", new Color(0.28f, 0.32f, 0.38f),
                new Vector2(0.5f, 1f), new Vector2(0f, -415f), new Vector2(360f, 60f));

            UnityEventTools.AddPersistentListener(quixoButton.onClick, menuController.StartQuixo);
            UnityEventTools.AddPersistentListener(qometButton.onClick, menuController.StartQomet);
            UnityEventTools.AddPersistentListener(quitButton.onClick, menuController.Quit);

            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        private static void CreateGameplayScene()
        {
            var palette = GetPalette(ActiveGameplayTheme);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = palette.AmbientLight;

            var camera = CreateMainCamera(palette);
            CreateDirectionalLight(palette);
            CreateEventSystem();

            var boardViewObject = new GameObject("BoardView");
            boardViewObject.transform.localScale = Vector3.one * palette.BoardScale;
            var boardView = boardViewObject.AddComponent<BoardViewRenderer>();

            var boardRoot = new GameObject("BoardRoot");
            boardRoot.transform.SetParent(boardViewObject.transform, false);
            boardRoot.transform.localPosition = Vector3.zero;
            ApplyBoardTheme(boardView, palette);

            var hudObject = CreateCanvas("HUD");
            var hudView = hudObject.AddComponent<HudView>();
            BuildGameplayHud(hudObject.transform, hudView, palette);
            AssignColor(hudView, "turnPlayer1Color", palette.Player1);
            AssignColor(hudView, "turnPlayer2Color", palette.Player2);

            var gameRoot = new GameObject("GameRoot");
            var gameFlow = gameRoot.AddComponent<GameFlowController>();

            AssignObject(gameFlow, "boardView", boardView);
            AssignObject(gameFlow, "hudView", hudView);
            AssignObject(boardView, "boardRoot", boardRoot.transform);

            Selection.activeGameObject = camera.gameObject;
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
        }

        private static void ApplyBoardTheme(BoardViewRenderer boardView, GameplayPalette palette)
        {
            AssignColor(boardView, "generatedCellColor", palette.Cube);
            AssignColor(boardView, "generatedTopColor", palette.CubeTop);
            AssignColor(boardView, "generatedSelectedCellColor", palette.SelectedCube);
            AssignColor(boardView, "generatedBoardColor", palette.Board);
            AssignColor(boardView, "generatedBoardTrimColor", palette.BoardTrim);
            AssignColor(boardView, "generatedSelectionColor", palette.Selection);
            AssignColor(boardView, "generatedPlayer1Color", palette.Player1);
            AssignColor(boardView, "generatedPlayer2Color", palette.Player2);
            AssignFloat(boardView, "generatedMarkFontSize", palette.MarkFontSize);
        }

        private static GameplayPalette GetPalette(GameplayTheme theme)
        {
            switch (theme)
            {
                case GameplayTheme.PremiumDark:
                    return new GameplayPalette(
                        new Color(0.44f, 0.42f, 0.38f),
                        new Color(0.08f, 0.09f, 0.11f),
                        new Color(0.72f, 0.5f, 0.23f),
                        new Color(0.43f, 0.28f, 0.12f),
                        new Color(0.96f, 0.91f, 0.78f),
                        new Color(1f, 0.96f, 0.84f),
                        new Color(1f, 0.88f, 0.52f),
                        new Color(1f, 0.73f, 0.18f),
                        new Color(0.16f, 0.45f, 0.95f),
                        new Color(0.95f, 0.38f, 0.08f),
                        new Color(0.02f, 0.025f, 0.03f, 0.32f),
                        new Color(0.18f, 0.28f, 0.42f),
                        new Color(0.34f, 0.24f, 0.14f),
                        new Color(0.11f, 0.12f, 0.14f, 0.58f),
                        new Color(0.96f, 0.94f, 0.88f),
                        new Color(0.76f, 0.78f, 0.82f),
                        new Color(1f, 0.9f, 0.68f),
                        4.85f,
                        1.45f,
                        4.55f);
                case GameplayTheme.CleanModern:
                    return new GameplayPalette(
                        new Color(0.86f, 0.86f, 0.82f),
                        new Color(0.78f, 0.8f, 0.82f),
                        new Color(0.73f, 0.56f, 0.34f),
                        new Color(0.51f, 0.37f, 0.2f),
                        new Color(0.94f, 0.87f, 0.71f),
                        new Color(1f, 0.94f, 0.78f),
                        new Color(1f, 0.92f, 0.68f),
                        new Color(0.95f, 0.65f, 0.18f),
                        new Color(0.03f, 0.18f, 0.4f),
                        new Color(0.78f, 0.28f, 0.2f),
                        new Color(1f, 0.98f, 0.92f, 0.32f),
                        new Color(0.16f, 0.36f, 0.58f),
                        new Color(0.46f, 0.43f, 0.38f),
                        new Color(0.74f, 0.73f, 0.69f, 0.58f),
                        new Color(0.12f, 0.13f, 0.14f),
                        new Color(0.28f, 0.29f, 0.3f),
                        new Color(1f, 0.94f, 0.82f),
                        4.85f,
                        1.45f,
                        4.5f);
                default:
                    return new GameplayPalette(
                        new Color(0.82f, 0.78f, 0.69f),
                        new Color(0.8f, 0.74f, 0.62f),
                        new Color(0.7f, 0.51f, 0.29f),
                        new Color(0.46f, 0.31f, 0.15f),
                        new Color(0.92f, 0.82f, 0.62f),
                        new Color(1f, 0.91f, 0.7f),
                        new Color(1f, 0.9f, 0.6f),
                        new Color(1f, 0.74f, 0.16f),
                        new Color(0.04f, 0.14f, 0.34f),
                        new Color(0.62f, 0.18f, 0.09f),
                        new Color(0.16f, 0.1f, 0.04f, 0.22f),
                        new Color(0.58f, 0.38f, 0.17f),
                        new Color(0.39f, 0.27f, 0.16f),
                        new Color(0.52f, 0.46f, 0.36f, 0.58f),
                        new Color(0.98f, 0.94f, 0.84f),
                        new Color(0.88f, 0.8f, 0.66f),
                        new Color(1f, 0.94f, 0.78f),
                        4.85f,
                        1.45f,
                        4.55f);
            }
        }

        private static void BuildGameplayHud(Transform canvas, HudView hudView, GameplayPalette palette)
        {
            CreateFullScreenImage(canvas, "HudBackground", new Color(0f, 0f, 0f, 0f));

            var statusPanel = CreatePanel(canvas, "StatusPanel", new Vector2(760f, 76f), palette.UiPanel);
            SetAnchored(statusPanel.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(32f, -28f), new Vector2(760f, 76f));

            var turnLabel = CreateText(statusPanel.transform, "TurnLabel", "Tour: Joueur 1 (X)", 28f, TextAlignmentOptions.Left, palette.UiText,
                new Vector2(0f, 1f), new Vector2(24f, -10f), new Vector2(420f, 34f));
            AddTextShadow(turnLabel);

            var infoLabel = CreateText(statusPanel.transform, "InfoLabel", "Choisissez un cube du bord libre ou a vous.", 19f, TextAlignmentOptions.Left, palette.UiMuted,
                new Vector2(0f, 1f), new Vector2(24f, -44f), new Vector2(700f, 28f));
            AddTextShadow(infoLabel);

            var restartButton = CreateButton(canvas, "RestartButton", "Recommencer", palette.UiButtonSecondary,
                new Vector2(1f, 1f), new Vector2(-184f, -32f), new Vector2(170f, 52f), palette.UiText, palette.UiButtonDisabled);
            var menuButton = CreateButton(canvas, "MenuButton", "Menu", palette.UiButtonSecondary,
                new Vector2(1f, 1f), new Vector2(-40f, -32f), new Vector2(112f, 52f), palette.UiText, palette.UiButtonDisabled);

            var directionsPanel = CreatePanel(canvas, "DirectionsPanel", new Vector2(348f, 236f), new Color(0f, 0f, 0f, 0f));
            SetAnchored(directionsPanel.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(-60f, 46f), new Vector2(348f, 236f));

            var upButton = CreateButton(directionsPanel.transform, "UpButton", "Haut", palette.UiButton,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 78f), new Vector2(146f, 64f), palette.UiText, palette.UiButtonDisabled);
            var downButton = CreateButton(directionsPanel.transform, "DownButton", "Bas", palette.UiButton,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(146f, 64f), palette.UiText, palette.UiButtonDisabled);
            var leftButton = CreateButton(directionsPanel.transform, "LeftButton", "Gauche", palette.UiButton,
                new Vector2(0.5f, 0.5f), new Vector2(-112f, 0f), new Vector2(146f, 64f), palette.UiText, palette.UiButtonDisabled);
            var rightButton = CreateButton(directionsPanel.transform, "RightButton", "Droite", palette.UiButton,
                new Vector2(0.5f, 0.5f), new Vector2(112f, 0f), new Vector2(146f, 64f), palette.UiText, palette.UiButtonDisabled);

            upButton.interactable = false;
            downButton.interactable = false;
            leftButton.interactable = false;
            rightButton.interactable = false;

            AssignObject(hudView, "turnLabel", turnLabel);
            AssignObject(hudView, "infoLabel", infoLabel);
            AssignObject(hudView, "restartButton", restartButton);
            AssignObject(hudView, "menuButton", menuButton);
            AssignObject(hudView, "upButton", upButton);
            AssignObject(hudView, "downButton", downButton);
            AssignObject(hudView, "leftButton", leftButton);
            AssignObject(hudView, "rightButton", rightButton);
        }

        private static Camera CreateMainCamera(GameplayPalette palette)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 7f, -7f);
            cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = palette.CameraBackground;
            camera.orthographic = true;
            camera.orthographicSize = palette.CameraSize;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<PhysicsRaycaster>();
            return camera;
        }

        private static void CreateDirectionalLight(GameplayPalette palette)
        {
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(48f, -36f, 0f);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.55f;
            light.color = palette.KeyLight;
        }

        private static void CreateEventSystem()
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static GameObject CreateCanvas(string name)
        {
            var canvasObject = new GameObject(name);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
            return canvasObject;
        }

        private static GameObject CreateFullScreenImage(Transform parent, string name, Color color)
        {
            var imageObject = new GameObject(name);
            imageObject.transform.SetParent(parent, false);

            var image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return imageObject;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Color color)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            var image = panel.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            return panel;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string text,
            float fontSize,
            TextAlignmentOptions alignment,
            Color color,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);

            var label = textObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;

            SetAnchored(label.rectTransform, anchor, anchoredPosition, size);
            return label;
        }

        private static void AddTextShadow(TextMeshProUGUI label)
        {
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.useGraphicAlpha = true;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Color normalColor,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size,
            Color? textColor = null,
            Color? disabledColor = null)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = normalColor;
            var shadow = buttonObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.22f);
            shadow.effectDistance = new Vector2(2f, -2f);
            var outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = Color.Lerp(normalColor, Color.white, 0.16f);
            outline.effectDistance = new Vector2(1f, 1f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = disabledColor ?? new Color(0.28f, 0.26f, 0.22f, 0.45f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            SetAnchored(buttonObject.GetComponent<RectTransform>(), anchor, anchoredPosition, size);

            var text = CreateText(buttonObject.transform, "Text", label, 20f, TextAlignmentOptions.Center, textColor ?? TextColor,
                new Vector2(0.5f, 0.5f), Vector2.zero, size);
            text.enableAutoSizing = true;
            text.fontSizeMin = 12f;
            text.fontSizeMax = 22f;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            AddTextShadow(text);

            return button;
        }

        private static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void AssignObject(Object target, string propertyName, Object value)
        {
            if (target == null)
            {
                Debug.LogError($"Impossible d'assigner '{propertyName}': target null.");
                return;
            }

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"Property '{propertyName}' introuvable sur {target.name}.", target);
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AssignColor(Object target, string propertyName, Color value)
        {
            if (target == null)
            {
                Debug.LogError($"Impossible d'assigner '{propertyName}': target null.");
                return;
            }

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"Property '{propertyName}' introuvable sur {target.name}.", target);
                return;
            }

            property.colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AssignFloat(Object target, string propertyName, float value)
        {
            if (target == null)
            {
                Debug.LogError($"Impossible d'assigner '{propertyName}': target null.");
                return;
            }

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"Property '{propertyName}' introuvable sur {target.name}.", target);
                return;
            }

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void EnsureScenesFolder()
        {
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }
        }

        private static void ConfigureBuildSettings()
        {
            var orderedScenes = new List<EditorBuildSettingsScene>
            {
                new(MenuScenePath, true),
                new(GameplayScenePath, true),
            };

            orderedScenes.AddRange(EditorBuildSettings.scenes
                .Where(scene => scene.path != MenuScenePath && scene.path != GameplayScenePath));

            EditorBuildSettings.scenes = orderedScenes.ToArray();
        }
    }
}
