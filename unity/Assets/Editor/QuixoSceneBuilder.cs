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

        private static readonly Color BackgroundColor = new(0.08f, 0.1f, 0.13f);
        private static readonly Color PanelColor = new(0.13f, 0.16f, 0.2f, 0.92f);
        private static readonly Color PrimaryColor = new(0.25f, 0.47f, 0.95f);
        private static readonly Color SecondaryColor = new(0.95f, 0.45f, 0.28f);
        private static readonly Color TextColor = new(0.94f, 0.96f, 0.98f);
        private static readonly Color MutedTextColor = new(0.72f, 0.77f, 0.84f);

        [MenuItem("Tools/Quixo/Create/Repair Scenes")]
        public static void CreateOrRepairScenes()
        {
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
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.45f, 0.46f, 0.48f);

            var camera = CreateMainCamera();
            CreateDirectionalLight();
            CreateEventSystem();

            var boardViewObject = new GameObject("BoardView");
            var boardView = boardViewObject.AddComponent<BoardViewRenderer>();

            var boardRoot = new GameObject("BoardRoot");
            boardRoot.transform.SetParent(boardViewObject.transform, false);

            var hudObject = CreateCanvas("HUD");
            var hudView = hudObject.AddComponent<HudView>();
            BuildGameplayHud(hudObject.transform, hudView);

            var gameRoot = new GameObject("GameRoot");
            var gameFlow = gameRoot.AddComponent<GameFlowController>();

            AssignObject(gameFlow, "boardView", boardView);
            AssignObject(gameFlow, "hudView", hudView);
            AssignObject(boardView, "boardRoot", boardRoot.transform);

            Selection.activeGameObject = camera.gameObject;
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
        }

        private static void BuildGameplayHud(Transform canvas, HudView hudView)
        {
            CreateFullScreenImage(canvas, "HudBackground", new Color(0f, 0f, 0f, 0f));

            var topBar = CreatePanel(canvas, "TopBar", new Vector2(0f, 96f), new Color(0.05f, 0.06f, 0.08f, 0.72f));
            var topRect = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.anchoredPosition = Vector2.zero;
            topRect.sizeDelta = new Vector2(0f, 96f);

            var turnLabel = CreateText(topBar.transform, "TurnLabel", "Tour: Joueur 1 (X)", 28f, TextAlignmentOptions.Left, TextColor,
                new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(520f, 54f));
            var infoLabel = CreateText(topBar.transform, "InfoLabel", "Choisissez un cube du bord libre ou a vous.", 20f, TextAlignmentOptions.Left, MutedTextColor,
                new Vector2(0f, 0.5f), new Vector2(48f, -34f), new Vector2(820f, 40f));

            var restartButton = CreateButton(topBar.transform, "RestartButton", "Recommencer", new Color(0.22f, 0.27f, 0.33f),
                new Vector2(1f, 0.5f), new Vector2(-235f, 0f), new Vector2(180f, 52f));
            var menuButton = CreateButton(topBar.transform, "MenuButton", "Menu", new Color(0.22f, 0.27f, 0.33f),
                new Vector2(1f, 0.5f), new Vector2(-82f, 0f), new Vector2(116f, 52f));

            var directionsPanel = CreatePanel(canvas, "DirectionsPanel", new Vector2(320f, 260f), new Color(0.05f, 0.06f, 0.08f, 0.72f));
            SetAnchored(directionsPanel.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(-210f, 170f), new Vector2(320f, 260f));

            CreateText(directionsPanel.transform, "DirectionsTitle", "Directions", 22f, TextAlignmentOptions.Center, TextColor,
                new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(260f, 42f));

            var upButton = CreateButton(directionsPanel.transform, "UpButton", "Haut", PrimaryColor,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 58f), new Vector2(110f, 54f));
            var downButton = CreateButton(directionsPanel.transform, "DownButton", "Bas", PrimaryColor,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -72f), new Vector2(110f, 54f));
            var leftButton = CreateButton(directionsPanel.transform, "LeftButton", "Gauche", PrimaryColor,
                new Vector2(0.5f, 0.5f), new Vector2(-92f, -7f), new Vector2(110f, 54f));
            var rightButton = CreateButton(directionsPanel.transform, "RightButton", "Droite", PrimaryColor,
                new Vector2(0.5f, 0.5f), new Vector2(92f, -7f), new Vector2(110f, 54f));

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

        private static Camera CreateMainCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 6.1f, -7.2f);
            cameraObject.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.14f, 0.17f);
            camera.fieldOfView = 42f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<PhysicsRaycaster>();
            return camera;
        }

        private static void CreateDirectionalLight()
        {
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.96f, 0.9f);
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
            label.raycastTarget = false;

            SetAnchored(label.rectTransform, anchor, anchoredPosition, size);
            return label;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Color normalColor,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = normalColor;

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.14f);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.16f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.18f, 0.2f, 0.24f, 0.55f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            SetAnchored(buttonObject.GetComponent<RectTransform>(), anchor, anchoredPosition, size);

            var text = CreateText(buttonObject.transform, "Text", label, 20f, TextAlignmentOptions.Center, TextColor,
                new Vector2(0.5f, 0.5f), Vector2.zero, size);
            text.enableAutoSizing = true;
            text.fontSizeMin = 12f;
            text.fontSizeMax = 22f;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

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
