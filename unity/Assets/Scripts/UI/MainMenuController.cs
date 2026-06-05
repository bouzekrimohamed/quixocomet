using System.Collections;
using QuixoUnity.Auth;
using QuixoUnity.Core;
using QuixoUnity.Gameplay;
using QuixoUnity.Online;
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
        [SerializeField] private Button playButton;
        [SerializeField] private Button localButton;
        [SerializeField] private Button onlineButton;
        [SerializeField] private Button modeBackButton;
        [SerializeField] private Button gameBackButton;
        [SerializeField] private Button quixoOnlineButton;
        [SerializeField] private Button qometOnlineButton;
        [SerializeField] private Button quixoTeamOnlineButton;
        [SerializeField] private Button cancelOnlineButton;
        [SerializeField] private Button friendsButton;
        [SerializeField] private Button themeButton;
        [SerializeField] private Button logoutButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button timerUnlimitedButton;
        [SerializeField] private Button timer15Button;
        [SerializeField] private Button timer30Button;
        [SerializeField] private Button timer60Button;
        [SerializeField] private TextMeshProUGUI timerSummaryLabel;
        [SerializeField] private TMP_InputField teamLobbyCodeInput;
        [SerializeField] private TextMeshProUGUI teamLobbyCodeLabel;
        [SerializeField] private TextMeshProUGUI teamLobbyTeam1Label;
        [SerializeField] private TextMeshProUGUI teamLobbyTeam2Label;
        [SerializeField] private TextMeshProUGUI teamLobbyHintLabel;
        [SerializeField] private Button createTeamLobbyButton;
        [SerializeField] private Button joinTeam1Button;
        [SerializeField] private Button joinTeam2Button;
        [SerializeField] private Button startTeamLobbyButton;
        [SerializeField] private Button refreshTeamLobbyButton;
        [SerializeField] private Button leaveTeamLobbyButton;
        [SerializeField] private GameObject friendsPanel;
        [SerializeField] private GameObject mainActionsPanel;
        [SerializeField] private GameObject modePanel;
        [SerializeField] private GameObject gamePanel;
        [SerializeField] private GameObject teamLobbyPanel;
        [SerializeField] private FriendsView friendsView;
        [SerializeField] private AuthService authService;
        [SerializeField] private FriendService friendService;
        [SerializeField] private OnlineMatchService onlineMatchService;
        [SerializeField] private OnlinePresenceService onlinePresenceService;

        private TextMeshProUGUI _themeButtonLabel;
        private bool _loadingGameplay;
        private bool _searchingOnline;
        private GameKind _searchingKind;
        private bool _teamLobbyBusy;
        private TeamLobbySnapshot _teamLobbySnapshot;
        private Coroutine _teamLobbyPollRoutine;

        private void Awake()
        {
            // Entrer dans le menu ne doit jamais reprendre une partie transitoire.
            OnlineSessionTransit.Clear();
            SceneTransit.SelectedTheme = VisualThemeCatalog.ActiveTheme;
            ResolveReferences();
            BindButtons();
            ApplyTheme();
            RefreshSessionDisplay();
            RefreshOnlineAvailability();
            RefreshTimerSelection();
            if (friendsPanel != null)
            {
                friendsPanel.SetActive(false);
            }
            ShowMainActions();
        }

        private void Start()
        {
            RefreshOnlineAvailability();
            if (SessionManager.IsOnline && authService != null)
            {
                onlinePresenceService?.StartPresence();
                authService.FetchCurrentProfile(result =>
                {
                    if (result != null && result.Success)
                    {
                        RefreshSessionDisplay();
                    }
                });
            }
            else if (SessionManager.IsOffline)
            {
                SetStatus("Mode invite : jeu en ligne indisponible. Connectez-vous pour jouer en ligne.");
            }
        }

        private void OnDestroy()
        {
            onlinePresenceService?.StopPresence();
            StopTeamLobbyPolling();
        }

        public void StartQuixo()
        {
            StartGame(GameKind.Quixo);
        }

        public void StartQomet()
        {
            StartGame(GameKind.Qomet);
        }

        public void StartOnlineQuixo()
        {
            if (!RequireOnlineAccount())
            {
                return;
            }

            StartOnlineGame(GameKind.Quixo);
        }

        public void StartOnlineQomet()
        {
            if (!RequireOnlineAccount())
            {
                return;
            }

            StartOnlineGame(GameKind.Qomet);
        }

        public void StartOnlineQuixoTeam()
        {
            if (!RequireOnlineAccount())
            {
                return;
            }

            ShowTeamLobbyChoice();
        }

        public void ShowModeChoice()
        {
            SetPanelState(false, true, false);
        }

        public void ShowLocalChoice()
        {
            SetPanelState(false, false, true);
            SetActive(quixoButton, true);
            SetActive(qometButton, true);
            SetActive(quixoOnlineButton, false);
            SetActive(qometOnlineButton, false);
            SetActive(quixoTeamOnlineButton, false);
            SetInteractable(quixoButton, true);
            SetInteractable(qometButton, true);
        }

        public void ShowOnlineChoice()
        {
            if (!RequireOnlineAccount())
            {
                return;
            }

            SetPanelState(false, false, true);
            SetActive(quixoButton, false);
            SetActive(qometButton, false);
            SetActive(quixoOnlineButton, true);
            SetActive(qometOnlineButton, true);
            SetActive(quixoTeamOnlineButton, true);
            SetInteractable(quixoOnlineButton, true);
            SetInteractable(qometOnlineButton, true);
            SetInteractable(quixoTeamOnlineButton, true);
        }

        public void ShowMainActions()
        {
            SetPanelState(true, false, false);
        }

        public void ShowTeamLobbyChoice()
        {
            SetPanelState(false, false, false, true);
            RenderTeamLobby();
            SetStatus("Creez un salon 2v2 ou rejoignez un code existant.");
        }

        private bool RequireOnlineAccount()
        {
            if (SessionManager.IsOnline)
            {
                return true;
            }

            SetStatus("Connectez-vous pour jouer en ligne.");
            RefreshOnlineAvailability();
            return false;
        }

        private void RefreshOnlineAvailability()
        {
            bool online = SessionManager.IsOnline;
            SetInteractable(quixoOnlineButton, online && !_searchingOnline);
            SetInteractable(qometOnlineButton, online && !_searchingOnline);
            SetInteractable(quixoTeamOnlineButton, online && !_searchingOnline);
            SetInteractable(friendsButton, online && !_searchingOnline);
            if (!online && SessionManager.IsOffline && statusLabel != null && string.IsNullOrEmpty(statusLabel.text))
            {
                SetStatus("Connectez-vous pour jouer en ligne.");
            }
        }

        public void CancelOnlineSearch()
        {
            if (!_searchingOnline || onlineMatchService == null)
            {
                SetOnlineSearching(false);
                return;
            }

            onlineMatchService.CancelMatchmaking(_searchingKind, result =>
            {
                SetOnlineSearching(false);
                SetStatus(result != null ? result.Message : "Recherche annulee.");
            });
        }

        public void ToggleFriends()
        {
            if (friendsPanel == null)
            {
                SetStatus("Panneau amis introuvable.");
                return;
            }

            if (!SessionManager.IsOnline)
            {
                SetStatus("Connectez-vous pour utiliser les amis et le jeu en ligne.");
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
            onlinePresenceService?.StopPresence();
            OnlineSessionTransit.Clear();
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

        public void SelectTimerUnlimited()
        {
            SelectTimer("none");
        }

        public void SelectTimer15()
        {
            SelectTimer("1+0");
        }

        public void SelectTimer30()
        {
            SelectTimer("5+3");
        }

        public void SelectTimer60()
        {
            SelectTimer("10+5");
        }

        public void CreateTeamLobby()
        {
            if (_teamLobbyBusy || !RequireOnlineAccount())
            {
                return;
            }

            ResolveReferences();
            SetTeamLobbyBusy(true);
            SetStatus("Creation du salon 2v2...");
            onlineMatchService.CreateTeamLobby(HandleTeamLobbyResult);
        }

        public void JoinTeam1Lobby()
        {
            JoinTeamLobby(TeamId.Team1);
        }

        public void JoinTeam2Lobby()
        {
            JoinTeamLobby(TeamId.Team2);
        }

        public void RefreshTeamLobby()
        {
            if (_teamLobbyBusy || _teamLobbySnapshot?.Lobby == null)
            {
                RenderTeamLobby();
                return;
            }

            SetTeamLobbyBusy(true);
            onlineMatchService.FetchTeamLobby(_teamLobbySnapshot.Lobby.id, HandleTeamLobbyResult);
        }

        public void StartTeamLobby()
        {
            if (_teamLobbyBusy || _teamLobbySnapshot?.Lobby == null)
            {
                return;
            }

            SetTeamLobbyBusy(true);
            SetStatus("Demarrage du match 2v2...");
            onlineMatchService.StartTeamLobby(_teamLobbySnapshot.Lobby.id, HandleTeamLobbyResult);
        }

        public void LeaveTeamLobby()
        {
            if (_teamLobbyBusy)
            {
                return;
            }

            string lobbyId = _teamLobbySnapshot?.Lobby?.id;
            StopTeamLobbyPolling();
            _teamLobbySnapshot = null;
            RenderTeamLobby();
            if (string.IsNullOrWhiteSpace(lobbyId))
            {
                ShowOnlineChoice();
                return;
            }

            SetTeamLobbyBusy(true);
            onlineMatchService.LeaveTeamLobby(lobbyId, result =>
            {
                SetTeamLobbyBusy(false);
                SetStatus(result != null ? result.Message : "Salon quitte.");
                ShowOnlineChoice();
            });
        }

        private void SelectTimer(string key)
        {
            TurnTimerSettings.SelectedKey = key;
            RefreshTimerSelection();
        }

        private void JoinTeamLobby(TeamId team)
        {
            if (_teamLobbyBusy || !RequireOnlineAccount())
            {
                return;
            }

            string code = teamLobbyCodeInput != null ? teamLobbyCodeInput.text : string.Empty;
            SetTeamLobbyBusy(true);
            SetStatus($"Connexion au salon {TeamDisplayName(team)}...");
            onlineMatchService.JoinTeamLobby(code, team, HandleTeamLobbyResult);
        }

        private void HandleTeamLobbyResult(TeamLobbyOperationResult result)
        {
            SetTeamLobbyBusy(false);
            if (result == null)
            {
                SetStatus("Operation salon impossible.");
                return;
            }

            SetStatus(result.Message);
            if (!result.Success)
            {
                RenderTeamLobby();
                return;
            }

            _teamLobbySnapshot = result.Snapshot;
            RenderTeamLobby();
            if (_teamLobbySnapshot?.Lobby != null)
            {
                StartTeamLobbyPolling();
            }

            TryLaunchStartedTeamLobby();
        }

        private void TryLaunchStartedTeamLobby()
        {
            if (_teamLobbySnapshot == null || !_teamLobbySnapshot.IsStarted || _teamLobbySnapshot.Match == null)
            {
                return;
            }

            if (!OnlineSessionTransit.IsValidForLocalPlayer(_teamLobbySnapshot.Match, SessionManager.UserId))
            {
                SetStatus("Match 2v2 inaccessible pour ce compte.");
                return;
            }

            StopTeamLobbyPolling();
            OnlineSessionTransit.StartTeam(_teamLobbySnapshot.Match, SessionManager.UserId, _teamLobbySnapshot);
            SceneTransit.SelectedGame = GameKind.Quixo;
            SceneTransit.SelectedTheme = VisualThemeCatalog.ActiveTheme;
            LoadGameplay();
        }

        private void StartTeamLobbyPolling()
        {
            if (_teamLobbyPollRoutine != null || _teamLobbySnapshot?.Lobby == null)
            {
                return;
            }

            _teamLobbyPollRoutine = StartCoroutine(TeamLobbyPollRoutine());
        }

        private void StopTeamLobbyPolling()
        {
            if (_teamLobbyPollRoutine != null)
            {
                StopCoroutine(_teamLobbyPollRoutine);
                _teamLobbyPollRoutine = null;
            }
        }

        private IEnumerator TeamLobbyPollRoutine()
        {
            while (_teamLobbySnapshot?.Lobby != null && !_loadingGameplay)
            {
                yield return new WaitForSeconds(2f);
                if (_teamLobbyBusy || _teamLobbySnapshot?.Lobby == null)
                {
                    continue;
                }

                bool done = false;
                onlineMatchService.FetchTeamLobby(_teamLobbySnapshot.Lobby.id, result =>
                {
                    done = true;
                    if (result != null && result.Success)
                    {
                        _teamLobbySnapshot = result.Snapshot;
                        RenderTeamLobby();
                        TryLaunchStartedTeamLobby();
                    }
                });
                yield return new WaitUntil(() => done);
            }

            _teamLobbyPollRoutine = null;
        }

        private void RenderTeamLobby()
        {
            bool hasLobby = _teamLobbySnapshot?.Lobby != null;
            string code = hasLobby ? _teamLobbySnapshot.Lobby.lobby_code : "aucun";
            if (teamLobbyCodeLabel != null)
            {
                teamLobbyCodeLabel.text = $"Code salon : {code}";
            }

            if (teamLobbyTeam1Label != null)
            {
                teamLobbyTeam1Label.text = $"Equipe 1 (X) : {SlotLabel(TeamId.Team1, 0)} + {SlotLabel(TeamId.Team1, 1)}";
            }

            if (teamLobbyTeam2Label != null)
            {
                teamLobbyTeam2Label.text = $"Equipe 2 (O) : {SlotLabel(TeamId.Team2, 0)} + {SlotLabel(TeamId.Team2, 1)}";
            }

            if (teamLobbyHintLabel != null)
            {
                string cadence = hasLobby
                    ? TurnTimerSettings.DisplayName(_teamLobbySnapshot.Lobby.time_control_key, _teamLobbySnapshot.Lobby.initial_seconds, _teamLobbySnapshot.Lobby.increment_seconds)
                    : TurnTimerSettings.DisplayCurrent();
                teamLobbyHintLabel.text = hasLobby
                    ? $"Cadence : {cadence}. Ordre : Equipe 1 joueur 1 -> Equipe 2 joueur 1 -> Equipe 1 joueur 2 -> Equipe 2 joueur 2."
                    : $"Cadence : {cadence}. Creez un salon, partagez le code, puis les joueurs rejoignent l'equipe 1 ou l'equipe 2.";
            }

            bool online = SessionManager.IsOnline;
            bool isLobbyOpen = hasLobby && string.Equals(_teamLobbySnapshot.Lobby.status, "lobby", System.StringComparison.OrdinalIgnoreCase);
            bool isHost = hasLobby && _teamLobbySnapshot.Lobby.host_user_id == SessionManager.UserId;
            bool hasLocal = hasLobby && _teamLobbySnapshot.HasUser(SessionManager.UserId);
            SetInteractable(createTeamLobbyButton, online && !_teamLobbyBusy && !hasLobby);
            SetInteractable(joinTeam1Button, online && !_teamLobbyBusy && !hasLocal);
            SetInteractable(joinTeam2Button, online && !_teamLobbyBusy && !hasLocal);
            SetInteractable(refreshTeamLobbyButton, online && !_teamLobbyBusy && hasLobby);
            SetInteractable(leaveTeamLobbyButton, online && !_teamLobbyBusy);
            SetInteractable(startTeamLobbyButton, online && !_teamLobbyBusy && isHost && isLobbyOpen && _teamLobbySnapshot.IsFull);
        }

        private string SlotLabel(TeamId team, int slotIndex)
        {
            var player = _teamLobbySnapshot?.GetPlayer(team, slotIndex);
            if (player == null)
            {
                return "libre";
            }

            return string.IsNullOrWhiteSpace(player.username) ? "joueur" : player.username;
        }

        private static string TeamDisplayName(TeamId team)
        {
            return team == TeamId.Team1 ? "l'equipe 1" : team == TeamId.Team2 ? "l'equipe 2" : "une equipe";
        }

        private void RefreshTimerSelection()
        {
            string current = TurnTimerSettings.SelectedKey;
            SetButtonLabel(timerUnlimitedButton, "Sans limite");
            SetButtonLabel(timer15Button, "1+0");
            SetButtonLabel(timer30Button, "5+3");
            SetButtonLabel(timer60Button, "10+5");
            HighlightTimerButton(timerUnlimitedButton, current == "none");
            HighlightTimerButton(timer15Button, current == "1+0");
            HighlightTimerButton(timer30Button, current == "5+3");
            HighlightTimerButton(timer60Button, current == "10+5");
            if (timerSummaryLabel != null)
            {
                timerSummaryLabel.text = $"Cadence : {TurnTimerSettings.DisplayCurrent()}";
            }
        }

        private void HighlightTimerButton(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            var palette = VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme);
            // Le bouton selectionne prend la couleur principale, les autres restent en secondaire.
            ApplyButton(
                button,
                selected ? palette.UiButton : palette.UiButtonSecondary,
                palette.UiButtonText,
                palette.UiButtonDisabled);
        }

        private void StartGame(GameKind kind)
        {
            if (_loadingGameplay)
            {
                return;
            }

            OnlineSessionTransit.Clear();
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

        private void StartOnlineGame(GameKind kind)
        {
            if (_loadingGameplay || _searchingOnline)
            {
                return;
            }

            if (!SessionManager.IsOnline)
            {
                SetStatus("Connectez-vous pour jouer en ligne.");
                return;
            }

            onlineMatchService ??= FindObjectOfType<OnlineMatchService>();
            if (onlineMatchService == null)
            {
                onlineMatchService = gameObject.AddComponent<OnlineMatchService>();
            }

            _searchingKind = kind;
            SetOnlineSearching(true);
            SetStatus($"Recherche {OnlineSessionTransit.GameKindName(kind)} - cadence {TurnTimerSettings.DisplayCurrent()}...");
            onlineMatchService.StartMatchmaking(kind, result =>
            {
                if (result == null || !result.Success || result.Match == null)
                {
                    SetOnlineSearching(false);
                    SetStatus(result != null ? result.Message : "Recherche de partie impossible.");
                    return;
                }

                if (!OnlineSessionTransit.IsValidForLocalPlayer(result.Match, SessionManager.UserId))
                {
                    SetOnlineSearching(false);
                    SetStatus("Match invalide ou inaccessible.");
                    return;
                }

                OnlineSessionTransit.Start(result.Match, SessionManager.UserId);
                SceneTransit.SelectedGame = OnlineSessionTransit.SelectedGameKind;
                SceneTransit.SelectedTheme = VisualThemeCatalog.ActiveTheme;
                LoadGameplay();
            }, SetStatus);
        }

        private void LoadGameplay()
        {
            if (Application.CanStreamedLevelBeLoaded(GameplaySceneName))
            {
                _loadingGameplay = true;
                SceneManager.LoadScene(GameplaySceneName);
                return;
            }

            SetOnlineSearching(false);
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

            onlineMatchService ??= FindObjectOfType<OnlineMatchService>();
            if (onlineMatchService == null)
            {
                onlineMatchService = gameObject.AddComponent<OnlineMatchService>();
            }

            onlinePresenceService ??= FindObjectOfType<OnlinePresenceService>();
            if (onlinePresenceService == null)
            {
                onlinePresenceService = gameObject.AddComponent<OnlinePresenceService>();
            }

            connectedLabel ??= FindChild<TextMeshProUGUI>("ConnectedLabel");
            statusLabel ??= FindChild<TextMeshProUGUI>("MenuStatusLabel");
            quixoButton ??= FindChild<Button>("QuixoButton");
            qometButton ??= FindChild<Button>("QometButton");
            playButton ??= FindChild<Button>("PlayButton");
            localButton ??= FindChild<Button>("LocalButton");
            onlineButton ??= FindChild<Button>("OnlineButton");
            modeBackButton ??= FindChild<Button>("ModeBackButton");
            gameBackButton ??= FindChild<Button>("GameBackButton");
            quixoOnlineButton ??= FindChild<Button>("QuixoOnlineButton");
            qometOnlineButton ??= FindChild<Button>("QometOnlineButton");
            quixoTeamOnlineButton ??= FindChild<Button>("QuixoTeamOnlineButton");
            cancelOnlineButton ??= FindChild<Button>("CancelOnlineButton");
            friendsButton ??= FindChild<Button>("FriendsButton");
            themeButton ??= FindChild<Button>("ThemeButton");
            logoutButton ??= FindChild<Button>("LogoutButton");
            quitButton ??= FindChild<Button>("QuitButton");
            timerUnlimitedButton ??= FindChild<Button>("TimerUnlimitedButton");
            timer15Button ??= FindChild<Button>("Timer15Button");
            timer30Button ??= FindChild<Button>("Timer30Button");
            timer60Button ??= FindChild<Button>("Timer60Button");
            timerSummaryLabel ??= FindChild<TextMeshProUGUI>("TimerSummaryLabel");
            teamLobbyCodeInput ??= FindChild<TMP_InputField>("TeamLobbyCodeInput");
            teamLobbyCodeLabel ??= FindChild<TextMeshProUGUI>("TeamLobbyCodeLabel");
            teamLobbyTeam1Label ??= FindChild<TextMeshProUGUI>("TeamLobbyTeam1Label");
            teamLobbyTeam2Label ??= FindChild<TextMeshProUGUI>("TeamLobbyTeam2Label");
            teamLobbyHintLabel ??= FindChild<TextMeshProUGUI>("TeamLobbyHintLabel");
            createTeamLobbyButton ??= FindChild<Button>("CreateTeamLobbyButton");
            joinTeam1Button ??= FindChild<Button>("JoinTeam1Button");
            joinTeam2Button ??= FindChild<Button>("JoinTeam2Button");
            startTeamLobbyButton ??= FindChild<Button>("StartTeamLobbyButton");
            refreshTeamLobbyButton ??= FindChild<Button>("RefreshTeamLobbyButton");
            leaveTeamLobbyButton ??= FindChild<Button>("LeaveTeamLobbyButton");
            friendsView ??= FindObjectOfType<FriendsView>(true);
            mainActionsPanel ??= FindChild<Transform>("MainActionsPanel")?.gameObject;
            modePanel ??= FindChild<Transform>("ModePanel")?.gameObject;
            gamePanel ??= FindChild<Transform>("GamePanel")?.gameObject;
            teamLobbyPanel ??= FindChild<Transform>("TeamLobbyPanel")?.gameObject;

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
            Bind(playButton, ShowModeChoice);
            Bind(localButton, ShowLocalChoice);
            Bind(onlineButton, ShowOnlineChoice);
            Bind(modeBackButton, ShowMainActions);
            Bind(gameBackButton, ShowModeChoice);
            Bind(quixoOnlineButton, StartOnlineQuixo);
            Bind(qometOnlineButton, StartOnlineQomet);
            Bind(quixoTeamOnlineButton, StartOnlineQuixoTeam);
            Bind(cancelOnlineButton, CancelOnlineSearch);
            Bind(friendsButton, ToggleFriends);
            Bind(themeButton, CycleTheme);
            Bind(logoutButton, Logout);
            Bind(quitButton, Quit);
            Bind(timerUnlimitedButton, SelectTimerUnlimited);
            Bind(timer15Button, SelectTimer15);
            Bind(timer30Button, SelectTimer30);
            Bind(timer60Button, SelectTimer60);
            Bind(createTeamLobbyButton, CreateTeamLobby);
            Bind(joinTeam1Button, JoinTeam1Lobby);
            Bind(joinTeam2Button, JoinTeam2Lobby);
            Bind(startTeamLobbyButton, StartTeamLobby);
            Bind(refreshTeamLobbyButton, RefreshTeamLobby);
            Bind(leaveTeamLobbyButton, LeaveTeamLobby);
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

            VisualThemeCatalog.ApplySceneTextPalette(palette);

            ApplyButton(quixoButton, palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(qometButton, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(playButton, palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(localButton, palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(onlineButton, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(modeBackButton, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(gameBackButton, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(quixoOnlineButton, palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(qometOnlineButton, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(quixoTeamOnlineButton, palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(cancelOnlineButton, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(friendsButton, palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(themeButton, palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(logoutButton, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(quitButton, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(timerUnlimitedButton, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(timer15Button, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(timer30Button, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(timer60Button, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(createTeamLobbyButton, palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(joinTeam1Button, palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(joinTeam2Button, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(startTeamLobbyButton, palette.UiButton, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(refreshTeamLobbyButton, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            ApplyButton(leaveTeamLobbyButton, palette.UiButtonSecondary, palette.UiButtonText, palette.UiButtonDisabled);
            RefreshThemeLabel();
            RefreshTimerSelection();
            SetOnlineSearching(_searchingOnline);
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

        private void SetOnlineSearching(bool searching)
        {
            _searchingOnline = searching;
            bool online = SessionManager.IsOnline;
            SetInteractable(quixoButton, !searching);
            SetInteractable(qometButton, !searching);
            SetInteractable(quixoOnlineButton, !searching && online);
            SetInteractable(qometOnlineButton, !searching && online);
            SetInteractable(quixoTeamOnlineButton, !searching && online);
            SetInteractable(friendsButton, !searching && online);
            if (friendsButton != null)
            {
                friendsButton.gameObject.SetActive(!searching);
            }

            SetInteractable(themeButton, !searching);
            SetInteractable(logoutButton, !searching);
            SetInteractable(timerUnlimitedButton, !searching);
            SetInteractable(timer15Button, !searching);
            SetInteractable(timer30Button, !searching);
            SetInteractable(timer60Button, !searching);
            if (cancelOnlineButton != null)
            {
                cancelOnlineButton.gameObject.SetActive(searching);
                cancelOnlineButton.interactable = searching;
            }
        }

        private void SetTeamLobbyBusy(bool busy)
        {
            _teamLobbyBusy = busy;
            RenderTeamLobby();
        }

        private void SetPanelState(bool main, bool mode, bool game, bool teamLobby = false)
        {
            if (mainActionsPanel != null) mainActionsPanel.SetActive(main);
            if (modePanel != null) modePanel.SetActive(mode);
            if (gamePanel != null) gamePanel.SetActive(game);
            if (teamLobbyPanel != null) teamLobbyPanel.SetActive(teamLobby);
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
                Color background = button.interactable ? normalColor : disabledColor;
                label.color = VisualThemeCatalog.GetButtonTextColor(background, VisualThemeCatalog.Get(VisualThemeCatalog.ActiveTheme));
            }
        }

        private static void SetButtonLabel(Button button, string text)
        {
            if (button == null)
            {
                return;
            }

            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = text;
            }
        }

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private static void SetActive(Button button, bool active)
        {
            if (button != null)
            {
                button.gameObject.SetActive(active);
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
