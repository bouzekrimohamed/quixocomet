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
        private const string TeamLobbyHelpText =
            "COMMENT JOUER EN 2V2\n\n" +
            "1. Un joueur crée un salon.\n" +
            "2. Le code du salon s'affiche.\n" +
            "3. Les 3 autres joueurs entrent ce code.\n" +
            "4. Chaque joueur choisit son équipe :\n" +
            "   - Équipe 1 joue X.\n" +
            "   - Équipe 2 joue O.\n" +
            "5. Il faut 4 joueurs pour démarrer.\n" +
            "6. Ordre des tours :\n" +
            "   Équipe 1 J1 -> Équipe 2 J1 -> Équipe 1 J2 -> Équipe 2 J2.\n" +
            "7. Le point sur un cube indique quel coéquipier peut reprendre ce cube.";

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
        [SerializeField] private GameObject teamLobbyHelpPanel;
        [SerializeField] private TextMeshProUGUI teamLobbyHelpLabel;
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
            EnsureTeamLobbyLayout();
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
            SetStatus("Créez un salon 2v2 ou rejoignez un code existant.");
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
            string enteredCode = CurrentLobbyCode();
            if (_teamLobbySnapshot?.Lobby == null && !string.IsNullOrWhiteSpace(enteredCode))
            {
                LoadTeamLobbyByCode(enteredCode);
                return;
            }

            SetTeamLobbyBusy(true);
            SetStatus("Création du salon 2v2...");
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
            if (_teamLobbyBusy)
            {
                return;
            }

            if (_teamLobbySnapshot?.Lobby == null)
            {
                string enteredCode = CurrentLobbyCode();
                if (!string.IsNullOrWhiteSpace(enteredCode))
                {
                    LoadTeamLobbyByCode(enteredCode);
                    return;
                }

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
            SetStatus("Démarrage du match 2v2...");
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

            string code = _teamLobbySnapshot?.Lobby != null ? _teamLobbySnapshot.Lobby.lobby_code : CurrentLobbyCode();
            Debug.Log($"[2v2 Lobby] join team request team={OnlineSessionTransit.TeamName(team)}");
            SetTeamLobbyBusy(true);
            SetStatus($"Connexion au salon {TeamDisplayName(team)}...");
            onlineMatchService.JoinTeamLobby(code, team, HandleTeamLobbyResult);
        }

        private void LoadTeamLobbyByCode(string code)
        {
            if (_teamLobbyBusy || !RequireOnlineAccount())
            {
                return;
            }

            Debug.Log($"[2v2 Lobby] code entered={code}");
            SetTeamLobbyBusy(true);
            SetStatus("Chargement du salon...");
            onlineMatchService.FetchTeamLobbyByCode(code, HandleTeamLobbyResult);
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
            LogTeamLobbyState(result.Message);
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
                // Code tres visible : ligne dediee + mention "partagez ce code".
                teamLobbyCodeLabel.text = hasLobby
                    ? $"Code du salon : <b>{code}</b>\n<size=80%>Partagez ce code avec vos 3 coéquipiers.</size>"
                    : "Aucun salon actif.";
                teamLobbyCodeLabel.richText = true;
            }

            bool localInTeam1 = hasLobby
                && (_teamLobbySnapshot.GetPlayer(TeamId.Team1, 0)?.user_id == SessionManager.UserId
                    || _teamLobbySnapshot.GetPlayer(TeamId.Team1, 1)?.user_id == SessionManager.UserId);
            bool localInTeam2 = hasLobby
                && (_teamLobbySnapshot.GetPlayer(TeamId.Team2, 0)?.user_id == SessionManager.UserId
                    || _teamLobbySnapshot.GetPlayer(TeamId.Team2, 1)?.user_id == SessionManager.UserId);

            if (teamLobbyTeam1Label != null)
            {
                string suffix = localInTeam1 ? "  <- vous êtes ici" : string.Empty;
                teamLobbyTeam1Label.text = $"Équipe 1 (X) : {SlotLabel(TeamId.Team1, 0)} + {SlotLabel(TeamId.Team1, 1)}{suffix}";
            }

            if (teamLobbyTeam2Label != null)
            {
                string suffix = localInTeam2 ? "  <- vous êtes ici" : string.Empty;
                teamLobbyTeam2Label.text = $"Équipe 2 (O) : {SlotLabel(TeamId.Team2, 0)} + {SlotLabel(TeamId.Team2, 1)}{suffix}";
            }

            int playerCount = hasLobby ? _teamLobbySnapshot.Players.Count : 0;
            bool team1Full = hasLobby && _teamLobbySnapshot.IsTeamFull(TeamId.Team1);
            bool team2Full = hasLobby && _teamLobbySnapshot.IsTeamFull(TeamId.Team2);

            if (teamLobbyHintLabel != null)
            {
                string cadence = hasLobby
                    ? TurnTimerSettings.DisplayName(_teamLobbySnapshot.Lobby.time_control_key, _teamLobbySnapshot.Lobby.initial_seconds, _teamLobbySnapshot.Lobby.increment_seconds)
                    : TurnTimerSettings.DisplayCurrent();
                string localState = localInTeam1
                    ? "Vous êtes dans l'équipe 1 (X)."
                    : localInTeam2
                        ? "Vous êtes dans l'équipe 2 (O)."
                        : _teamLobbySnapshot != null && _teamLobbySnapshot.IsFull
                            ? "Salon complet."
                            : "Choisissez une équipe.";
                teamLobbyHintLabel.text = hasLobby
                    ? $"{localState}\nCadence : {cadence}. Joueurs présents : {playerCount}/4."
                    : $"Créez un salon ou entrez un code reçu.\nCadence : {cadence}.";
            }

            if (teamLobbyHelpLabel != null)
            {
                teamLobbyHelpLabel.text = TeamLobbyHelpText;
            }

            bool online = SessionManager.IsOnline;
            bool isLobbyOpen = hasLobby && string.Equals(_teamLobbySnapshot.Lobby.status, "lobby", System.StringComparison.OrdinalIgnoreCase);
            bool isHost = hasLobby && _teamLobbySnapshot.Lobby.host_user_id == SessionManager.UserId;
            bool hasLocal = hasLobby && _teamLobbySnapshot.HasUser(SessionManager.UserId);
            bool codeEntered = !string.IsNullOrWhiteSpace(CurrentLobbyCode());
            bool canLoadLobby = online && !_teamLobbyBusy && !hasLobby && codeEntered;
            bool canCreateLobby = online && !_teamLobbyBusy && !hasLobby && !codeEntered;
            bool canJoinTeam1 = online && !_teamLobbyBusy && hasLobby && isLobbyOpen && !hasLocal && !team1Full;
            bool canJoinTeam2 = online && !_teamLobbyBusy && hasLobby && isLobbyOpen && !hasLocal && !team2Full;

            SetInteractable(createTeamLobbyButton, canCreateLobby || canLoadLobby);
            // Bouton equipe : desactive si deja dans une equipe OU si cette equipe est pleine.
            SetInteractable(joinTeam1Button, canJoinTeam1);
            SetInteractable(joinTeam2Button, canJoinTeam2);
            SetInteractable(refreshTeamLobbyButton, online && !_teamLobbyBusy && hasLobby);
            SetInteractable(leaveTeamLobbyButton, online && !_teamLobbyBusy);
            SetInteractable(startTeamLobbyButton, online && !_teamLobbyBusy && isHost && isLobbyOpen && _teamLobbySnapshot != null && _teamLobbySnapshot.IsFull);

            // Labels dynamiques pour mieux guider le joueur.
            SetButtonLabel(createTeamLobbyButton, codeEntered && !hasLobby ? "Charger le salon" : "Créer un salon");
            SetButtonLabel(joinTeam1Button, team1Full && !localInTeam1 ? "Équipe 1 complète" : "Rejoindre équipe 1 (X)");
            SetButtonLabel(joinTeam2Button, team2Full && !localInTeam2 ? "Équipe 2 complète" : "Rejoindre équipe 2 (O)");
            SetButtonLabel(startTeamLobbyButton,
                _teamLobbySnapshot != null && _teamLobbySnapshot.IsFull
                    ? "Démarrer la partie"
                    : "En attente de 4 joueurs");
            SetButtonLabel(refreshTeamLobbyButton, "Rafraîchir");
            SetButtonLabel(leaveTeamLobbyButton, "Retour");
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

        private string CurrentLobbyCode()
        {
            return NormalizeLobbyCode(teamLobbyCodeInput != null ? teamLobbyCodeInput.text : string.Empty);
        }

        private static string NormalizeLobbyCode(string code)
        {
            return (code ?? string.Empty).Trim().Replace(" ", string.Empty).ToUpperInvariant();
        }

        private void LogTeamLobbyState(string resultMessage)
        {
            if (_teamLobbySnapshot?.Lobby == null)
            {
                return;
            }

            int team1Slots = _teamLobbySnapshot.CountTeam(TeamId.Team1);
            int team2Slots = _teamLobbySnapshot.CountTeam(TeamId.Team2);
            bool hasLocal = _teamLobbySnapshot.HasUser(SessionManager.UserId);
            bool isLobbyOpen = string.Equals(_teamLobbySnapshot.Lobby.status, "lobby", System.StringComparison.OrdinalIgnoreCase);
            bool canJoinTeam1 = SessionManager.IsOnline && !_teamLobbyBusy && isLobbyOpen && !hasLocal && team1Slots < 2;
            bool canJoinTeam2 = SessionManager.IsOnline && !_teamLobbyBusy && isLobbyOpen && !hasLocal && team2Slots < 2;
            string reason = !isLobbyOpen
                ? "salon non ouvert"
                : hasLocal
                    ? "utilisateur deja dans une equipe"
                    : _teamLobbySnapshot.IsFull
                        ? "salon complet"
                        : "places disponibles";

            Debug.Log($"[2v2 Lobby] loaded lobby={_teamLobbySnapshot.Lobby.id} team1Slots={team1Slots}/2 team2Slots={team2Slots}/2");
            Debug.Log($"[2v2 Lobby] canJoinTeam1={canJoinTeam1} canJoinTeam2={canJoinTeam2} reason={reason}");

            if (!string.IsNullOrWhiteSpace(resultMessage) && resultMessage.ToLowerInvariant().Contains("rejoint"))
            {
                TeamId joined = _teamLobbySnapshot.GetPlayer(TeamId.Team1, 0)?.user_id == SessionManager.UserId
                    || _teamLobbySnapshot.GetPlayer(TeamId.Team1, 1)?.user_id == SessionManager.UserId
                    ? TeamId.Team1
                    : _teamLobbySnapshot.GetPlayer(TeamId.Team2, 0)?.user_id == SessionManager.UserId
                        || _teamLobbySnapshot.GetPlayer(TeamId.Team2, 1)?.user_id == SessionManager.UserId
                        ? TeamId.Team2
                        : TeamId.None;
                Debug.Log($"[2v2 Lobby] joined team={OnlineSessionTransit.TeamName(joined)}");
            }
        }

        private static string TeamDisplayName(TeamId team)
        {
            return team == TeamId.Team1 ? "l'équipe 1" : team == TeamId.Team2 ? "l'équipe 2" : "une équipe";
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
            teamLobbyHelpPanel ??= FindChild<Transform>("TeamLobbyHelpPanel")?.gameObject;
            teamLobbyHelpLabel ??= FindChild<TextMeshProUGUI>("TeamLobbyHelpLabel");
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

        private void EnsureTeamLobbyLayout()
        {
            if (teamLobbyPanel == null)
            {
                return;
            }

            var mainRect = teamLobbyPanel.GetComponent<RectTransform>();
            if (mainRect != null)
            {
                SetAnchored(mainRect, new Vector2(0.5f, 1f), new Vector2(-285f, -220f), new Vector2(500f, 620f));
            }

            EnsurePanelSurface(teamLobbyPanel);
            ConfigureTeamLobbyInput();
            ConfigureTeamLobbyLabelRects();
            EnsureTeamLobbyHelpPanel();
        }

        private void ConfigureTeamLobbyInput()
        {
            if (teamLobbyCodeInput == null)
            {
                return;
            }

            if (teamLobbyCodeInput.placeholder is TextMeshProUGUI placeholderLabel)
            {
                placeholderLabel.text = "Entrer le code du salon";
                placeholderLabel.enableAutoSizing = true;
                placeholderLabel.fontSizeMin = 12f;
                placeholderLabel.fontSizeMax = 20f;
            }
        }

        private void ConfigureTeamLobbyLabelRects()
        {
            ConfigureLabel(teamLobbyCodeLabel, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(440f, 54f), 18f, 22f, TextAlignmentOptions.Center);
            ConfigureLabel(teamLobbyTeam1Label, new Vector2(0f, 1f), new Vector2(34f, -180f), new Vector2(430f, 34f), 14f, 19f, TextAlignmentOptions.Left);
            ConfigureLabel(teamLobbyTeam2Label, new Vector2(0f, 1f), new Vector2(34f, -224f), new Vector2(430f, 34f), 14f, 19f, TextAlignmentOptions.Left);
            ConfigureLabel(teamLobbyHintLabel, new Vector2(0.5f, 1f), new Vector2(0f, -532f), new Vector2(440f, 74f), 12f, 15f, TextAlignmentOptions.Center);
        }

        private static void ConfigureLabel(TextMeshProUGUI label, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, float minSize, float maxSize, TextAlignmentOptions alignment)
        {
            if (label == null)
            {
                return;
            }

            SetAnchored(label.rectTransform, anchor, anchoredPosition, size);
            label.alignment = alignment;
            label.enableWordWrapping = true;
            label.enableAutoSizing = true;
            label.fontSizeMin = minSize;
            label.fontSizeMax = maxSize;
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void EnsureTeamLobbyHelpPanel()
        {
            Transform parent = teamLobbyPanel.transform.parent;
            if (parent == null)
            {
                return;
            }

            if (teamLobbyHelpPanel == null)
            {
                teamLobbyHelpPanel = new GameObject("TeamLobbyHelpPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                teamLobbyHelpPanel.transform.SetParent(parent, false);
            }
            else if (teamLobbyHelpPanel.transform.parent != parent)
            {
                teamLobbyHelpPanel.transform.SetParent(parent, false);
            }

            var helpRect = teamLobbyHelpPanel.GetComponent<RectTransform>();
            if (helpRect != null)
            {
                SetAnchored(helpRect, new Vector2(0.5f, 1f), new Vector2(285f, -220f), new Vector2(500f, 620f));
            }

            EnsurePanelSurface(teamLobbyHelpPanel);

            if (teamLobbyHelpLabel == null)
            {
                var textObject = new GameObject("TeamLobbyHelpLabel", typeof(RectTransform));
                textObject.transform.SetParent(teamLobbyHelpPanel.transform, false);
                teamLobbyHelpLabel = textObject.AddComponent<TextMeshProUGUI>();
            }

            teamLobbyHelpLabel.text = TeamLobbyHelpText;
            teamLobbyHelpLabel.alignment = TextAlignmentOptions.TopLeft;
            teamLobbyHelpLabel.enableWordWrapping = true;
            teamLobbyHelpLabel.enableAutoSizing = true;
            teamLobbyHelpLabel.fontSizeMin = 12f;
            teamLobbyHelpLabel.fontSizeMax = 17f;
            teamLobbyHelpLabel.overflowMode = TextOverflowModes.Ellipsis;
            teamLobbyHelpLabel.raycastTarget = false;
            Stretch(teamLobbyHelpLabel.rectTransform, new Vector2(34f, 28f), new Vector2(-34f, -28f));
            teamLobbyHelpPanel.SetActive(teamLobbyPanel.activeSelf);
        }

        private static void EnsurePanelSurface(GameObject panel)
        {
            if (panel == null)
            {
                return;
            }

            var image = panel.GetComponent<Image>();
            if (image == null)
            {
                image = panel.AddComponent<Image>();
            }

            image.raycastTarget = false;

            if (panel.GetComponent<Shadow>() == null)
            {
                var shadow = panel.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.24f);
                shadow.effectDistance = new Vector2(0f, -4f);
            }

            if (panel.GetComponent<Outline>() == null)
            {
                var outline = panel.AddComponent<Outline>();
                outline.effectDistance = new Vector2(1f, 1f);
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
            if (teamLobbyCodeInput != null)
            {
                teamLobbyCodeInput.onValueChanged.RemoveListener(HandleTeamLobbyCodeChanged);
                teamLobbyCodeInput.onValueChanged.AddListener(HandleTeamLobbyCodeChanged);
            }
        }

        private void HandleTeamLobbyCodeChanged(string value)
        {
            string entered = NormalizeLobbyCode(value);
            string loaded = NormalizeLobbyCode(_teamLobbySnapshot?.Lobby?.lobby_code);
            if (_teamLobbySnapshot?.Lobby != null && !string.IsNullOrWhiteSpace(entered) && entered != loaded)
            {
                StopTeamLobbyPolling();
                _teamLobbySnapshot = null;
            }

            RenderTeamLobby();
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
            ApplyTeamLobbyPanelTheme(palette);
            RefreshThemeLabel();
            RefreshTimerSelection();
            SetOnlineSearching(_searchingOnline);
        }

        private void ApplyTeamLobbyPanelTheme(GameplayPalette palette)
        {
            ApplyPanelTheme(teamLobbyPanel, palette);
            ApplyPanelTheme(teamLobbyHelpPanel, palette);

            if (teamLobbyHelpLabel != null)
            {
                teamLobbyHelpLabel.color = palette.UiText;
            }

            if (teamLobbyCodeLabel != null)
            {
                teamLobbyCodeLabel.color = palette.UiText;
            }

            if (teamLobbyHintLabel != null)
            {
                teamLobbyHintLabel.color = palette.UiMuted;
            }
        }

        private static void ApplyPanelTheme(GameObject panel, GameplayPalette palette)
        {
            if (panel == null)
            {
                return;
            }

            var image = panel.GetComponent<Image>();
            if (image != null)
            {
                image.color = palette.MenuPanel;
            }

            var outline = panel.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(palette.UiText.r, palette.UiText.g, palette.UiText.b, 0.18f);
            }
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
            if (teamLobbyHelpPanel != null) teamLobbyHelpPanel.SetActive(teamLobby);
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

        private static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
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
