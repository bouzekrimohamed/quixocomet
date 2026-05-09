using System.Collections.Generic;
using System.Linq;
using QuixoUnity.Auth;
using QuixoUnity.Gameplay;
using QuixoUnity.Social;
using QuixoUnity.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace QuixoUnity.EditorTools
{
    public static class QuixoSceneBuilder
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string IntroVideoScenePath = "Assets/Scenes/IntroVideoScene.unity";
        private const string SplashScenePath = "Assets/Scenes/SplashScene.unity";
        private const string AuthScenePath = "Assets/Scenes/AuthScene.unity";
        private const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
        private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
        private const string IntroVideoClipPath = "Assets/Videos/powered_by_mohamed_bouzekri.mp4";
        private const string IntroVideoClipFallbackPath = "Assets/Videos/powered_by_mohamed_bouzekri.mp4.mp4";
        // ===============================
        // CHANGE THEME HERE
        // Options:
        // ClassicWood, PremiumDark, CleanModern, MarineBlue, EmeraldGreen, RoyalPurple
        // ===============================
        private const GameplayTheme ActiveGameplayTheme = GameplayTheme.MarineBlue;

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
            CreateIntroVideoScene();
            CreateSplashScene();
            CreateAuthScene();
            CreateMenuScene();
            CreateGameplayScene();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "Quixo",
                "IntroVideoScene, SplashScene, AuthScene, MenuScene et GameplayScene ont ete creees ou reparees. Ouvrez IntroVideoScene puis appuyez sur Play.",
                "OK");
        }

        private static void CreateIntroVideoScene()
        {
            var palette = VisualThemeCatalog.Get(ActiveGameplayTheme);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = palette.AmbientLight;
            CreateEventSystem();

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = palette.CameraBackground;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            cameraObject.AddComponent<AudioListener>();

            var videoRoot = new GameObject("IntroVideoRoot");
            var videoPlayer = videoRoot.AddComponent<VideoPlayer>();
            var audioSource = videoRoot.AddComponent<AudioSource>();
            var controller = videoRoot.AddComponent<IntroVideoController>();
            videoPlayer.playOnAwake = false;
            videoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
            videoPlayer.targetCamera = camera;
            videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.controlledAudioTrackCount = 1;
            videoPlayer.SetTargetAudioSource(0, audioSource);
            videoPlayer.clip = LoadIntroVideoClip();
            AssignObject(controller, "videoPlayer", videoPlayer);

            var canvas = CreateCanvas("Canvas");
            CreateText(canvas.transform, "SkipHint", "Espace / Entree / clic pour passer", 18f, TextAlignmentOptions.Right, palette.UiMuted,
                new Vector2(1f, 0f), new Vector2(-36f, 28f), new Vector2(420f, 34f));

            EditorSceneManager.SaveScene(scene, IntroVideoScenePath);
        }

        private static void CreateSplashScene()
        {
            var palette = VisualThemeCatalog.Get(ActiveGameplayTheme);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = palette.AmbientLight;

            CreateEventSystem();

            var canvas = CreateCanvas("Canvas");
            CreateFullScreenImage(canvas.transform, "Background", palette.MenuBackground);

            var starLayer = new GameObject("StarLayer", typeof(RectTransform));
            starLayer.transform.SetParent(canvas.transform, false);
            var starRect = starLayer.GetComponent<RectTransform>();
            starRect.anchorMin = Vector2.zero;
            starRect.anchorMax = Vector2.one;
            starRect.offsetMin = Vector2.zero;
            starRect.offsetMax = Vector2.zero;
            CreateStars(starLayer.transform, palette);

            var content = new GameObject("SplashContent", typeof(RectTransform));
            content.transform.SetParent(canvas.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            SetAnchored(contentRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 280f));
            var contentGroup = content.AddComponent<CanvasGroup>();

            var powered = CreateText(content.transform, "PoweredLabel", "POWERED BY", 30f, TextAlignmentOptions.Center, palette.UiMuted,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 48f), new Vector2(720f, 52f));
            var name = CreateText(content.transform, "NameLabel", "MOHAMED BOUZEKRI", 54f, TextAlignmentOptions.Center, palette.UiText,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(720f, 76f));
            AddTextShadow(powered);
            AddTextShadow(name);

            var splashRoot = new GameObject("SplashRoot");
            var controller = splashRoot.AddComponent<SplashController>();
            AssignObject(controller, "contentGroup", contentGroup);
            AssignObject(controller, "contentRoot", contentRect);
            AssignObject(controller, "starRoot", starLayer.transform);
            AssignObject(controller, "poweredLabel", powered);
            AssignObject(controller, "nameLabel", name);

            EditorSceneManager.SaveScene(scene, SplashScenePath);
        }

        private static void CreateAuthScene()
        {
            var palette = VisualThemeCatalog.Get(ActiveGameplayTheme);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = palette.AmbientLight;

            CreateEventSystem();

            var canvas = CreateCanvas("Canvas");
            CreateFullScreenImage(canvas.transform, "Background", palette.MenuBackground);

            var authRoot = new GameObject("AuthRoot");
            var authService = authRoot.AddComponent<AuthService>();
            var authView = authRoot.AddComponent<AuthView>();

            var panel = CreatePanel(canvas.transform, "AuthPanel", new Vector2(620f, 760f), palette.MenuPanel);
            SetAnchored(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 760f));

            CreateText(panel.transform, "Title", "Quixo / Qomet", 44f, TextAlignmentOptions.Center, palette.UiText,
                new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(520f, 72f));
            CreateText(panel.transform, "Subtitle", "Connexion joueur", 22f, TextAlignmentOptions.Center, palette.UiMuted,
                new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(520f, 42f));

            var emailInput = CreateInput(panel.transform, "EmailInput", "Email ou username", false,
                new Vector2(0.5f, 1f), new Vector2(0f, -196f), new Vector2(430f, 58f), palette);
            var passwordInput = CreateInput(panel.transform, "PasswordInput", "Mot de passe", true,
                new Vector2(0.5f, 1f), new Vector2(0f, -276f), new Vector2(430f, 58f), palette);
            var usernameInput = CreateInput(panel.transform, "UsernameInput", "Username pour inscription", false,
                new Vector2(0.5f, 1f), new Vector2(0f, -356f), new Vector2(430f, 58f), palette);

            var loginButton = CreateButton(panel.transform, "LoginButton", "Connexion", palette.UiButton,
                new Vector2(0.5f, 1f), new Vector2(-112f, -440f), new Vector2(206f, 58f), palette.UiText, palette.UiButtonDisabled);
            var registerButton = CreateButton(panel.transform, "RegisterButton", "Inscription", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(112f, -440f), new Vector2(206f, 58f), palette.UiText, palette.UiButtonDisabled);
            var resetPasswordButton = CreateButton(panel.transform, "ResetPasswordButton", "Mot de passe oublie", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(0f, -510f), new Vector2(430f, 50f), palette.UiText, palette.UiButtonDisabled);
            var offlineButton = CreateButton(panel.transform, "OfflineButton", "Continuer hors ligne", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(0f, -580f), new Vector2(430f, 54f), palette.UiText, palette.UiButtonDisabled);
            var message = CreateText(panel.transform, "MessageLabel", "Configurez Supabase ou continuez hors ligne.", 18f, TextAlignmentOptions.Center, palette.UiMuted,
                new Vector2(0.5f, 1f), new Vector2(0f, -660f), new Vector2(500f, 54f));

            AssignObject(authView, "authService", authService);
            AssignObject(authView, "emailInput", emailInput);
            AssignObject(authView, "passwordInput", passwordInput);
            AssignObject(authView, "usernameInput", usernameInput);
            AssignObject(authView, "loginButton", loginButton);
            AssignObject(authView, "registerButton", registerButton);
            AssignObject(authView, "resetPasswordButton", resetPasswordButton);
            AssignObject(authView, "offlineButton", offlineButton);
            AssignObject(authView, "messageLabel", message);

            EditorSceneManager.SaveScene(scene, AuthScenePath);
        }

        private static void CreateMenuScene()
        {
            var palette = VisualThemeCatalog.Get(ActiveGameplayTheme);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = palette.AmbientLight;

            CreateEventSystem();

            var canvas = CreateCanvas("Canvas");
            CreateFullScreenImage(canvas.transform, "Background", palette.MenuBackground);

            var menuRoot = new GameObject("MenuRoot");
            var authService = menuRoot.AddComponent<AuthService>();
            var friendService = menuRoot.AddComponent<FriendService>();
            var menuController = menuRoot.AddComponent<MainMenuController>();

            var panel = CreatePanel(canvas.transform, "MenuPanel", new Vector2(680f, 760f), palette.MenuPanel);
            SetAnchored(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-340f, 0f), new Vector2(680f, 760f));

            CreateText(panel.transform, "Title", "Quixo / Qomet", 48f, TextAlignmentOptions.Center, palette.UiText,
                new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(560f, 76f));
            CreateText(panel.transform, "Subtitle", "Jeu de strategie local", 23f, TextAlignmentOptions.Center, palette.UiMuted,
                new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(560f, 44f));
            var connected = CreateText(panel.transform, "ConnectedLabel", "Connecte : Invite", 19f, TextAlignmentOptions.Center, palette.UiText,
                new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(560f, 34f));

            var quixoButton = CreateButton(panel.transform, "QuixoButton", "Jouer Quixo", palette.UiButton,
                new Vector2(0.5f, 1f), new Vector2(0f, -254f), new Vector2(420f, 64f), palette.UiText, palette.UiButtonDisabled);
            var qometButton = CreateButton(panel.transform, "QometButton", "Jouer Qomet", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(0f, -332f), new Vector2(420f, 64f), palette.UiText, palette.UiButtonDisabled);
            var friendsButton = CreateButton(panel.transform, "FriendsButton", "Amis", palette.UiButton,
                new Vector2(0.5f, 1f), new Vector2(0f, -410f), new Vector2(420f, 60f), palette.UiText, palette.UiButtonDisabled);
            var themeButton = CreateButton(panel.transform, "ThemeButton", $"Theme : {VisualThemeCatalog.DisplayName(ActiveGameplayTheme)}", palette.UiButton,
                new Vector2(0.5f, 1f), new Vector2(0f, -484f), new Vector2(420f, 60f), palette.UiText, palette.UiButtonDisabled);
            var logoutButton = CreateButton(panel.transform, "LogoutButton", "Deconnexion", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(0f, -558f), new Vector2(420f, 58f), palette.UiText, palette.UiButtonDisabled);
            var quitButton = CreateButton(panel.transform, "QuitButton", "Quitter", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(0f, -630f), new Vector2(420f, 56f), palette.UiText, palette.UiButtonDisabled);
            var status = CreateText(panel.transform, "MenuStatusLabel", "", 17f, TextAlignmentOptions.Center, palette.UiMuted,
                new Vector2(0.5f, 1f), new Vector2(0f, -704f), new Vector2(560f, 34f));

            var friendsPanel = BuildFriendsPanel(canvas.transform, palette);
            var friendsView = friendsPanel.AddComponent<FriendsView>();
            friendsPanel.SetActive(false);

            EditorSceneManager.SaveScene(scene, MenuScenePath);

            AssignObject(menuController, "authService", authService);
            AssignObject(menuController, "friendService", friendService);
            AssignObject(menuController, "connectedLabel", connected);
            AssignObject(menuController, "statusLabel", status);
            AssignObject(menuController, "quixoButton", quixoButton);
            AssignObject(menuController, "qometButton", qometButton);
            AssignObject(menuController, "friendsButton", friendsButton);
            AssignObject(menuController, "themeButton", themeButton);
            AssignObject(menuController, "logoutButton", logoutButton);
            AssignObject(menuController, "quitButton", quitButton);
            AssignObject(menuController, "friendsPanel", friendsPanel);
            AssignObject(menuController, "friendsView", friendsView);
            AssignObject(friendsView, "friendService", friendService);
            AssignObject(friendsView, "usernameInput", FindComponentInChildren<TMP_InputField>(friendsPanel.transform, "FriendUsernameInput"));
            AssignObject(friendsView, "addButton", FindComponentInChildren<Button>(friendsPanel.transform, "AddFriendButton"));
            AssignObject(friendsView, "refreshButton", FindComponentInChildren<Button>(friendsPanel.transform, "RefreshFriendsButton"));
            AssignObject(friendsView, "closeButton", FindComponentInChildren<Button>(friendsPanel.transform, "CloseFriendsButton"));
            AssignObject(friendsView, "statusLabel", FindComponentInChildren<TextMeshProUGUI>(friendsPanel.transform, "FriendsStatusLabel"));
            AssignObject(friendsView, "requestsContainer", FindComponentInChildren<RectTransform>(friendsPanel.transform, "RequestsContainer"));
            AssignObject(friendsView, "friendsContainer", FindComponentInChildren<RectTransform>(friendsPanel.transform, "FriendsContainer"));

            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        private static void CreateGameplayScene()
        {
            var palette = VisualThemeCatalog.Get(ActiveGameplayTheme);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = palette.AmbientLight;

            var camera = CreateMainCamera(palette);
            CreateDirectionalLight(palette);
            CreateEventSystem();

            var boardViewObject = new GameObject("BoardView");
            boardViewObject.transform.position = new Vector3(0f, 0f, -0.45f);
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
            AssignObject(boardView, "generatedMaterialShader", GetSafeBoardShader());
        }

        private static GameObject BuildFriendsPanel(Transform canvas, GameplayPalette palette)
        {
            var panel = CreatePanel(canvas, "FriendsPanel", new Vector2(720f, 760f), palette.MenuPanel);
            SetAnchored(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(390f, 0f), new Vector2(720f, 760f));

            CreateText(panel.transform, "FriendsTitle", "Amis", 38f, TextAlignmentOptions.Center, palette.UiText,
                new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(620f, 60f));
            CreateText(panel.transform, "FriendsSubtitle", "Preparation pour les parties en ligne V2", 18f, TextAlignmentOptions.Center, palette.UiMuted,
                new Vector2(0.5f, 1f), new Vector2(0f, -104f), new Vector2(620f, 34f));

            CreateInput(panel.transform, "FriendUsernameInput", "username ami", false,
                new Vector2(0.5f, 1f), new Vector2(-102f, -170f), new Vector2(360f, 54f), palette);
            CreateButton(panel.transform, "AddFriendButton", "Ajouter", palette.UiButton,
                new Vector2(0.5f, 1f), new Vector2(206f, -170f), new Vector2(170f, 54f), palette.UiText, palette.UiButtonDisabled);
            CreateButton(panel.transform, "RefreshFriendsButton", "Rafraichir", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(-102f, -236f), new Vector2(226f, 48f), palette.UiText, palette.UiButtonDisabled);
            CreateButton(panel.transform, "CloseFriendsButton", "Fermer", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(164f, -236f), new Vector2(226f, 48f), palette.UiText, palette.UiButtonDisabled);

            CreateText(panel.transform, "RequestsTitle", "Demandes recues", 20f, TextAlignmentOptions.Left, palette.UiText,
                new Vector2(0f, 1f), new Vector2(52f, -306f), new Vector2(300f, 34f));
            var requests = CreateListContainer(panel.transform, "RequestsContainer", new Vector2(0f, 1f), new Vector2(52f, -352f), new Vector2(616f, 140f));

            CreateText(panel.transform, "AcceptedTitle", "Amis acceptes", 20f, TextAlignmentOptions.Left, palette.UiText,
                new Vector2(0f, 1f), new Vector2(52f, -514f), new Vector2(300f, 34f));
            var friends = CreateListContainer(panel.transform, "FriendsContainer", new Vector2(0f, 1f), new Vector2(52f, -560f), new Vector2(616f, 110f));

            CreateText(panel.transform, "FriendsStatusLabel", "Connectez-vous pour synchroniser les amis.", 17f, TextAlignmentOptions.Center, palette.UiMuted,
                new Vector2(0.5f, 1f), new Vector2(0f, -702f), new Vector2(620f, 40f));

            requests.name = "RequestsContainer";
            friends.name = "FriendsContainer";
            return panel;
        }

        private static Shader GetSafeBoardShader()
        {
            if (GraphicsSettings.currentRenderPipeline != null || QualitySettings.renderPipeline != null)
            {
                return Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard")
                    ?? Shader.Find("Unlit/Color")
                    ?? Shader.Find("Sprites/Default")
                    ?? Shader.Find("UI/Default")
                    ?? Shader.Find("Hidden/Internal-Colored");
            }

            return Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("UI/Default")
                ?? Shader.Find("Hidden/Internal-Colored");
        }

        private static VideoClip LoadIntroVideoClip()
        {
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(IntroVideoClipPath);
            if (clip != null)
            {
                return clip;
            }

            return AssetDatabase.LoadAssetAtPath<VideoClip>(IntroVideoClipFallbackPath);
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
            cameraObject.transform.position = new Vector3(0f, 7.2f, -7.4f);
            cameraObject.transform.rotation = Quaternion.Euler(48f, 0f, 0f);

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

        private static TMP_InputField CreateInput(
            Transform parent,
            string name,
            string placeholder,
            bool password,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size,
            GameplayPalette palette)
        {
            var inputObject = new GameObject(name);
            inputObject.transform.SetParent(parent, false);

            var image = inputObject.AddComponent<Image>();
            image.color = Color.Lerp(palette.UiPanel, palette.CubeTop, 0.18f);

            var input = inputObject.AddComponent<TMP_InputField>();
            input.targetGraphic = image;
            input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = palette.UiText;
            input.selectionColor = new Color(palette.Selection.r, palette.Selection.g, palette.Selection.b, 0.35f);

            SetAnchored(inputObject.GetComponent<RectTransform>(), anchor, anchoredPosition, size);

            var text = CreateText(inputObject.transform, "Text", string.Empty, 20f, TextAlignmentOptions.Left, palette.UiText,
                new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Stretch(text.rectTransform, new Vector2(18f, 5f), new Vector2(-18f, -5f));

            var placeholderLabel = CreateText(inputObject.transform, "Placeholder", placeholder, 20f, TextAlignmentOptions.Left, palette.UiMuted,
                new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Stretch(placeholderLabel.rectTransform, new Vector2(18f, 5f), new Vector2(-18f, -5f));

            input.textComponent = text;
            input.placeholder = placeholderLabel;
            input.textViewport = inputObject.GetComponent<RectTransform>();
            return input;
        }

        private static RectTransform CreateListContainer(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            var container = new GameObject(name, typeof(RectTransform));
            container.transform.SetParent(parent, false);
            var rect = container.GetComponent<RectTransform>();
            SetAnchored(rect, anchor, anchoredPosition, size);

            var layout = container.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = 6f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            return rect;
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

        private static void CreateStars(Transform parent, GameplayPalette palette)
        {
            for (int i = 0; i < 34; i++)
            {
                var star = new GameObject($"Star_{i:00}");
                star.transform.SetParent(parent, false);
                var image = star.AddComponent<Image>();
                float alpha = 0.16f + (i % 5) * 0.045f;
                image.color = new Color(palette.UiText.r, palette.UiText.g, palette.UiText.b, alpha);
                image.raycastTarget = false;

                float x = Mathf.Sin(i * 1.73f) * 820f;
                float y = Mathf.Cos(i * 2.11f) * 430f;
                float size = 2.5f + (i % 4) * 1.2f;
                SetAnchored(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(size, size));
            }
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

            var text = CreateText(buttonObject.transform, "Text", label, 20f, TextAlignmentOptions.Center, textColor ?? Color.white,
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

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static T FindComponentInChildren<T>(Transform root, string name) where T : Component
        {
            if (root == null)
            {
                return null;
            }

            var components = root.GetComponentsInChildren<T>(true);
            foreach (var component in components)
            {
                if (component.name == name)
                {
                    return component;
                }
            }

            return null;
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
                new(IntroVideoScenePath, true),
                new(SplashScenePath, true),
                new(AuthScenePath, true),
                new(MenuScenePath, true),
                new(GameplayScenePath, true),
            };

            orderedScenes.AddRange(EditorBuildSettings.scenes
                .Where(scene => scene.path != IntroVideoScenePath
                    && scene.path != SplashScenePath
                    && scene.path != AuthScenePath
                    && scene.path != MenuScenePath
                    && scene.path != GameplayScenePath));

            EditorBuildSettings.scenes = orderedScenes.ToArray();
        }
    }
}
