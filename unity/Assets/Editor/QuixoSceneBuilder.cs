using System.Collections.Generic;
using System.Linq;
using QuixoUnity.Auth;
using QuixoUnity.Gameplay;
using QuixoUnity.Online;
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
            CreatePremiumBackdrop(canvas.transform, palette, "Auth");

            var authRoot = new GameObject("AuthRoot");
            var authService = authRoot.AddComponent<AuthService>();
            var authView = authRoot.AddComponent<AuthView>();

            var authPanelColor = WithAlpha(new Color(0.025f, 0.045f, 0.082f, 1f), 0.84f);
            var panel = CreatePanel(canvas.transform, "AuthPanel", new Vector2(760f, 790f), authPanelColor);
            SetAnchored(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 790f));
            CreatePanelAccent(panel.transform, palette);

            CreateText(panel.transform, "Title", "Quixo / Qomet", 48f, TextAlignmentOptions.Center, palette.UiText,
                new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(610f, 72f));
            CreateText(panel.transform, "Subtitle", "Espace joueur", 21f, TextAlignmentOptions.Center, palette.UiMuted,
                new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(610f, 42f));

            var showSignInButton = CreateButton(panel.transform, "ShowSignInButton", "Sign In", palette.UiButton,
                new Vector2(0.5f, 1f), new Vector2(-190f, -174f), new Vector2(170f, 48f), palette.UiText, palette.UiButtonDisabled);
            var showSignUpButton = CreateButton(panel.transform, "ShowSignUpButton", "Sign Up", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(0f, -174f), new Vector2(170f, 48f), palette.UiText, palette.UiButtonDisabled);
            var showGuestButton = CreateButton(panel.transform, "ShowGuestButton", "Guest", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(190f, -174f), new Vector2(170f, 48f), palette.UiText, palette.UiButtonDisabled);

            var signInPanel = CreateAuthModePanel(panel.transform, "SignInPanel", new Vector2(560f, 390f),
                new Vector2(0.5f, 1f), new Vector2(0f, -250f));
            CreateText(signInPanel.transform, "SignInTitle", "Connexion", 30f, TextAlignmentOptions.Left, palette.UiText,
                new Vector2(0f, 1f), new Vector2(0f, -4f), new Vector2(520f, 48f));
            var signInCredentialInput = CreateInput(signInPanel.transform, "SignInCredentialInput", "Email ou username", false,
                new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(500f, 58f), palette);
            var signInPasswordInput = CreateInput(signInPanel.transform, "SignInPasswordInput", "Mot de passe", true,
                new Vector2(0.5f, 1f), new Vector2(0f, -158f), new Vector2(500f, 58f), palette);
            var loginButton = CreateButton(signInPanel.transform, "LoginButton", "Connexion", palette.UiButton,
                new Vector2(0.5f, 1f), new Vector2(0f, -244f), new Vector2(500f, 58f), palette.UiText, palette.UiButtonDisabled);
            var resetPasswordButton = CreateButton(signInPanel.transform, "ResetPasswordButton", "Mot de passe oublie", WithAlpha(palette.UiButtonSecondary, 0.70f),
                new Vector2(0.5f, 1f), new Vector2(-128f, -318f), new Vector2(244f, 48f), palette.UiText, palette.UiButtonDisabled);
            var createAccountButton = CreateButton(signInPanel.transform, "CreateAccountButton", "Creer un compte", WithAlpha(palette.UiButtonSecondary, 0.70f),
                new Vector2(0.5f, 1f), new Vector2(128f, -318f), new Vector2(244f, 48f), palette.UiText, palette.UiButtonDisabled);

            var signUpPanel = CreateAuthModePanel(panel.transform, "SignUpPanel", new Vector2(560f, 420f),
                new Vector2(0.5f, 1f), new Vector2(0f, -250f));
            CreateText(signUpPanel.transform, "SignUpTitle", "Inscription", 30f, TextAlignmentOptions.Left, palette.UiText,
                new Vector2(0f, 1f), new Vector2(0f, -4f), new Vector2(520f, 48f));
            var signUpEmailInput = CreateInput(signUpPanel.transform, "SignUpEmailInput", "Email", false,
                new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(500f, 58f), palette);
            var signUpUsernameInput = CreateInput(signUpPanel.transform, "SignUpUsernameInput", "Username", false,
                new Vector2(0.5f, 1f), new Vector2(0f, -158f), new Vector2(500f, 58f), palette);
            var signUpPasswordInput = CreateInput(signUpPanel.transform, "SignUpPasswordInput", "Mot de passe", true,
                new Vector2(0.5f, 1f), new Vector2(0f, -234f), new Vector2(500f, 58f), palette);
            var registerButton = CreateButton(signUpPanel.transform, "RegisterButton", "Inscription", palette.UiButton,
                new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(500f, 58f), palette.UiText, palette.UiButtonDisabled);
            var alreadyAccountButton = CreateButton(signUpPanel.transform, "AlreadyAccountButton", "Deja un compte ? Connexion", WithAlpha(palette.UiButtonSecondary, 0.70f),
                new Vector2(0.5f, 1f), new Vector2(0f, -388f), new Vector2(500f, 48f), palette.UiText, palette.UiButtonDisabled);

            var guestPanel = CreateAuthModePanel(panel.transform, "GuestPanel", new Vector2(560f, 300f),
                new Vector2(0.5f, 1f), new Vector2(0f, -292f));
            var guestCard = CreatePanel(guestPanel.transform, "GuestCard", new Vector2(500f, 210f), WithAlpha(palette.UiPanel, 0.52f));
            SetAnchored(guestCard.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 210f));
            CreateText(guestCard.transform, "GuestTitle", "Jouer hors ligne sans compte", 25f, TextAlignmentOptions.Center, palette.UiText,
                new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(430f, 42f));
            CreateText(guestCard.transform, "GuestSubtitle", "Parties locales, sans synchronisation de profil.", 17f, TextAlignmentOptions.Center, palette.UiMuted,
                new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(430f, 34f));
            var offlineButton = CreateButton(guestCard.transform, "OfflineButton", "Continuer hors ligne", palette.UiButton,
                new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(360f, 54f), palette.UiText, palette.UiButtonDisabled);

            var message = CreateText(panel.transform, "MessageLabel", "Connectez-vous ou continuez hors ligne.", 18f, TextAlignmentOptions.Center, palette.UiMuted,
                new Vector2(0.5f, 1f), new Vector2(0f, -726f), new Vector2(620f, 50f));

            AssignObject(authView, "authService", authService);
            AssignObject(authView, "emailInput", signInCredentialInput);
            AssignObject(authView, "passwordInput", signInPasswordInput);
            AssignObject(authView, "usernameInput", signUpUsernameInput);
            AssignObject(authView, "signInCredentialInput", signInCredentialInput);
            AssignObject(authView, "signInPasswordInput", signInPasswordInput);
            AssignObject(authView, "signUpEmailInput", signUpEmailInput);
            AssignObject(authView, "signUpUsernameInput", signUpUsernameInput);
            AssignObject(authView, "signUpPasswordInput", signUpPasswordInput);
            AssignObject(authView, "loginButton", loginButton);
            AssignObject(authView, "registerButton", registerButton);
            AssignObject(authView, "resetPasswordButton", resetPasswordButton);
            AssignObject(authView, "offlineButton", offlineButton);
            AssignObject(authView, "showSignInButton", showSignInButton);
            AssignObject(authView, "showSignUpButton", showSignUpButton);
            AssignObject(authView, "showGuestButton", showGuestButton);
            AssignObject(authView, "createAccountButton", createAccountButton);
            AssignObject(authView, "alreadyAccountButton", alreadyAccountButton);
            AssignObject(authView, "signInPanel", signInPanel);
            AssignObject(authView, "signUpPanel", signUpPanel);
            AssignObject(authView, "guestPanel", guestPanel);
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
            CreatePremiumBackdrop(canvas.transform, palette, "Menu");

            var menuRoot = new GameObject("MenuRoot");
            var authService = menuRoot.AddComponent<AuthService>();
            var friendService = menuRoot.AddComponent<FriendService>();
            var onlineMatchService = menuRoot.AddComponent<OnlineMatchService>();
            var onlinePresenceService = menuRoot.AddComponent<OnlinePresenceService>();
            var menuController = menuRoot.AddComponent<MainMenuController>();

            var panel = CreatePanel(canvas.transform, "MenuPanel", new Vector2(680f, 820f), palette.MenuPanel);
            SetAnchored(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-340f, 0f), new Vector2(680f, 820f));
            CreatePanelAccent(panel.transform, palette);

            CreateText(panel.transform, "Title", "Quixo / Qomet", 48f, TextAlignmentOptions.Center, palette.UiText,
                new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(560f, 68f));
            CreateText(panel.transform, "Subtitle", "Jeu de strategie local et online", 22f, TextAlignmentOptions.Center, palette.UiMuted,
                new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(560f, 40f));
            var connected = CreateText(panel.transform, "ConnectedLabel", "Connecte : Invite", 19f, TextAlignmentOptions.Center, palette.UiText,
                new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(560f, 32f));

            var mainActionsPanel = CreateAuthModePanel(panel.transform, "MainActionsPanel", new Vector2(520f, 500f),
                new Vector2(0.5f, 1f), new Vector2(0f, -210f));
            var playButton = CreateButton(mainActionsPanel.transform, "PlayButton", "Jouer", palette.UiButton,
                new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(420f, 56f), palette.UiText, palette.UiButtonDisabled);
            var friendsButton = CreateButton(mainActionsPanel.transform, "FriendsButton", "Amis", palette.UiButton,
                new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(420f, 50f), palette.UiText, palette.UiButtonDisabled);
            var themeButton = CreateButton(mainActionsPanel.transform, "ThemeButton", $"Theme : {VisualThemeCatalog.DisplayName(ActiveGameplayTheme)}", palette.UiButton,
                new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(420f, 50f), palette.UiText, palette.UiButtonDisabled);
            var logoutButton = CreateButton(mainActionsPanel.transform, "LogoutButton", "Deconnexion", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(0f, -234f), new Vector2(420f, 50f), palette.UiText, palette.UiButtonDisabled);
            var quitButton = CreateButton(mainActionsPanel.transform, "QuitButton", "Quitter", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(0f, -296f), new Vector2(420f, 50f), palette.UiText, palette.UiButtonDisabled);

            var modePanel = CreateAuthModePanel(panel.transform, "ModePanel", new Vector2(520f, 360f),
                new Vector2(0.5f, 1f), new Vector2(0f, -250f));
            CreateText(modePanel.transform, "ModeTitle", "Choisir le mode", 27f, TextAlignmentOptions.Center, palette.UiText,
                new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(460f, 44f));
            var localButton = CreateButton(modePanel.transform, "LocalButton", "Jouer en local", palette.UiButton,
                new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(420f, 56f), palette.UiText, palette.UiButtonDisabled);
            var onlineButton = CreateButton(modePanel.transform, "OnlineButton", "Jouer en ligne", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(0f, -166f), new Vector2(420f, 56f), palette.UiText, palette.UiButtonDisabled);
            var modeBackButton = CreateButton(modePanel.transform, "ModeBackButton", "Retour", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(0f, -236f), new Vector2(420f, 50f), palette.UiText, palette.UiButtonDisabled);

            var gamePanel = CreateAuthModePanel(panel.transform, "GamePanel", new Vector2(520f, 500f),
                new Vector2(0.5f, 1f), new Vector2(0f, -220f));
            CreateText(gamePanel.transform, "GameTitle", "Choisir le jeu", 27f, TextAlignmentOptions.Center, palette.UiText,
                new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(460f, 44f));
            var quixoButton = CreateButton(gamePanel.transform, "QuixoButton", "Quixo", palette.UiButton,
                new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(420f, 56f), palette.UiText, palette.UiButtonDisabled);
            var qometButton = CreateButton(gamePanel.transform, "QometButton", "Qomet", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(0f, -152f), new Vector2(420f, 56f), palette.UiText, palette.UiButtonDisabled);
            var quixoOnlineButton = CreateButton(gamePanel.transform, "QuixoOnlineButton", "Matchmaking Quixo", palette.UiButton,
                new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(420f, 56f), palette.UiText, palette.UiButtonDisabled);
            var qometOnlineButton = CreateButton(gamePanel.transform, "QometOnlineButton", "Matchmaking Qomet", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(0f, -152f), new Vector2(420f, 56f), palette.UiText, palette.UiButtonDisabled);
            var gameBackButton = CreateButton(gamePanel.transform, "GameBackButton", "Retour", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(0f, -222f), new Vector2(420f, 50f), palette.UiText, palette.UiButtonDisabled);
            modePanel.gameObject.SetActive(false);
            gamePanel.gameObject.SetActive(false);
            var cancelOnlineButton = CreateButton(panel.transform, "CancelOnlineButton", "Annuler recherche", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(0f, -490f), new Vector2(420f, 50f), palette.UiText, palette.UiButtonDisabled);
            cancelOnlineButton.gameObject.SetActive(false);
            var status = CreateText(panel.transform, "MenuStatusLabel", "", 17f, TextAlignmentOptions.Center, palette.UiMuted,
                new Vector2(0.5f, 1f), new Vector2(0f, -750f), new Vector2(560f, 44f));

            // Renfort visuel : quelques X et O flottants qui s'ajoutent aux Token deja crees
            // par CreatePremiumBackdrop. Le MenuVisualAnimator les fera deriver doucement.
            CreateMenuFloatingMarks(canvas.transform, palette);

            var friendsPanel = BuildFriendsPanel(canvas.transform, palette);
            var friendsView = friendsPanel.AddComponent<FriendsView>();
            friendsPanel.SetActive(false);

            // Animator du menu : drift des particules, pulse du titre, fade-in du panneau,
            // pulsation legere des boutons online. Attache au Canvas pour qu'il voie tous
            // les enfants du fond ET le MenuPanel.
            canvas.AddComponent<MenuVisualAnimator>();

            EditorSceneManager.SaveScene(scene, MenuScenePath);

            AssignObject(menuController, "authService", authService);
            AssignObject(menuController, "friendService", friendService);
            AssignObject(menuController, "onlineMatchService", onlineMatchService);
            AssignObject(menuController, "onlinePresenceService", onlinePresenceService);
            AssignObject(menuController, "connectedLabel", connected);
            AssignObject(menuController, "statusLabel", status);
            AssignObject(menuController, "quixoButton", quixoButton);
            AssignObject(menuController, "qometButton", qometButton);
            AssignObject(menuController, "playButton", playButton);
            AssignObject(menuController, "localButton", localButton);
            AssignObject(menuController, "onlineButton", onlineButton);
            AssignObject(menuController, "modeBackButton", modeBackButton);
            AssignObject(menuController, "gameBackButton", gameBackButton);
            AssignObject(menuController, "quixoOnlineButton", quixoOnlineButton);
            AssignObject(menuController, "qometOnlineButton", qometOnlineButton);
            AssignObject(menuController, "cancelOnlineButton", cancelOnlineButton);
            AssignObject(menuController, "friendsButton", friendsButton);
            AssignObject(menuController, "themeButton", themeButton);
            AssignObject(menuController, "logoutButton", logoutButton);
            AssignObject(menuController, "quitButton", quitButton);
            AssignObject(menuController, "friendsPanel", friendsPanel);
            AssignObject(menuController, "mainActionsPanel", mainActionsPanel.gameObject);
            AssignObject(menuController, "modePanel", modePanel.gameObject);
            AssignObject(menuController, "gamePanel", gamePanel.gameObject);
            AssignObject(menuController, "friendsView", friendsView);
            AssignObject(friendsView, "friendService", friendService);
            AssignObject(friendsView, "onlineMatchService", onlineMatchService);
            AssignObject(friendsView, "onlinePresenceService", onlinePresenceService);
            AssignObject(friendsView, "usernameInput", FindComponentInChildren<TMP_InputField>(friendsPanel.transform, "FriendUsernameInput"));
            AssignObject(friendsView, "addButton", FindComponentInChildren<Button>(friendsPanel.transform, "AddFriendButton"));
            AssignObject(friendsView, "refreshButton", FindComponentInChildren<Button>(friendsPanel.transform, "RefreshFriendsButton"));
            AssignObject(friendsView, "closeButton", FindComponentInChildren<Button>(friendsPanel.transform, "CloseFriendsButton"));
            AssignObject(friendsView, "statusLabel", FindComponentInChildren<TextMeshProUGUI>(friendsPanel.transform, "FriendsStatusLabel"));
            AssignObject(friendsView, "requestsContainer", FindComponentInChildren<RectTransform>(friendsPanel.transform, "RequestsContainer"));
            AssignObject(friendsView, "invitesContainer", FindComponentInChildren<RectTransform>(friendsPanel.transform, "InvitesContainer"));
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
            CreateGameplayStage(boardViewObject.transform, palette);
            ApplyBoardTheme(boardView, palette);

            var hudObject = CreateCanvas("HUD");
            var hudView = hudObject.AddComponent<HudView>();
            BuildGameplayHud(hudObject.transform, hudView, palette);
            AssignColor(hudView, "turnPlayer1Color", palette.Player1);
            AssignColor(hudView, "turnPlayer2Color", palette.Player2);

            var gameRoot = new GameObject("GameRoot");
            var gameFlow = gameRoot.AddComponent<GameFlowController>();
            var onlineMatchService = gameRoot.AddComponent<OnlineMatchService>();
            var onlinePresenceService = gameRoot.AddComponent<OnlinePresenceService>();

            AssignObject(gameFlow, "boardView", boardView);
            AssignObject(gameFlow, "hudView", hudView);
            AssignObject(gameFlow, "onlineMatchService", onlineMatchService);
            AssignObject(gameFlow, "onlinePresenceService", onlinePresenceService);
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

        private static CanvasGroup CreateAuthModePanel(Transform parent, string name, Vector2 size, Vector2 anchor, Vector2 anchoredPosition)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            panel.transform.SetParent(parent, false);

            var rect = panel.GetComponent<RectTransform>();
            SetAnchored(rect, anchor, anchoredPosition, size);

            var canvasGroup = panel.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            return canvasGroup;
        }

        private static GameObject BuildFriendsPanel(Transform canvas, GameplayPalette palette)
        {
            const float panelWidth = 760f;
            const float panelHeight = 940f;
            const float listWidth = panelWidth - 80f;

            var panel = CreatePanel(canvas, "FriendsPanel", new Vector2(panelWidth, panelHeight), palette.MenuPanel);
            SetAnchored(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(400f, 0f), new Vector2(panelWidth, panelHeight));
            CreatePanelAccent(panel.transform, palette);

            CreateText(panel.transform, "FriendsTitle", "Amis et invitations", 32f, TextAlignmentOptions.Center, palette.UiText,
                new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(680f, 44f));
            CreateText(panel.transform, "FriendsSubtitle", "Ajoutez un ami par username et voyez les invitations recues.", 15f, TextAlignmentOptions.Center, palette.UiMuted,
                new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(680f, 22f));

            CreateInput(panel.transform, "FriendUsernameInput", "username ami", false,
                new Vector2(0.5f, 1f), new Vector2(-118f, -140f), new Vector2(380f, 52f), palette);
            CreateButton(panel.transform, "AddFriendButton", "Ajouter", palette.UiButton,
                new Vector2(0.5f, 1f), new Vector2(210f, -140f), new Vector2(170f, 52f), palette.UiText, palette.UiButtonDisabled);
            CreateButton(panel.transform, "RefreshFriendsButton", "Rafraichir", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(-128f, -204f), new Vector2(220f, 44f), palette.UiText, palette.UiButtonDisabled);
            CreateButton(panel.transform, "CloseFriendsButton", "Fermer", palette.UiButtonSecondary,
                new Vector2(0.5f, 1f), new Vector2(128f, -204f), new Vector2(220f, 44f), palette.UiText, palette.UiButtonDisabled);

            CreateText(panel.transform, "RequestsTitle", "Demandes d'amis recues", 19f, TextAlignmentOptions.Left, palette.UiText,
                new Vector2(0f, 1f), new Vector2(40f, -260f), new Vector2(420f, 26f));
            CreateScrollableListContainer(panel.transform, "RequestsContainer", palette,
                new Vector2(0f, 1f), new Vector2(40f, -294f), new Vector2(listWidth, 180f));

            CreateText(panel.transform, "InvitesTitle", "Invitations de partie recues", 19f, TextAlignmentOptions.Left, palette.UiText,
                new Vector2(0f, 1f), new Vector2(40f, -486f), new Vector2(460f, 26f));
            CreateScrollableListContainer(panel.transform, "InvitesContainer", palette,
                new Vector2(0f, 1f), new Vector2(40f, -520f), new Vector2(listWidth, 180f));

            CreateText(panel.transform, "AcceptedTitle", "Amis acceptes", 19f, TextAlignmentOptions.Left, palette.UiText,
                new Vector2(0f, 1f), new Vector2(40f, -712f), new Vector2(420f, 26f));
            CreateScrollableListContainer(panel.transform, "FriendsContainer", palette,
                new Vector2(0f, 1f), new Vector2(40f, -746f), new Vector2(listWidth, 140f));

            CreateText(panel.transform, "FriendsStatusLabel", "Connectez-vous pour synchroniser les amis.", 15f, TextAlignmentOptions.Center, palette.UiMuted,
                new Vector2(0.5f, 1f), new Vector2(0f, -898f), new Vector2(680f, 22f));

            return panel;
        }

        private static void CreateGameplayStage(Transform parent, GameplayPalette palette)
        {
            var shadowColor = Color.Lerp(palette.CameraBackground, Color.black, 0.24f);
            var focusColor = Color.Lerp(palette.CameraBackground, palette.Selection, 0.20f);
            var trimColor = Color.Lerp(palette.BoardTrim, palette.CameraBackground, 0.20f);

            CreateWorldCube(parent, "BoardFocusMat", new Vector3(0f, -0.22f, 0f), new Vector3(6.72f, 0.045f, 6.72f), trimColor);
            CreateWorldCube(parent, "BoardSoftHalo", new Vector3(0f, -0.185f, 0f), new Vector3(6.24f, 0.032f, 6.24f), focusColor);
            CreateWorldCube(parent, "BoardGroundShadow", new Vector3(0f, -0.245f, 0.08f), new Vector3(5.82f, 0.024f, 5.82f), shadowColor);
        }

        private static GameObject CreateWorldCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = localScale;

            var collider = cube.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            var renderer = cube.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                var material = CreateWorldMaterial(color);
                if (material != null)
                {
                    renderer.material = material;
                }
            }

            return cube;
        }

        private static Material CreateWorldMaterial(Color color)
        {
            var shader = GetSafeBoardShader();
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = "QuixoSceneGeneratedMaterial",
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.12f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.12f);
            }

            material.color = color;
            return material;
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

            var statusPanel = CreatePanel(canvas, "StatusPanel", new Vector2(620f, 88f), WithAlpha(palette.UiPanel, Mathf.Max(0.52f, palette.UiPanel.a * 0.86f)));
            SetAnchored(statusPanel.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(32f, -26f), new Vector2(620f, 88f));
            CreatePanelAccent(statusPanel.transform, palette);

            var turnLabel = CreateText(statusPanel.transform, "TurnLabel", "Tour: Joueur 1 (X)", 27f, TextAlignmentOptions.Left, palette.UiText,
                new Vector2(0f, 1f), new Vector2(24f, -12f), new Vector2(420f, 34f));
            AddTextShadow(turnLabel);

            var infoLabel = CreateText(statusPanel.transform, "InfoLabel", "Choisissez un cube du bord libre ou a vous.", 19f, TextAlignmentOptions.Left, palette.UiMuted,
                new Vector2(0f, 1f), new Vector2(24f, -50f), new Vector2(560f, 28f));
            AddTextShadow(infoLabel);

            var restartButton = CreateButton(canvas, "RestartButton", "Recommencer", palette.UiButtonSecondary,
                new Vector2(1f, 1f), new Vector2(-196f, -32f), new Vector2(182f, 54f), palette.UiText, palette.UiButtonDisabled);
            var menuButton = CreateButton(canvas, "MenuButton", "Menu", palette.UiButtonSecondary,
                new Vector2(1f, 1f), new Vector2(-42f, -32f), new Vector2(116f, 54f), palette.UiText, palette.UiButtonDisabled);

            var directionsPanel = CreatePanel(canvas, "DirectionsPanel", new Vector2(368f, 252f), WithAlpha(palette.UiPanel, 0.34f));
            SetAnchored(directionsPanel.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(-56f, 42f), new Vector2(368f, 252f));

            var upButton = CreateButton(directionsPanel.transform, "UpButton", "Haut", palette.UiButton,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 82f), new Vector2(152f, 66f), palette.UiText, palette.UiButtonDisabled);
            var downButton = CreateButton(directionsPanel.transform, "DownButton", "Bas", palette.UiButton,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -82f), new Vector2(152f, 66f), palette.UiText, palette.UiButtonDisabled);
            var leftButton = CreateButton(directionsPanel.transform, "LeftButton", "Gauche", palette.UiButton,
                new Vector2(0.5f, 0.5f), new Vector2(-116f, 0f), new Vector2(152f, 66f), palette.UiText, palette.UiButtonDisabled);
            var rightButton = CreateButton(directionsPanel.transform, "RightButton", "Droite", palette.UiButton,
                new Vector2(0.5f, 0.5f), new Vector2(116f, 0f), new Vector2(152f, 66f), palette.UiText, palette.UiButtonDisabled);

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
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.36f;
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

        private static void CreateMenuFloatingMarks(Transform parent, GameplayPalette palette)
        {
            // Couleurs douces et translucides : on veut un effet decoratif premium, pas
            // un truc qui distrait l'oeil par-dessus la carte du menu.
            Color xColor = WithAlpha(palette.Player1, 0.18f);
            Color oColor = WithAlpha(palette.Player2, 0.18f);

            // 6 X et 6 O eparpilles. Les noms commencent par MenuToken_ pour etre captures
            // par MenuVisualAnimator.
            float[] xPositions = { -760f, -540f, -260f, 220f, 540f, 780f };
            float[] yPositions = { 360f, -120f, 240f, -380f, 60f, -260f };
            for (int i = 0; i < xPositions.Length; i++)
            {
                bool isX = i % 2 == 0;
                string name = $"MenuToken_Mark{i:00}";
                var label = CreateText(parent, name, isX ? "X" : "O", 96f + (i % 3) * 18f,
                    TMPro.TextAlignmentOptions.Center,
                    isX ? xColor : oColor,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(xPositions[i], yPositions[i]),
                    new Vector2(140f, 140f));
                label.fontStyle = TMPro.FontStyles.Bold;
                label.raycastTarget = false;
            }

            // 8 etoiles supplementaires plus grosses pour densifier le fond, placees sur
            // les bords pour ne pas chevaucher la carte centrale.
            for (int i = 0; i < 8; i++)
            {
                float angle = (i / 8f) * Mathf.PI * 2f;
                float radius = 540f + (i % 2) * 80f;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * (radius * 0.55f);
                var star = new GameObject($"Star_Big{i:00}", typeof(RectTransform));
                star.transform.SetParent(parent, false);
                var image = star.AddComponent<Image>();
                image.color = WithAlpha(palette.UiText, 0.10f + (i % 4) * 0.025f);
                image.raycastTarget = false;
                SetAnchored(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(8f + (i % 3) * 4f, 8f + (i % 3) * 4f));
            }
        }

        private static void CreatePremiumBackdrop(Transform parent, GameplayPalette palette, string prefix)
        {
            CreateStars(parent, palette);

            CreateBackdropBand(parent, $"{prefix}SoftBandTop", WithAlpha(palette.Selection, 0.075f),
                new Vector2(0.5f, 0.5f), new Vector2(-340f, 230f), new Vector2(1620f, 92f), 11f);
            CreateBackdropBand(parent, $"{prefix}SoftBandBottom", WithAlpha(palette.Board, 0.11f),
                new Vector2(0.5f, 0.5f), new Vector2(420f, -320f), new Vector2(1420f, 84f), -9f);

            var x = CreateText(parent, $"{prefix}GhostX", "X", 240f, TextAlignmentOptions.Center, WithAlpha(palette.Player1, 0.08f),
                new Vector2(0f, 0.5f), new Vector2(90f, 40f), new Vector2(240f, 260f));
            x.fontStyle = FontStyles.Bold;

            var o = CreateText(parent, $"{prefix}GhostO", "O", 220f, TextAlignmentOptions.Center, WithAlpha(palette.Player2, 0.075f),
                new Vector2(1f, 0.5f), new Vector2(-108f, -62f), new Vector2(250f, 250f));
            o.fontStyle = FontStyles.Bold;

            for (int i = 0; i < 9; i++)
            {
                float xPos = -820f + i * 205f;
                float yPos = -430f + Mathf.Abs(Mathf.Sin(i * 1.7f)) * 170f;
                float size = 14f + (i % 3) * 6f;
                CreateBackdropBand(parent, $"{prefix}Token_{i:00}", WithAlpha(palette.CubeTop, 0.12f),
                    new Vector2(0.5f, 0.5f), new Vector2(xPos, yPos), new Vector2(size, size), i * 13f);
            }
        }

        private static void CreateBackdropBand(
            Transform parent,
            string name,
            Color color,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size,
            float rotation)
        {
            var band = new GameObject(name);
            band.transform.SetParent(parent, false);

            var image = band.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            var rect = band.GetComponent<RectTransform>();
            SetAnchored(rect, anchor, anchoredPosition, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private static void CreatePanelAccent(Transform parent, GameplayPalette palette)
        {
            var accent = new GameObject("AccentLine");
            accent.transform.SetParent(parent, false);

            var image = accent.AddComponent<Image>();
            image.color = WithAlpha(palette.Selection, 0.72f);
            image.raycastTarget = false;

            var rect = accent.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(34f, -12f);
            rect.offsetMax = new Vector2(-34f, -8f);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Color color)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            var image = panel.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            if (color.a > 0.03f)
            {
                var shadow = panel.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
                shadow.effectDistance = new Vector2(0f, -4f);

                var outline = panel.AddComponent<Outline>();
                outline.effectColor = WithAlpha(Color.Lerp(color, Color.white, 0.24f), 0.28f);
                outline.effectDistance = new Vector2(1f, 1f);
            }

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
            var shadow = inputObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.16f);
            shadow.effectDistance = new Vector2(0f, -2f);
            var outline = inputObject.AddComponent<Outline>();
            outline.effectColor = WithAlpha(Color.Lerp(palette.UiText, palette.UiPanel, 0.62f), 0.28f);
            outline.effectDistance = new Vector2(1f, 1f);

            var input = inputObject.AddComponent<TMP_InputField>();
            input.targetGraphic = image;
            input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = palette.UiText;
            input.selectionColor = new Color(palette.Selection.r, palette.Selection.g, palette.Selection.b, 0.35f);
            var colors = input.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = Color.Lerp(image.color, Color.white, 0.12f);
            colors.selectedColor = Color.Lerp(image.color, palette.Selection, 0.18f);
            colors.disabledColor = WithAlpha(image.color, 0.48f);
            colors.fadeDuration = 0.10f;
            input.colors = colors;

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

        // Cree un conteneur scrollable. Le RectTransform retourne porte le nom demande
        // et contient un VerticalLayoutGroup + ContentSizeFitter : FriendsView peut y
        // ajouter des rows sans craindre l'overflow visuel grace au Mask de la viewport.
        private static RectTransform CreateScrollableListContainer(
            Transform parent,
            string name,
            GameplayPalette palette,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var scrollRoot = new GameObject(name + "Scroll", typeof(RectTransform));
            scrollRoot.transform.SetParent(parent, false);
            var scrollRect = scrollRoot.GetComponent<RectTransform>();
            SetAnchored(scrollRect, anchor, anchoredPosition, size);

            var scrollBg = scrollRoot.AddComponent<Image>();
            scrollBg.color = WithAlpha(palette.UiPanel, 0.18f);
            scrollBg.raycastTarget = true;

            var scroll = scrollRoot.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 18f;
            scroll.inertia = false;

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(scrollRoot.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.pivot = new Vector2(0f, 1f);
            viewportRect.offsetMin = new Vector2(8f, 8f);
            viewportRect.offsetMax = new Vector2(-8f, -8f);
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = Color.white;
            viewportImage.raycastTarget = true;
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = new GameObject(name, typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = 8f;
            layout.padding = new RectOffset(2, 2, 2, 2);
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            return contentRect;
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
            shadow.effectColor = new Color(0f, 0f, 0f, 0.26f);
            shadow.effectDistance = new Vector2(0f, -3f);
            var outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = WithAlpha(Color.Lerp(normalColor, Color.white, 0.26f), 0.38f);
            outline.effectDistance = new Vector2(1f, 1f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.24f);
            colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.20f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = disabledColor ?? new Color(0.28f, 0.26f, 0.22f, 0.45f);
            colors.fadeDuration = 0.10f;
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
