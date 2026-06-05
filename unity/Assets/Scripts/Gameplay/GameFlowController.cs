using System;
using System.Collections;
using System.Collections.Generic;
using QuixoUnity.Auth;
using QuixoUnity.Core;
using QuixoUnity.Online;
using QuixoUnity.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuixoUnity.Gameplay
{
    public sealed class GameFlowController : MonoBehaviour
    {
        private const string MenuSceneName = "MenuScene";
        private const float OnlinePollSeconds = 1f;

        [SerializeField] private GameKind gameKind = GameKind.Quixo;
        [SerializeField] private BoardViewRenderer boardView = null!;
        [SerializeField] private HudView hudView = null!;
        [SerializeField] private OnlineMatchService onlineMatchService = null!;
        [SerializeField] private OnlinePresenceService onlinePresenceService = null!;

        private IGameRules _rules = null!;
        private BoardState _state = null!;
        private Vector2Int? _selected;
        private bool _ready;
        private bool _loadingMenu;
        private bool _applyingOnlineMove;
        private bool _onlineSubmitting;
        private int _lastAppliedMoveNumber;
        private OnlineMatchDto _onlineMatch;
        private Coroutine _onlinePollRoutine;
        private int _turnTimeSecondsForSession;
        private bool _timerEventBound;
        private bool _gameOverShown;
        // Vrai si une perte par inactivite vient d'etre declenchee (local ou online).
        // Permet de styliser la popup de fin de partie.
        private bool _lastLossWasTimeout;
        // Dernier turn_id online observe : on relance le timer uniquement si le tour a change,
        // sinon chaque poll (toutes les 1s) reinitialiserait le compte a rebours.
        private string _lastKnownTurnId = string.Empty;

        private bool IsOnlineGame => OnlineSessionTransit.IsOnlineMatch && _onlineMatch != null;

        private void Start()
        {
            var kind = OnlineSessionTransit.IsOnlineMatch ? OnlineSessionTransit.SelectedGameKind : SceneTransit.SelectedGame;
            StartGame(kind, OpeningMessage(kind));
        }

        private void OnDestroy()
        {
            StopOnlinePolling();
            UnbindTimerEvent();
        }

        private void BindTimerEvent()
        {
            if (_timerEventBound || hudView == null)
            {
                return;
            }

            hudView.TurnTimedOut += HandleTurnTimedOut;
            _timerEventBound = true;
        }

        private void UnbindTimerEvent()
        {
            if (!_timerEventBound || hudView == null)
            {
                _timerEventBound = false;
                return;
            }

            hudView.TurnTimedOut -= HandleTurnTimedOut;
            _timerEventBound = false;
        }

        public void SelectGame(GameKind kind)
        {
            OnlineSessionTransit.Clear();
            StartGame(kind, OpeningMessage(kind));
        }

        public void RestartGame()
        {
            if (IsOnlineGame)
            {
                hudView?.SetInfo("Recommencer indisponible en ligne. Utilisez Menu pour quitter la partie.");
                return;
            }

            if (!ResolveReferences())
            {
                _ready = false;
                return;
            }

            StartGame(gameKind, "Partie reinitialisee.");
        }

        public void PlayDirection(MoveDirection direction)
        {
            if (!EnsureReady())
            {
                return;
            }

            if (!CanActLocally())
            {
                return;
            }

            if (gameKind != GameKind.Quixo)
            {
                hudView.SetInfo("Qomet se joue en cliquant les points relies du plateau.");
                return;
            }

            if (_state.Winner != PlayerMark.None)
            {
                hudView.SetInfo($"{WinnerMessageForCurrentMatch(_state.Winner)} Recommencez ou revenez au menu.");
                return;
            }

            if (_selected == null)
            {
                hudView.SetInfo("Selectionnez un cube du bord avant de choisir une direction.");
                return;
            }

            var origin = _selected.Value;
            var payload = new OnlineMovePayload
            {
                gameKind = OnlineSessionTransit.GameKindName(gameKind),
                matchMode = OnlineSessionTransit.MatchModeName(OnlineSessionTransit.SelectedMatchMode),
                team = OnlineSessionTransit.TeamName(OnlineSessionTransit.TeamForUser(SessionManager.UserId)),
                playerId = SessionManager.UserId,
                action = "direction",
                selectedRow = origin.x,
                selectedCol = origin.y,
                direction = direction.ToString()
            };

            bool moved = _rules.TryDirectionalMove(_state, origin.x, origin.y, direction);
            if (!moved)
            {
                hudView.SetInfo("Mouvement invalide.");
                return;
            }

            EndTurn(origin);
            SubmitOnlineMove(payload);
        }

        private void HandleCellClick(int row, int col)
        {
            if (!EnsureReady())
            {
                return;
            }

            if (!CanActLocally())
            {
                return;
            }

            if (_state.Winner != PlayerMark.None)
            {
                hudView.SetInfo($"{WinnerMessageForCurrentMatch(_state.Winner)} Recommencez ou revenez au menu.");
                return;
            }

            if (gameKind == GameKind.Qomet)
            {
                HandleQometCellClick(row, col);
                return;
            }

            if (_selected == null)
            {
                if (!_rules.TrySelect(_state, row, col))
                {
                    hudView.SetInfo("Piece invalide.");
                    return;
                }

                _selected = new Vector2Int(row, col);
                var directions = _rules.GetDirections(_state, row, col);
                hudView.SetDirections(directions);
                hudView.SetInfo(SelectionMessage(directions));
                boardView.Render(_state, _selected);
                return;
            }

            if (_rules.TrySelect(_state, row, col))
            {
                _selected = new Vector2Int(row, col);
                var directions = _rules.GetDirections(_state, row, col);
                hudView.SetDirections(directions);
                hudView.SetInfo(SelectionMessage(directions));
                boardView.Render(_state, _selected);
            }
            else
            {
                _selected = null;
                hudView.SetDirections(new List<MoveDirection>());
                boardView.Render(_state, null);
                hudView.SetInfo("Selection annulee.");
            }
        }

        private void HandleQometCellClick(int row, int col)
        {
            var qometRules = _rules as QometGameRulesAdapter;
            if (qometRules == null)
            {
                hudView.SetInfo("Regles Qomet introuvables.");
                return;
            }

            if (!QometRules.InBounds(_state.Size, row, col))
            {
                hudView.SetInfo("Cliquez un point du plateau Qomet.");
                return;
            }

            if (_selected == null)
            {
                if (_state.Cells[row, col] == PlayerMark.None)
                {
                    if (qometRules.TryPlace(_state, row, col))
                    {
                        var payload = CreateQometPlacePayload(row, col);
                        EndTurn(new Vector2Int(row, col), $"{qometRules.LastMessage} {qometRules.ReserveStatus()}");
                        SubmitOnlineMove(payload);
                        return;
                    }

                    hudView.SetInfo(qometRules.LastMessage);
                    return;
                }

                if (qometRules.TrySelect(_state, row, col))
                {
                    _selected = new Vector2Int(row, col);
                    hudView.SetDirections(new List<MoveDirection>());
                    hudView.SetInfo($"Etoile selectionnee. Cliquez un point relie pour deplacer ou pousser. {qometRules.ReserveStatus()}");
                    boardView.Render(_state, _selected);
                    return;
                }

                hudView.SetInfo("Selectionnez une etoile a vous ou un point libre pour poser.");
                return;
            }

            var src = _selected.Value;
            if (src.x == row && src.y == col)
            {
                _selected = null;
                hudView.SetDirections(new List<MoveDirection>());
                hudView.SetInfo("Selection annulee.");
                boardView.Render(_state, null);
                return;
            }

            if (qometRules.TrySelect(_state, row, col))
            {
                _selected = new Vector2Int(row, col);
                hudView.SetInfo($"Nouvelle etoile selectionnee. {qometRules.ReserveStatus()}");
                boardView.Render(_state, _selected);
                return;
            }

            if (!qometRules.TryMoveToCell(_state, src.x, src.y, row, col))
            {
                hudView.SetInfo(qometRules.LastMessage);
                boardView.Render(_state, _selected);
                return;
            }

            var movePayload = CreateQometMovePayload(src.x, src.y, row, col);
            EndTurn(src, $"{qometRules.LastMessage} {qometRules.ReserveStatus()}");
            SubmitOnlineMove(movePayload);
        }

        private void EndTurn(Vector2Int fromCell, string successMessage = null)
        {
            var movedPlayer = _state.CurrentPlayer;
            var winner = _rules.EvaluateWinner(_state, movedPlayer);
            _selected = null;
            hudView.SetDirections(new List<MoveDirection>());
            boardView.AnimateBoardChange(_state, fromCell);

            if (winner != PlayerMark.None)
            {
                _state.SetWinner(winner);
                boardView.Render(_state, null);
                hudView.SetInfo(WinnerMessageForCurrentMatch(winner));
                hudView.SetTurn(_state.CurrentPlayer);
                hudView.StopTurnTimer();
                ShowGameOver(winner);
                return;
            }

            _state.SetCurrentPlayer(movedPlayer == PlayerMark.Player1 ? PlayerMark.Player2 : PlayerMark.Player1);
            boardView.Render(_state, null);
            hudView.SetTurn(_state.CurrentPlayer);
            hudView.SetInfo(string.IsNullOrWhiteSpace(successMessage) ? "Coup valide." : successMessage);

            // En local : on relance le timer pour le nouveau joueur courant.
            // En online : UpdateOnlineHud() est appele par le path ApplyOnlineMatchStatus et
            // gere le timer la-bas pour rester aligne avec le serveur. On stoppe le timer entre
            // les deux pour eviter qu'un timeout faussement decla par notre propre PATCH n'arrive
            // pendant la latence reseau.
            if (IsOnlineGame)
            {
                hudView.StopTurnTimer();
            }
            else
            {
                RestartTimerForCurrentPlayer();
            }
        }

        private static IGameRules CreateRules(GameKind kind)
        {
            return kind == GameKind.Quixo ? new QuixoGameRulesAdapter() : new QometGameRulesAdapter();
        }

        public void ReturnToMenu()
        {
            if (_loadingMenu)
            {
                return;
            }

            StopOnlinePolling();
            OnlineSessionTransit.Clear();

            if (Application.CanStreamedLevelBeLoaded(MenuSceneName))
            {
                _loadingMenu = true;
                SceneManager.LoadScene(MenuSceneName);
                return;
            }

            Debug.LogWarning($"Scene '{MenuSceneName}' introuvable. Ajoutez-la aux Build Settings.", this);
            if (hudView != null)
            {
                hudView.SetInfo("Scene MenuScene introuvable. Verifiez les Build Settings.");
            }
        }

        private void StartGame(GameKind kind, string message)
        {
            _ready = false;
            _gameOverShown = false;
            _lastLossWasTimeout = false;
            _lastKnownTurnId = string.Empty;
            StopOnlinePolling();

            if (!ResolveReferences())
            {
                enabled = false;
                return;
            }

            gameKind = kind;
            _rules = CreateRules(gameKind);
            _state = new BoardState(_rules.BoardSize);
            _rules.SetupInitialState(_state);
            _selected = null;
            _loadingMenu = false;
            _onlineSubmitting = false;
            _applyingOnlineMove = false;
            _lastAppliedMoveNumber = 0;

            // On lit la duree du tour pour cette session. En online, on prend la valeur stockee
            // dans OnlineSessionTransit (peuplee depuis le menu/invitation). Sinon, on lit la
            // preference locale du joueur. 0 = sans limite.
            _turnTimeSecondsForSession = OnlineSessionTransit.IsOnlineMatch && OnlineSessionTransit.TurnTimeSeconds > 0
                ? OnlineSessionTransit.TurnTimeSeconds
                : TurnTimerSettings.SelectedSeconds;

            hudView.Bind(this);
            hudView.SetGameKind(gameKind);
            hudView.HideGameOver();
            hudView.SetRestartEnabled(!OnlineSessionTransit.IsOnlineMatch);
            hudView.SetDirections(new List<MoveDirection>());
            BindTimerEvent();
            boardView.Initialize(_state.Size, HandleCellClick, gameKind);
            boardView.Render(_state, null);
            hudView.SetTurn(_state.CurrentPlayer);
            if (gameKind == GameKind.Qomet && _rules is QometGameRulesAdapter qometRules)
            {
                hudView.SetInfo($"{message} {qometRules.ReserveStatus()}");
            }
            else
            {
                hudView.SetInfo(message);
            }

            _ready = true;

            if (OnlineSessionTransit.IsOnlineMatch)
            {
                ConfigureOnlineSession();
            }
            else
            {
                // Demarrage du timer en local des que la partie est prete.
                RestartTimerForCurrentPlayer();
            }
        }

        private void RestartTimerForCurrentPlayer()
        {
            if (hudView == null || _state == null || _gameOverShown)
            {
                return;
            }

            if (_state.Winner != PlayerMark.None)
            {
                hudView.StopTurnTimer();
                return;
            }

            if (_turnTimeSecondsForSession <= 0)
            {
                hudView.StartTurnTimer(0, true);
                return;
            }

            bool isLocalTurn;
            if (IsOnlineGame)
            {
                string turnId = string.IsNullOrWhiteSpace(_onlineMatch.current_turn_id)
                    ? _onlineMatch.player1_id
                    : _onlineMatch.current_turn_id;
                isLocalTurn = turnId == SessionManager.UserId;
            }
            else
            {
                // En local 2 joueurs : on considere toujours que le timer s'applique au joueur courant
                // qui partage le clavier/souris avec son adversaire.
                isLocalTurn = true;
            }

            hudView.StartTurnTimer(_turnTimeSecondsForSession, isLocalTurn);
        }

        private void HandleTurnTimedOut()
        {
            if (_gameOverShown || _state == null || _state.Winner != PlayerMark.None)
            {
                return;
            }

            // En online, on n'agit que si le timeout concerne notre propre tour. L'autre client
            // verra le match passer en finished via le polling.
            if (IsOnlineGame)
            {
                string turnId = string.IsNullOrWhiteSpace(_onlineMatch.current_turn_id)
                    ? _onlineMatch.player1_id
                    : _onlineMatch.current_turn_id;
                if (turnId != SessionManager.UserId)
                {
                    return;
                }
            }

            PlayerMark loser = _state.CurrentPlayer;
            PlayerMark winner = loser == PlayerMark.Player1 ? PlayerMark.Player2 : PlayerMark.Player1;
            _lastLossWasTimeout = true;
            _state.SetWinner(winner);
            Debug.Log($"[Timer] Turn timed out. loser={loser} winner={winner} online={IsOnlineGame}");
            boardView?.Render(_state, null);
            hudView.SetInfo("Temps ecoule.");
            hudView.SetTurn(_state.CurrentPlayer);
            ShowGameOver(winner);

            if (IsOnlineGame && onlineMatchService != null && _onlineMatch != null)
            {
                if (OnlineSessionTransit.IsTeam2v2)
                {
                    var winnerTeam = winner == PlayerMark.Player1 ? TeamId.Team1 : TeamId.Team2;
                    onlineMatchService.UpdateTeamMatchFinished(_onlineMatch, winnerTeam, _ => { });
                }
                else
                {
                    string opponentId = OnlineSessionTransit.OpponentOf(SessionManager.UserId);
                    onlineMatchService.UpdateMatchFinished(_onlineMatch, opponentId, _ => { });
                }
            }
        }

        private void ConfigureOnlineSession()
        {
            ResolveOnlineServices();

            string localUserId = SessionManager.UserId;
            string currentTurnId = OnlineSessionTransit.CurrentTurnId;
            if (string.IsNullOrWhiteSpace(currentTurnId))
            {
                currentTurnId = OnlineSessionTransit.IsTeam2v2
                    ? OnlineSessionTransit.UserIdForTurnIndex(OnlineSessionTransit.CurrentTurnIndex)
                    : OnlineSessionTransit.Player1Id;
                OnlineSessionTransit.CurrentTurnId = currentTurnId;
            }

            _onlineMatch = new OnlineMatchDto
            {
                id = OnlineSessionTransit.MatchId,
                game_kind = OnlineSessionTransit.GameKindName(OnlineSessionTransit.SelectedGameKind),
                match_mode = OnlineSessionTransit.MatchModeName(OnlineSessionTransit.SelectedMatchMode),
                player1_id = OnlineSessionTransit.Player1Id,
                player2_id = OnlineSessionTransit.Player2Id,
                team1_player1_id = OnlineSessionTransit.Team1Player1Id,
                team1_player2_id = OnlineSessionTransit.Team1Player2Id,
                team2_player1_id = OnlineSessionTransit.Team2Player1Id,
                team2_player2_id = OnlineSessionTransit.Team2Player2Id,
                current_turn_id = currentTurnId,
                current_turn_index = OnlineSessionTransit.CurrentTurnIndex,
                status = "active"
            };

            if (!OnlineSessionTransit.IsValidForLocalPlayer(_onlineMatch, localUserId))
            {
                Debug.LogError($"[Online] Local user {localUserId} is not part of match {OnlineSessionTransit.MatchId}.");
                hudView.SetInfo("Vous ne faites pas partie de ce match. Retour au menu.");
                return;
            }

            // Aligne immediatement l'etat local sur le tour serveur.
            _state.SetCurrentPlayer(OnlineSessionTransit.PlayerMarkForUser(currentTurnId));

            bool isMyTurn = currentTurnId == localUserId;
            string localMark = OnlineSessionTransit.LocalPlayerMark() == PlayerMark.Player1 ? "Player1" : "Player2";
            Debug.Log($"[Online] Loaded match {_onlineMatch.id} mode={_onlineMatch.match_mode} p1={_onlineMatch.player1_id} p2={_onlineMatch.player2_id} turn={_onlineMatch.current_turn_id} local={localUserId} localMark={localMark} isMyTurn={isMyTurn}");

            onlinePresenceService?.StartPresence();
            hudView.SetRestartEnabled(false);
            UpdateOnlineHud();
            // Fetch a fresh snapshot immediately so the HUD reflects server state, not transit cache.
            onlineMatchService.FetchMatch(_onlineMatch.id, result =>
            {
                if (result != null && result.Success && result.Match != null)
                {
                    _onlineMatch = result.Match;
                    OnlineSessionTransit.UpdateMatch(_onlineMatch);
                    Debug.Log($"[Online] Fresh fetch match {_onlineMatch.id} turn={_onlineMatch.current_turn_id} status={_onlineMatch.status}");
                    ApplyOnlineMatchStatus();
                }
            });

            _onlinePollRoutine = StartCoroutine(OnlinePollRoutine());
        }

        private IEnumerator OnlinePollRoutine()
        {
            string lastLoggedTurn = string.Empty;
            while (IsOnlineGame && SessionManager.IsOnline)
            {
                bool matchDone = false;
                onlineMatchService.FetchMatch(_onlineMatch.id, result =>
                {
                    if (result != null && result.Success && result.Match != null)
                    {
                        _onlineMatch = result.Match;
                        OnlineSessionTransit.UpdateMatch(_onlineMatch);
                        if (_onlineMatch.current_turn_id != lastLoggedTurn)
                        {
                            lastLoggedTurn = _onlineMatch.current_turn_id;
                            Debug.Log($"[Online] Poll match {_onlineMatch.id} turn={_onlineMatch.current_turn_id} status={_onlineMatch.status} winner={_onlineMatch.winner_id}");
                        }
                        ApplyOnlineMatchStatus();
                    }

                    matchDone = true;
                });
                yield return new WaitUntil(() => matchDone);

                bool movesDone = false;
                List<OnlineMoveDto> moves = null;
                onlineMatchService.FetchMovesAfter(_onlineMatch.id, _lastAppliedMoveNumber, fetched =>
                {
                    moves = fetched;
                    movesDone = true;
                });
                yield return new WaitUntil(() => movesDone);

                ApplyFetchedMoves(moves);
                if (_onlineMatch != null && _onlineMatch.status == "finished")
                {
                    _onlinePollRoutine = null;
                    yield break;
                }

                yield return new WaitForSeconds(OnlinePollSeconds);
            }
        }

        private void ApplyFetchedMoves(List<OnlineMoveDto> moves)
        {
            if (moves == null)
            {
                return;
            }

            foreach (var move in moves)
            {
                if (move == null || move.move_number <= _lastAppliedMoveNumber)
                {
                    continue;
                }

                if (move.move_number > _lastAppliedMoveNumber + 1)
                {
                    return;
                }

                if (move.player_id == SessionManager.UserId)
                {
                    _lastAppliedMoveNumber = move.move_number;
                    continue;
                }

                if (ApplyOnlineMove(move))
                {
                    _lastAppliedMoveNumber = move.move_number;
                }
            }
        }

        private bool ApplyOnlineMove(OnlineMoveDto move)
        {
            if (move == null || move.move_payload == null || _state == null)
            {
                return false;
            }

            _applyingOnlineMove = true;
            _state.SetCurrentPlayer(OnlineSessionTransit.PlayerMarkForUser(move.player_id));
            bool applied = false;
            var payload = move.move_payload;
            if (gameKind == GameKind.Quixo)
            {
                if (Enum.TryParse(payload.direction, out MoveDirection direction))
                {
                    applied = _rules.TryDirectionalMove(_state, payload.selectedRow, payload.selectedCol, direction);
                    if (applied)
                    {
                        EndTurn(new Vector2Int(payload.selectedRow, payload.selectedCol), "Coup adverse recu.");
                    }
                }
            }
            else if (_rules is QometGameRulesAdapter qometRules)
            {
                if (payload.action == "place")
                {
                    applied = qometRules.TryPlace(_state, payload.toRow, payload.toCol);
                    if (applied)
                    {
                        EndTurn(new Vector2Int(payload.toRow, payload.toCol), $"Coup adverse recu. {qometRules.ReserveStatus()}");
                    }
                }
                else
                {
                    applied = qometRules.TryMoveToCell(_state, payload.fromRow, payload.fromCol, payload.toRow, payload.toCol);
                    if (applied)
                    {
                        EndTurn(new Vector2Int(payload.fromRow, payload.fromCol), $"Coup adverse recu. {qometRules.ReserveStatus()}");
                    }
                }
            }

            _applyingOnlineMove = false;
            if (!applied)
            {
                Debug.LogWarning($"[Online] Move #{move.move_number} from {move.player_id} could not be applied locally. payload={JsonUtility.ToJson(move.move_payload)}");
                hudView.SetInfo("Coup online recu mais non applicable. Resynchronisation en attente.");
            }
            else
            {
                Debug.Log($"[Online] Applied remote move #{move.move_number} from {move.player_id}");
                UpdateOnlineHud();
            }

            return applied;
        }

        private void SubmitOnlineMove(OnlineMovePayload payload)
        {
            if (!IsOnlineGame || _applyingOnlineMove || payload == null)
            {
                return;
            }

            _onlineSubmitting = true;
            string winnerId = WinnerUserId(_state.Winner);
            string winnerTeam = WinnerTeamName(_state.Winner);
            int nextTurnIndex = _onlineMatch.current_turn_index;
            string nextTurnId;
            if (OnlineSessionTransit.IsTeam2v2)
            {
                nextTurnIndex = string.IsNullOrWhiteSpace(winnerTeam)
                    ? (_onlineMatch.current_turn_index + 1) % 4
                    : _onlineMatch.current_turn_index;
                nextTurnId = string.IsNullOrWhiteSpace(winnerTeam)
                    ? OnlineSessionTransit.UserIdForTurnIndex(nextTurnIndex)
                    : SessionManager.UserId;
            }
            else
            {
                nextTurnId = string.IsNullOrWhiteSpace(winnerId)
                    ? OnlineSessionTransit.OpponentOf(SessionManager.UserId)
                    : SessionManager.UserId;
            }

            onlineMatchService.SubmitMove(_onlineMatch, payload, nextTurnId, winnerId, winnerTeam, nextTurnIndex, result =>
            {
                _onlineSubmitting = false;
                if (result == null || !result.Success)
                {
                    hudView.SetInfo(result != null ? result.Message : "Envoi du coup online impossible.");
                    return;
                }

                if (result.Match != null)
                {
                    _onlineMatch = result.Match;
                    OnlineSessionTransit.UpdateMatch(_onlineMatch);
                }

                ApplyOnlineMatchStatus();
                if (_state.Winner == PlayerMark.None)
                {
                    hudView.SetInfo(OnlineSessionTransit.IsTeam2v2
                        ? $"Coup envoye. Tour de {OnlineSessionTransit.UsernameForUser(nextTurnId)}."
                        : "Coup envoye. Tour de l'adversaire.");
                }
            });
        }

        private bool CanActLocally()
        {
            if (!IsOnlineGame || _applyingOnlineMove)
            {
                return true;
            }

            if (_onlineSubmitting)
            {
                hudView.SetInfo("Coup en cours d'envoi...");
                return false;
            }

            if (_onlineMatch.status == "finished")
            {
                ApplyOnlineMatchStatus();
                return false;
            }

            string turnId = string.IsNullOrWhiteSpace(_onlineMatch.current_turn_id)
                ? _onlineMatch.player1_id
                : _onlineMatch.current_turn_id;

            if (turnId != SessionManager.UserId)
            {
                if (OnlineSessionTransit.IsTeam2v2)
                {
                    TeamId localTeam = OnlineSessionTransit.TeamForUser(SessionManager.UserId);
                    TeamId activeTeam = OnlineSessionTransit.TeamForUser(turnId);
                    hudView.SetInfo(localTeam == activeTeam
                        ? "Tour de votre coequipier."
                        : "Tour de l'adversaire.");
                }
                else
                {
                    hudView.SetInfo("Tour de l'adversaire.");
                }

                return false;
            }

            return true;
        }

        private void ApplyOnlineMatchStatus()
        {
            if (_onlineMatch == null)
            {
                return;
            }

            if (_onlineMatch.status == "finished"
                && OnlineSessionTransit.IsTeam2v2
                && !string.IsNullOrWhiteSpace(_onlineMatch.winner_team))
            {
                TeamId winnerTeam = OnlineSessionTransit.ParseTeam(_onlineMatch.winner_team);
                var winner = winnerTeam == TeamId.Team1 ? PlayerMark.Player1 : PlayerMark.Player2;
                _state.SetWinner(winner);
                boardView.Render(_state, null);
                TeamId localTeam = OnlineSessionTransit.TeamForUser(SessionManager.UserId);
                hudView.SetInfo(localTeam == winnerTeam
                    ? $"Victoire de l'equipe {(winnerTeam == TeamId.Team1 ? "1" : "2")}."
                    : $"Defaite contre l'equipe {(winnerTeam == TeamId.Team1 ? "1" : "2")}.");
                ShowGameOver(winner);
                return;
            }

            if (_onlineMatch.status == "finished" && !string.IsNullOrWhiteSpace(_onlineMatch.winner_id))
            {
                var winner = OnlineSessionTransit.PlayerMarkForUser(_onlineMatch.winner_id);
                _state.SetWinner(winner);
                boardView.Render(_state, null);
                hudView.SetInfo(_onlineMatch.winner_id == SessionManager.UserId
                    ? "Victoire en ligne."
                    : "Defaite en ligne.");
                ShowGameOver(winner);
                return;
            }

            UpdateOnlineHud();
        }

        private void UpdateOnlineHud()
        {
            if (!IsOnlineGame || hudView == null || _state == null)
            {
                return;
            }

            string turnId = string.IsNullOrWhiteSpace(_onlineMatch.current_turn_id)
                ? _onlineMatch.player1_id
                : _onlineMatch.current_turn_id;

            var currentPlayer = OnlineSessionTransit.PlayerMarkForUser(turnId);
            _state.SetCurrentPlayer(currentPlayer);
            bool isMyTurn = !string.IsNullOrWhiteSpace(SessionManager.UserId) && turnId == SessionManager.UserId;
            bool turnChanged = !string.Equals(_lastKnownTurnId, turnId, StringComparison.Ordinal);

            if (OnlineSessionTransit.IsTeam2v2)
            {
                TeamId localTeam = OnlineSessionTransit.TeamForUser(SessionManager.UserId);
                TeamId activeTeam = OnlineSessionTransit.TeamForUser(turnId);
                string activeName = OnlineSessionTransit.UsernameForUser(turnId);
                string relation = isMyTurn
                    ? "A vous de jouer"
                    : localTeam == activeTeam ? "Tour de votre coequipier" : "Tour de l'adversaire";
                hudView.SetTurn(_state.CurrentPlayer, isMyTurn ? "A vous de jouer" : $"Tour : {activeName}");
                hudView.SetInfo(
                    $"Mode : Quixo equipe 2v2 | Equipe 1 : {OnlineSessionTransit.TeamLabel(TeamId.Team1)} | Equipe 2 : {OnlineSessionTransit.TeamLabel(TeamId.Team2)} | {relation}.");

                if (turnChanged)
                {
                    _lastKnownTurnId = turnId;
                    if (_state.Winner == PlayerMark.None && _onlineMatch.status != "finished")
                    {
                        RestartTimerForCurrentPlayer();
                    }
                }

                return;
            }

            string opponent = string.IsNullOrWhiteSpace(OnlineSessionTransit.OpponentUsername)
                ? "adversaire"
                : OnlineSessionTransit.OpponentUsername;
            string turnLabel = isMyTurn ? "A vous de jouer" : $"Tour de {opponent}";
            hudView.SetTurn(_state.CurrentPlayer, turnLabel);
            hudView.SetInfo(isMyTurn
                ? $"Online vs {opponent} : a vous de jouer."
                : $"Online vs {opponent} : tour de l'adversaire.");

            // Le timer ne doit etre relance QUE quand le tour change. Sans cette garde, chaque
            // poll (1s) remettrait le compte a rebours a fond.
            if (turnChanged)
            {
                _lastKnownTurnId = turnId;
                if (_state.Winner == PlayerMark.None && _onlineMatch.status != "finished")
                {
                    RestartTimerForCurrentPlayer();
                }
            }
        }

        private void StopOnlinePolling()
        {
            if (_onlinePollRoutine != null)
            {
                StopCoroutine(_onlinePollRoutine);
                _onlinePollRoutine = null;
            }
        }

        private bool EnsureReady()
        {
            if (_ready && _rules != null && _state != null && boardView != null && hudView != null)
            {
                return true;
            }

            Debug.LogWarning("GameFlowController called before the game was ready.", this);
            return false;
        }

        private bool ResolveReferences()
        {
            if (boardView == null)
            {
                boardView = FindObjectOfType<BoardViewRenderer>();
            }

            if (hudView == null)
            {
                hudView = FindObjectOfType<HudView>();
            }

            if (boardView != null && hudView != null)
            {
                ResolveOnlineServices();
                return true;
            }

            Debug.LogError("GameFlowController requires BoardViewRenderer and HudView references.", this);
            return false;
        }

        private void ResolveOnlineServices()
        {
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
        }

        private string WinnerUserId(PlayerMark winner)
        {
            if (winner == PlayerMark.None)
            {
                return string.Empty;
            }

            if (OnlineSessionTransit.IsTeam2v2)
            {
                return string.Empty;
            }

            return winner == PlayerMark.Player1 ? OnlineSessionTransit.Player1Id : OnlineSessionTransit.Player2Id;
        }

        private static string WinnerTeamName(PlayerMark winner)
        {
            if (!OnlineSessionTransit.IsTeam2v2 || winner == PlayerMark.None)
            {
                return string.Empty;
            }

            return OnlineSessionTransit.TeamName(winner == PlayerMark.Player1 ? TeamId.Team1 : TeamId.Team2);
        }

        private void ShowGameOver(PlayerMark winner)
        {
            if (hudView == null || winner == PlayerMark.None)
            {
                return;
            }

            if (_gameOverShown)
            {
                return;
            }

            _gameOverShown = true;
            hudView.StopTurnTimer();
            StopOnlinePolling();

            if (IsOnlineGame && OnlineSessionTransit.IsTeam2v2)
            {
                TeamId winningTeam = winner == PlayerMark.Player1 ? TeamId.Team1 : TeamId.Team2;
                TeamId losingTeam = OnlineSessionTransit.OpposingTeam(winningTeam);
                string winnerTeamLabel = winningTeam == TeamId.Team1 ? "equipe 1" : "equipe 2";
                string losingTeamLabel = OnlineSessionTransit.TeamLabel(losingTeam);
                string teamGameOverSubtitle = _lastLossWasTimeout
                    ? $"{losingTeamLabel} a perdu par inactivite."
                    : $"Gagnants : {OnlineSessionTransit.TeamLabel(winningTeam)}";
                hudView.ShowGameOver($"Victoire de l'{winnerTeamLabel}", teamGameOverSubtitle, false);
                return;
            }

            string winnerName = FormatPlayer(winner, gameKind);
            string loserName;
            if (IsOnlineGame)
            {
                string winnerId = WinnerUserId(winner);
                bool localIsWinner = winnerId == SessionManager.UserId;
                string localName = string.IsNullOrWhiteSpace(SessionManager.Username) ? "vous" : SessionManager.Username;
                string opponentName = string.IsNullOrWhiteSpace(OnlineSessionTransit.OpponentUsername) ? "adversaire" : OnlineSessionTransit.OpponentUsername;
                winnerName = localIsWinner ? localName : opponentName;
                loserName = localIsWinner ? opponentName : localName;
            }
            else
            {
                var loserMark = winner == PlayerMark.Player1 ? PlayerMark.Player2 : PlayerMark.Player1;
                loserName = FormatPlayer(loserMark, gameKind);
            }

            string subtitle;
            if (_lastLossWasTimeout)
            {
                subtitle = IsOnlineGame
                    ? $"{loserName} a perdu par inactivite."
                    : $"{loserName} a perdu par depassement du temps.";
            }
            else
            {
                subtitle = "La partie est terminee.";
            }

            hudView.ShowGameOver($"Victoire de {winnerName}", subtitle, !IsOnlineGame);
        }

        private static OnlineMovePayload CreateQometPlacePayload(int row, int col)
        {
            return new OnlineMovePayload
            {
                gameKind = OnlineSessionTransit.GameKindName(GameKind.Qomet),
                matchMode = OnlineSessionTransit.MatchModeName(OnlineSessionTransit.SelectedMatchMode),
                team = OnlineSessionTransit.TeamName(OnlineSessionTransit.TeamForUser(SessionManager.UserId)),
                playerId = SessionManager.UserId,
                action = "place",
                toRow = row,
                toCol = col,
                toNode = OnlineSessionTransit.NodeName(row, col)
            };
        }

        private static OnlineMovePayload CreateQometMovePayload(int fromRow, int fromCol, int toRow, int toCol)
        {
            return new OnlineMovePayload
            {
                gameKind = OnlineSessionTransit.GameKindName(GameKind.Qomet),
                matchMode = OnlineSessionTransit.MatchModeName(OnlineSessionTransit.SelectedMatchMode),
                team = OnlineSessionTransit.TeamName(OnlineSessionTransit.TeamForUser(SessionManager.UserId)),
                playerId = SessionManager.UserId,
                action = "move",
                fromRow = fromRow,
                fromCol = fromCol,
                toRow = toRow,
                toCol = toCol,
                fromNode = OnlineSessionTransit.NodeName(fromRow, fromCol),
                toNode = OnlineSessionTransit.NodeName(toRow, toCol)
            };
        }

        private static string OpeningMessage(GameKind kind)
        {
            return kind == GameKind.Quixo
                ? "Choisissez un cube du bord libre ou a vous, puis une direction."
                : "Qomet: posez vos 7 etoiles, puis deplacez ou poussez le long des lignes.";
        }

        private static string SelectionMessage(IReadOnlyList<MoveDirection> directions)
        {
            if (directions == null || directions.Count == 0)
            {
                return "Piece selectionnee.";
            }

            return "Piece selectionnee. Choisissez une direction.";
        }

        private static string WinnerMessage(PlayerMark winner, GameKind kind)
        {
            return $"Victoire: {FormatPlayer(winner, kind)}";
        }

        private string WinnerMessageForCurrentMatch(PlayerMark winner)
        {
            if (IsOnlineGame && OnlineSessionTransit.IsTeam2v2)
            {
                return winner == PlayerMark.Player1
                    ? "Victoire de l'equipe 1"
                    : "Victoire de l'equipe 2";
            }

            return WinnerMessage(winner, gameKind);
        }

        private static string FormatPlayer(PlayerMark player, GameKind kind)
        {
            if (kind == GameKind.Qomet)
            {
                return player == PlayerMark.Player1 ? "Joueur 1 jaune" : "Joueur 2 rouge";
            }

            return player == PlayerMark.Player1 ? "Joueur 1 (X)" : "Joueur 2 (O)";
        }
    }
}
