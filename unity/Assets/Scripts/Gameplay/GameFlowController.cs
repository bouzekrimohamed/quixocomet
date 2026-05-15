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

        private bool IsOnlineGame => OnlineSessionTransit.IsOnlineMatch && _onlineMatch != null;

        private void Start()
        {
            var kind = OnlineSessionTransit.IsOnlineMatch ? OnlineSessionTransit.SelectedGameKind : SceneTransit.SelectedGame;
            StartGame(kind, OpeningMessage(kind));
        }

        private void OnDestroy()
        {
            StopOnlinePolling();
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
                hudView.SetInfo($"{WinnerMessage(_state.Winner, gameKind)} Recommencez ou revenez au menu.");
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
                hudView.SetInfo($"{WinnerMessage(_state.Winner, gameKind)} Recommencez ou revenez au menu.");
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
                hudView.SetInfo(WinnerMessage(winner, gameKind));
                hudView.SetTurn(_state.CurrentPlayer);
                return;
            }

            _state.SetCurrentPlayer(movedPlayer == PlayerMark.Player1 ? PlayerMark.Player2 : PlayerMark.Player1);
            boardView.Render(_state, null);
            hudView.SetTurn(_state.CurrentPlayer);
            hudView.SetInfo(string.IsNullOrWhiteSpace(successMessage) ? "Coup valide." : successMessage);
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

            hudView.Bind(this);
            hudView.SetGameKind(gameKind);
            hudView.SetRestartEnabled(!OnlineSessionTransit.IsOnlineMatch);
            hudView.SetDirections(new List<MoveDirection>());
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
        }

        private void ConfigureOnlineSession()
        {
            ResolveOnlineServices();

            string localUserId = SessionManager.UserId;
            if (localUserId != OnlineSessionTransit.Player1Id && localUserId != OnlineSessionTransit.Player2Id)
            {
                Debug.LogError($"[Online] Local user {localUserId} is not part of match {OnlineSessionTransit.MatchId} (p1={OnlineSessionTransit.Player1Id} p2={OnlineSessionTransit.Player2Id}).");
                hudView.SetInfo("Vous ne faites pas partie de ce match. Retour au menu.");
                return;
            }

            string currentTurnId = OnlineSessionTransit.CurrentTurnId;
            if (string.IsNullOrWhiteSpace(currentTurnId))
            {
                // Securite : si le serveur ne renvoie pas current_turn_id, c'est forcement player1.
                currentTurnId = OnlineSessionTransit.Player1Id;
                OnlineSessionTransit.CurrentTurnId = currentTurnId;
            }

            _onlineMatch = new OnlineMatchDto
            {
                id = OnlineSessionTransit.MatchId,
                game_kind = OnlineSessionTransit.GameKindName(OnlineSessionTransit.SelectedGameKind),
                player1_id = OnlineSessionTransit.Player1Id,
                player2_id = OnlineSessionTransit.Player2Id,
                current_turn_id = currentTurnId,
                status = "active"
            };

            // Aligne immediatement l'etat local sur le tour serveur.
            _state.SetCurrentPlayer(OnlineSessionTransit.PlayerMarkForUser(currentTurnId));

            bool isMyTurn = currentTurnId == localUserId;
            string localMark = OnlineSessionTransit.LocalPlayerMark() == PlayerMark.Player1 ? "Player1" : "Player2";
            Debug.Log($"[Online] Loaded match {_onlineMatch.id} p1={_onlineMatch.player1_id} p2={_onlineMatch.player2_id} turn={_onlineMatch.current_turn_id} local={localUserId} localMark={localMark} isMyTurn={isMyTurn}");

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
            string nextTurnId = string.IsNullOrWhiteSpace(winnerId)
                ? OnlineSessionTransit.OpponentOf(SessionManager.UserId)
                : SessionManager.UserId;

            onlineMatchService.SubmitMove(_onlineMatch, payload, nextTurnId, winnerId, result =>
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
                    hudView.SetInfo("Coup envoye. Tour de l'adversaire.");
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
                hudView.SetInfo("Tour de l'adversaire.");
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

            if (_onlineMatch.status == "finished" && !string.IsNullOrWhiteSpace(_onlineMatch.winner_id))
            {
                var winner = OnlineSessionTransit.PlayerMarkForUser(_onlineMatch.winner_id);
                _state.SetWinner(winner);
                boardView.Render(_state, null);
                hudView.SetInfo(_onlineMatch.winner_id == SessionManager.UserId
                    ? "Victoire en ligne."
                    : "Defaite en ligne.");
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
            hudView.SetTurn(_state.CurrentPlayer);
            string opponent = string.IsNullOrWhiteSpace(OnlineSessionTransit.OpponentUsername)
                ? "adversaire"
                : OnlineSessionTransit.OpponentUsername;
            bool isMyTurn = !string.IsNullOrWhiteSpace(SessionManager.UserId) && turnId == SessionManager.UserId;
            hudView.SetInfo(isMyTurn
                ? $"Online vs {opponent}: a vous de jouer."
                : $"Online vs {opponent}: tour de l'adversaire.");
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

            return winner == PlayerMark.Player1 ? OnlineSessionTransit.Player1Id : OnlineSessionTransit.Player2Id;
        }

        private static OnlineMovePayload CreateQometPlacePayload(int row, int col)
        {
            return new OnlineMovePayload
            {
                gameKind = OnlineSessionTransit.GameKindName(GameKind.Qomet),
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
