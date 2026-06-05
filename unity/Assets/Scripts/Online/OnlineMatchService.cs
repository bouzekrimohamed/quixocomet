using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using QuixoUnity.Auth;
using QuixoUnity.Core;
using QuixoUnity.Gameplay;
using UnityEngine;
using UnityEngine.Networking;

namespace QuixoUnity.Online
{
    public sealed class OnlineMatchService : MonoBehaviour
    {
        private const float MatchmakingPollSeconds = 2f;
        // On ignore les rows matchmaking_queue dont updated_at est plus vieux que ce delai :
        // un joueur qui a ferme Unity sans annuler laisse une ligne fantome ; sans ce filtre
        // le client suivant matche "avec du vide".
        private const double QueueFreshnessSeconds = 90.0;
        // Idem pour les anciens online_matches non termines : on ignore au-dela de 5 min sans
        // mise a jour, pour eviter de retomber sur une partie zombie.
        private const double ActiveMatchFreshnessSeconds = 300.0;
        // Invitations pending trop vieilles : on les ignore cote UI pour eviter les lignes fantomes.
        private const double PendingInviteFreshnessSeconds = 86400.0;
        private const string MatchSelectWithTime = "id,game_kind,match_mode,player1_id,player2_id,team1_player1_id,team1_player2_id,team2_player1_id,team2_player2_id,current_turn_id,current_turn_index,status,winner_id,winner_team,time_control_key,initial_seconds,increment_seconds,created_at,updated_at";
        private const string MatchSelectNoTime = "id,game_kind,match_mode,player1_id,player2_id,team1_player1_id,team1_player2_id,team2_player1_id,team2_player2_id,current_turn_id,current_turn_index,status,winner_id,winner_team,created_at,updated_at";
        private const string MatchSelectLegacy = "id,game_kind,player1_id,player2_id,current_turn_id,status,winner_id,created_at,updated_at";
        private const string InviteSelectWithTime = "id,from_user_id,to_user_id,game_kind,status,match_id,time_control_key,initial_seconds,increment_seconds,created_at,updated_at";
        private const string InviteSelectLegacy = "id,from_user_id,to_user_id,game_kind,status,match_id,created_at,updated_at";
        private const string QueueSelectWithTime = "id,user_id,game_kind,status,match_id,time_control_key,initial_seconds,increment_seconds,created_at,updated_at";
        // Fallback select sans les colonnes cadence : utilise si la migration SQL section 13
        // n'a pas ete appliquee sur la base Supabase de l'utilisateur. Garde la compat 1v1
        // hors cadence pour ne pas casser le matchmaking sur les anciennes bases.
        private const string QueueSelectLegacy = "id,user_id,game_kind,status,match_id,created_at,updated_at";
        private const string LobbySelectWithTime = "id,lobby_code,game_kind,match_mode,host_user_id,status,match_id,time_control_key,initial_seconds,increment_seconds,created_at,updated_at";

        private Coroutine _matchmakingRoutine;
        private TurnTimerSettings.TimeControlOption _matchmakingTimeControl;
        // Une fois qu'on a constate que le schema n'a pas les colonnes timer pour la queue,
        // on garde un flag pour eviter de toujours faire deux requetes. Reset au demarrage
        // de chaque session Unity (variable d'instance).
        private bool _queueSchemaWithoutTime;
        private bool _matchSchemaWithoutTime;

        public void SendInvite(string friendUserId, GameKind kind, Action<OnlineOperationResult> onComplete)
        {
            if (!EnsureOnline(onComplete))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(friendUserId))
            {
                onComplete?.Invoke(OnlineOperationResult.Fail("Ami invalide."));
                return;
            }

            StartCoroutine(SendInviteRoutine(friendUserId, kind, onComplete));
        }

        public void LoadPendingInvites(Action<List<MatchInviteDto>> onComplete)
        {
            if (!SessionManager.IsOnline)
            {
                onComplete?.Invoke(new List<MatchInviteDto>());
                return;
            }

            StartCoroutine(LoadPendingInvitesRoutine(onComplete));
        }

        public void LoadAcceptedSentInvites(Action<List<MatchInviteDto>> onComplete)
        {
            if (!SessionManager.IsOnline)
            {
                onComplete?.Invoke(new List<MatchInviteDto>());
                return;
            }

            StartCoroutine(LoadAcceptedSentInvitesRoutine(onComplete));
        }

        public void AcceptInvite(MatchInviteDto invite, Action<OnlineOperationResult> onComplete)
        {
            if (!EnsureOnline(onComplete))
            {
                return;
            }

            if (invite == null || string.IsNullOrWhiteSpace(invite.id))
            {
                onComplete?.Invoke(OnlineOperationResult.Fail("Invitation invalide."));
                return;
            }

            StartCoroutine(AcceptInviteRoutine(invite, onComplete));
        }

        public void RejectInvite(string inviteId, Action<OnlineOperationResult> onComplete)
        {
            if (!EnsureOnline(onComplete))
            {
                return;
            }

            StartCoroutine(UpdateInviteRoutine(inviteId, "rejected", null, result =>
            {
                onComplete?.Invoke(result.Success ? OnlineOperationResult.Ok("Invitation refusee.") : result);
            }));
        }

        public void StartMatchmaking(GameKind kind, Action<OnlineOperationResult> onMatched, Action<string> onStatus = null)
        {
            if (!EnsureOnline(onMatched))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(SessionManager.UserId))
            {
                onMatched?.Invoke(OnlineOperationResult.Fail("Session utilisateur invalide. Reconnectez-vous."));
                return;
            }

            CancelLocalMatchmakingLoop();
            _matchmakingTimeControl = TurnTimerSettings.SelectedOption;
            _matchmakingRoutine = StartCoroutine(MatchmakingBootstrapAndLoop(kind, onMatched, onStatus));
        }

        private IEnumerator MatchmakingBootstrapAndLoop(GameKind kind, Action<OnlineOperationResult> onMatched, Action<string> onStatus)
        {
            string gameKind = OnlineSessionTransit.GameKindName(kind);
            var timeControl = ActiveMatchmakingTimeControl();
            Debug.Log($"[Matchmaking] Start user={SessionManager.UserId} game={gameKind} mode=OneVsOne time={timeControl.Key}");
            // 1. On annule toute ancienne ligne (waiting/matched) pour partir propre. La nouvelle
            //    cadence ecrasera celle d'une eventuelle session abandonnee.
            yield return CancelAllOwnQueuesRoutine(gameKind);
            // 2. On (re)pose une ligne waiting toute fraiche : created_at = now, match_id = null,
            //    time_control_key = cadence courante.
            yield return EnsureFreshOwnQueueRoutine(gameKind);
            Debug.Log($"[Matchmaking] Waiting queue user={SessionManager.UserId} game={gameKind} time={timeControl.Key}");
            yield return MatchmakingLoop(kind, onMatched, onStatus);
        }

        public void CancelMatchmaking(GameKind kind, Action<OnlineOperationResult> onComplete)
        {
            CancelLocalMatchmakingLoop();
            if (!EnsureOnline(onComplete))
            {
                return;
            }

            StartCoroutine(CancelMatchmakingRoutine(kind, onComplete));
        }

        public void FetchMatch(string matchId, Action<OnlineOperationResult> onComplete)
        {
            if (!EnsureOnline(onComplete))
            {
                return;
            }

            StartCoroutine(FetchMatchRoutine(matchId, onComplete));
        }

        public void FetchMovesAfter(string matchId, int lastMoveNumber, Action<List<OnlineMoveDto>> onComplete)
        {
            if (!SessionManager.IsOnline || string.IsNullOrWhiteSpace(matchId))
            {
                onComplete?.Invoke(new List<OnlineMoveDto>());
                return;
            }

            StartCoroutine(FetchMovesAfterRoutine(matchId, lastMoveNumber, onComplete));
        }

        public void SubmitMove(
            OnlineMatchDto match,
            OnlineMovePayload payload,
            string nextTurnId,
            string winnerId,
            string winnerTeam,
            int currentTurnIndex,
            Action<OnlineOperationResult> onComplete)
        {
            if (!EnsureOnline(onComplete))
            {
                return;
            }

            if (match == null || payload == null)
            {
                onComplete?.Invoke(OnlineOperationResult.Fail("Coup en ligne invalide."));
                return;
            }

            StartCoroutine(SubmitMoveRoutine(match, payload, nextTurnId, winnerId, winnerTeam, currentTurnIndex, onComplete));
        }

        public void UpdateMatchFinished(OnlineMatchDto match, string winnerId, Action<OnlineOperationResult> onComplete)
        {
            if (!EnsureOnline(onComplete) || match == null)
            {
                return;
            }

            StartCoroutine(PatchMatchRoutine(match.id, match.current_turn_id, "finished", winnerId, null, match.current_turn_index, onComplete));
        }

        public void UpdateTeamMatchFinished(OnlineMatchDto match, TeamId winnerTeam, Action<OnlineOperationResult> onComplete)
        {
            if (!EnsureOnline(onComplete) || match == null)
            {
                return;
            }

            StartCoroutine(PatchMatchRoutine(match.id, match.current_turn_id, "finished", string.Empty, OnlineSessionTransit.TeamName(winnerTeam), match.current_turn_index, onComplete));
        }

        public void CreateTeamLobby(Action<TeamLobbyOperationResult> onComplete)
        {
            if (!EnsureOnline(result => onComplete?.Invoke(TeamLobbyOperationResult.Fail(result.Message))))
            {
                return;
            }

            StartCoroutine(CreateTeamLobbyRoutine(onComplete));
        }

        public void JoinTeamLobby(string lobbyCode, TeamId team, Action<TeamLobbyOperationResult> onComplete)
        {
            if (!EnsureOnline(result => onComplete?.Invoke(TeamLobbyOperationResult.Fail(result.Message))))
            {
                return;
            }

            if (team == TeamId.None)
            {
                onComplete?.Invoke(TeamLobbyOperationResult.Fail("Equipe invalide."));
                return;
            }

            StartCoroutine(JoinTeamLobbyRoutine(lobbyCode, team, onComplete));
        }

        public void FetchTeamLobby(string lobbyId, Action<TeamLobbyOperationResult> onComplete)
        {
            if (!EnsureOnline(result => onComplete?.Invoke(TeamLobbyOperationResult.Fail(result.Message))))
            {
                return;
            }

            StartCoroutine(FetchTeamLobbyByIdRoutine(lobbyId, onComplete));
        }

        public void FetchTeamLobbyByCode(string lobbyCode, Action<TeamLobbyOperationResult> onComplete)
        {
            if (!EnsureOnline(result => onComplete?.Invoke(TeamLobbyOperationResult.Fail(result.Message))))
            {
                return;
            }

            string code = NormalizeLobbyCode(lobbyCode);
            if (string.IsNullOrWhiteSpace(code))
            {
                onComplete?.Invoke(TeamLobbyOperationResult.Fail("Entrez un code salon."));
                return;
            }

            StartCoroutine(FetchTeamLobbyByCodeRoutine(code, onComplete));
        }

        public void LeaveTeamLobby(string lobbyId, Action<TeamLobbyOperationResult> onComplete)
        {
            if (!EnsureOnline(result => onComplete?.Invoke(TeamLobbyOperationResult.Fail(result.Message))))
            {
                return;
            }

            StartCoroutine(LeaveTeamLobbyRoutine(lobbyId, onComplete));
        }

        public void StartTeamLobby(string lobbyId, Action<TeamLobbyOperationResult> onComplete)
        {
            if (!EnsureOnline(result => onComplete?.Invoke(TeamLobbyOperationResult.Fail(result.Message))))
            {
                return;
            }

            StartCoroutine(StartTeamLobbyRoutine(lobbyId, onComplete));
        }

        private IEnumerator SendInviteRoutine(string friendUserId, GameKind kind, Action<OnlineOperationResult> onComplete)
        {
            var timeControl = TurnTimerSettings.SelectedOption;
            string gameKind = OnlineSessionTransit.GameKindName(kind);
            Debug.Log($"[Invite] Creating invite to={friendUserId} game={gameKind} cadence={timeControl.Key}");

            UnityWebRequest request = null;
            yield return PostInviteRequest(friendUserId, gameKind, timeControl, withTime: true, completed => request = completed);
            using (request)
            {
                if (IsSuccess(request))
                {
                    CompleteInviteCreate(request, onComplete);
                    yield break;
                }

                if (IsMissingTimeControlSchemaError(request))
                {
                    Debug.LogWarning("[Invite] Missing SQL migration for timer columns; retrying without cadence fields.");
                    UnityWebRequest legacyRequest = null;
                    yield return PostInviteRequest(friendUserId, gameKind, timeControl, withTime: false, completed => legacyRequest = completed);
                    using (legacyRequest)
                    {
                        if (IsSuccess(legacyRequest))
                        {
                            CompleteInviteCreate(legacyRequest, onComplete);
                            yield break;
                        }

                        string reason = ParseInviteError(legacyRequest, "Impossible d'envoyer l'invitation.");
                        Debug.LogWarning($"[Invite] Failed reason={reason}");
                        onComplete?.Invoke(OnlineOperationResult.Fail(reason));
                    }

                    yield break;
                }

                string error = ParseInviteError(request, "Impossible d'envoyer l'invitation.");
                Debug.LogWarning($"[Invite] Failed reason={error}");
                onComplete?.Invoke(OnlineOperationResult.Fail(error));
            }
        }

        private IEnumerator LoadPendingInvitesRoutine(Action<List<MatchInviteDto>> onComplete)
        {
            string userId = Escape(SessionManager.UserId);
            string url = $"{SupabaseSettings.Url}/rest/v1/match_invites?to_user_id=eq.{userId}&status=eq.pending&select={InviteSelectWithTime}&order=created_at.desc";
            List<MatchInviteDto> invites = null;
            yield return FetchInvitesWithFallbackRoutine(
                url,
                $"{SupabaseSettings.Url}/rest/v1/match_invites?to_user_id=eq.{userId}&status=eq.pending&select={InviteSelectLegacy}&order=created_at.desc",
                loaded => invites = loaded);
            onComplete?.Invoke(FilterFreshPendingInvites(invites));
        }

        private IEnumerator LoadAcceptedSentInvitesRoutine(Action<List<MatchInviteDto>> onComplete)
        {
            string userId = Escape(SessionManager.UserId);
            string url = $"{SupabaseSettings.Url}/rest/v1/match_invites?from_user_id=eq.{userId}&status=eq.accepted&match_id=not.is.null&select={InviteSelectWithTime}&order=updated_at.desc&limit=3";
            List<MatchInviteDto> invites = null;
            yield return FetchInvitesWithFallbackRoutine(
                url,
                $"{SupabaseSettings.Url}/rest/v1/match_invites?from_user_id=eq.{userId}&status=eq.accepted&match_id=not.is.null&select={InviteSelectLegacy}&order=updated_at.desc&limit=3",
                loaded => invites = loaded);

            var valid = new List<MatchInviteDto>();
            if (invites != null)
            {
                foreach (var invite in invites)
                {
                    if (invite == null || string.IsNullOrWhiteSpace(invite.match_id))
                    {
                        continue;
                    }

                    OnlineOperationResult matchResult = null;
                    yield return FetchMatchRoutine(invite.match_id, result => matchResult = result);
                    if (matchResult == null || !matchResult.Success || matchResult.Match == null)
                    {
                        continue;
                    }

                    if (!string.Equals(matchResult.Match.status, "active", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!OnlineSessionTransit.IsValidForLocalPlayer(matchResult.Match, SessionManager.UserId))
                    {
                        continue;
                    }

                    valid.Add(invite);
                }
            }

            onComplete?.Invoke(valid);
        }

        private IEnumerator AcceptInviteRoutine(MatchInviteDto invite, Action<OnlineOperationResult> onComplete)
        {
            Debug.Log($"[Invite] Accepting invite id={invite.id} from={invite.from_user_id} game={invite.game_kind}");
            OnlineMatchDto match = null;
            yield return CreateMatchRoutine(invite.game_kind, invite.from_user_id, invite.to_user_id, TimeControlFromInvite(invite), created => match = created);
            if (match == null)
            {
                Debug.LogWarning("[Invite] Failed reason=match creation rejected by Supabase");
                onComplete?.Invoke(OnlineOperationResult.Fail("Creation du match impossible."));
                yield break;
            }

            Debug.Log($"[Invite] Created match id={match.id} p1={match.player1_id} p2={match.player2_id} turn={match.current_turn_id}");
            yield return UpdateInviteRoutine(invite.id, "accepted", match.id, result =>
            {
                if (!result.Success)
                {
                    Debug.LogWarning($"[Invite] Failed reason=invite patch rejected ({result.Message})");
                }

                onComplete?.Invoke(result.Success ? OnlineOperationResult.Ok("Invitation acceptee.", match, invite) : result);
            });
        }

        private IEnumerator UpdateInviteRoutine(string inviteId, string status, string matchId, Action<OnlineOperationResult> onComplete)
        {
            string json = "{\"status\":\"" + EscapeJson(status) + "\",\"updated_at\":\"" + DateTime.UtcNow.ToString("o") + "\"";
            if (!string.IsNullOrWhiteSpace(matchId))
            {
                json += ",\"match_id\":\"" + EscapeJson(matchId) + "\"";
            }

            json += "}";

            string url = $"{SupabaseSettings.Url}/rest/v1/match_invites?id=eq.{Escape(inviteId)}&to_user_id=eq.{Escape(SessionManager.UserId)}";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "PATCH", json);
                    created.SetRequestHeader("Prefer", "return=minimal");
                    return created;
                },
                completed => request = completed);
            using (request)
            {
                onComplete?.Invoke(IsSuccess(request)
                    ? OnlineOperationResult.Ok("Invitation mise a jour.")
                    : OnlineOperationResult.Fail(ParseInviteError(request, "Mise a jour invitation impossible.")));
            }
        }

        private IEnumerator CreateTeamLobbyRoutine(Action<TeamLobbyOperationResult> onComplete)
        {
            string code = GenerateLobbyCode();
            var timeControl = TurnTimerSettings.SelectedOption;
            string json = "{"
                + "\"lobby_code\":\"" + EscapeJson(code) + "\","
                + "\"game_kind\":\"Quixo\","
                + "\"match_mode\":\"Team2v2\","
                + "\"host_user_id\":\"" + EscapeJson(SessionManager.UserId) + "\","
                + "\"status\":\"lobby\","
                + "\"time_control_key\":\"" + EscapeJson(timeControl.Key) + "\","
                + "\"initial_seconds\":" + timeControl.InitialSeconds + ","
                + "\"increment_seconds\":" + timeControl.IncrementSeconds + ","
                + "\"updated_at\":\"" + DateTime.UtcNow.ToString("o") + "\""
                + "}";

            string url = $"{SupabaseSettings.Url}/rest/v1/online_lobbies";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "POST", json);
                    created.SetRequestHeader("Prefer", "return=representation");
                    return created;
                },
                completed => request = completed);
            TeamLobbyDto lobby = null;
            using (request)
            {
                if (!IsSuccess(request))
                {
                    onComplete?.Invoke(TeamLobbyOperationResult.Fail(ParseError(request, "Creation du salon 2v2 impossible. Verifiez le SQL Supabase.")));
                    yield break;
                }

                var rows = SupabaseJson.FromArray<TeamLobbyDto>(request.downloadHandler.text);
                lobby = rows.Count > 0 ? rows[0] : null;
            }

            if (lobby == null || string.IsNullOrWhiteSpace(lobby.id))
            {
                onComplete?.Invoke(TeamLobbyOperationResult.Fail("Salon 2v2 introuvable apres creation."));
                yield break;
            }

            bool inserted = false;
            yield return InsertLobbyPlayerRoutine(lobby.id, TeamId.Team1, 0, ok => inserted = ok);
            if (!inserted)
            {
                onComplete?.Invoke(TeamLobbyOperationResult.Fail("Salon cree, mais ajout de l'hote impossible."));
                yield break;
            }

            TeamLobbyOperationResult snapshotResult = null;
            yield return FetchTeamLobbyByIdRoutine(lobby.id, result => snapshotResult = result);
            onComplete?.Invoke(snapshotResult != null && snapshotResult.Success
                ? TeamLobbyOperationResult.Ok($"Salon cree. Code : {lobby.lobby_code}", snapshotResult.Snapshot)
                : TeamLobbyOperationResult.Fail("Salon cree, mais rafraichissement impossible."));
        }

        private IEnumerator JoinTeamLobbyRoutine(string lobbyCode, TeamId team, Action<TeamLobbyOperationResult> onComplete)
        {
            string code = NormalizeLobbyCode(lobbyCode);
            if (string.IsNullOrWhiteSpace(code))
            {
                onComplete?.Invoke(TeamLobbyOperationResult.Fail("Entrez un code salon."));
                yield break;
            }

            TeamLobbyOperationResult fetch = null;
            yield return FetchTeamLobbyByCodeRoutine(code, result => fetch = result);
            if (fetch == null || !fetch.Success || fetch.Snapshot?.Lobby == null)
            {
                onComplete?.Invoke(fetch ?? TeamLobbyOperationResult.Fail("Salon introuvable."));
                yield break;
            }

            var snapshot = fetch.Snapshot;
            if (!string.Equals(snapshot.Lobby.status, "lobby", StringComparison.OrdinalIgnoreCase))
            {
                onComplete?.Invoke(TeamLobbyOperationResult.Fail("Ce salon est deja lance ou ferme."));
                yield break;
            }

            if (snapshot.HasUser(SessionManager.UserId))
            {
                onComplete?.Invoke(TeamLobbyOperationResult.Ok("Vous etes deja dans ce salon.", snapshot));
                yield break;
            }

            if (snapshot.Players != null && snapshot.Players.Count >= 4)
            {
                Debug.LogWarning("[2v2 Lobby] team full reason=salon complet (4 joueurs)");
                onComplete?.Invoke(TeamLobbyOperationResult.Fail("Salon complet."));
                yield break;
            }

            Debug.Log($"[2v2 Lobby] join request user={SessionManager.UserId} team={OnlineSessionTransit.TeamName(team)}");
            Debug.Log($"[2v2 Lobby] team1 count={snapshot.CountTeam(TeamId.Team1)} team2 count={snapshot.CountTeam(TeamId.Team2)}");

            if (snapshot.IsTeamFull(team))
            {
                Debug.LogWarning($"[2v2 Lobby] team full reason={OnlineSessionTransit.TeamName(team)} a deja 2 joueurs");
                onComplete?.Invoke(TeamLobbyOperationResult.Fail(team == TeamId.Team1 ? "Equipe 1 complete." : "Equipe 2 complete."));
                yield break;
            }

            if (!snapshot.TryResolveFreeSlot(team, out int slotIndex))
            {
                Debug.LogWarning($"[2v2 Lobby] team full reason=aucun slot libre pour {OnlineSessionTransit.TeamName(team)}");
                onComplete?.Invoke(TeamLobbyOperationResult.Fail(team == TeamId.Team1 ? "Equipe 1 complete." : "Equipe 2 complete."));
                yield break;
            }

            Debug.Log($"[2v2 Lobby] assigned slot={TeamLobbySnapshot.SlotName(team, slotIndex)}");
            bool inserted = false;
            yield return InsertLobbyPlayerRoutine(snapshot.Lobby.id, team, slotIndex, ok => inserted = ok);
            if (!inserted)
            {
                TeamLobbyOperationResult refreshedAfterInsertFail = null;
                yield return FetchTeamLobbyByIdRoutine(snapshot.Lobby.id, result => refreshedAfterInsertFail = result);
                var retrySnapshot = refreshedAfterInsertFail?.Snapshot;
                if (retrySnapshot != null
                    && !retrySnapshot.HasUser(SessionManager.UserId)
                    && retrySnapshot.TryResolveFreeSlot(team, out int retrySlot)
                    && retrySlot != slotIndex)
                {
                    Debug.Log($"[2v2 Lobby] retry slot={TeamLobbySnapshot.SlotName(team, retrySlot)} after refresh");
                    yield return InsertLobbyPlayerRoutine(snapshot.Lobby.id, team, retrySlot, ok => inserted = ok);
                    slotIndex = retrySlot;
                }
            }

            if (!inserted)
            {
                Debug.LogWarning($"[2v2 Lobby] team full reason=insert rejected team={OnlineSessionTransit.TeamName(team)} slot={TeamLobbySnapshot.SlotName(team, slotIndex)}");
                onComplete?.Invoke(TeamLobbyOperationResult.Fail(TeamJoinFailureMessage(team, snapshot)));
                yield break;
            }

            TeamLobbyOperationResult refreshed = null;
            yield return FetchTeamLobbyByIdRoutine(snapshot.Lobby.id, result => refreshed = result);
            onComplete?.Invoke(refreshed != null && refreshed.Success
                ? TeamLobbyOperationResult.Ok($"Salon rejoint en {TeamDisplayName(team)}.", refreshed.Snapshot)
                : TeamLobbyOperationResult.Fail("Salon rejoint, mais rafraichissement impossible."));
        }

        private IEnumerator LeaveTeamLobbyRoutine(string lobbyId, Action<TeamLobbyOperationResult> onComplete)
        {
            if (string.IsNullOrWhiteSpace(lobbyId))
            {
                onComplete?.Invoke(TeamLobbyOperationResult.Ok("Salon ferme."));
                yield break;
            }

            TeamLobbyOperationResult fetch = null;
            yield return FetchTeamLobbyByIdRoutine(lobbyId, result => fetch = result);
            var lobby = fetch?.Snapshot?.Lobby;
            if (lobby != null && lobby.host_user_id == SessionManager.UserId && string.Equals(lobby.status, "lobby", StringComparison.OrdinalIgnoreCase))
            {
                yield return PatchLobbyRoutine(lobby.id, "cancelled", null, _ => { });
                onComplete?.Invoke(TeamLobbyOperationResult.Ok("Salon annule."));
                yield break;
            }

            string url = $"{SupabaseSettings.Url}/rest/v1/online_lobby_players?lobby_id=eq.{Escape(lobbyId)}&user_id=eq.{Escape(SessionManager.UserId)}";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "DELETE", null);
                    created.SetRequestHeader("Prefer", "return=minimal");
                    return created;
                },
                completed => request = completed);
            request?.Dispose();
            onComplete?.Invoke(TeamLobbyOperationResult.Ok("Salon quitte."));
        }

        private IEnumerator StartTeamLobbyRoutine(string lobbyId, Action<TeamLobbyOperationResult> onComplete)
        {
            TeamLobbyOperationResult fetch = null;
            yield return FetchTeamLobbyByIdRoutine(lobbyId, result => fetch = result);
            var snapshot = fetch?.Snapshot;
            if (snapshot?.Lobby == null)
            {
                onComplete?.Invoke(TeamLobbyOperationResult.Fail("Salon introuvable."));
                yield break;
            }

            if (snapshot.Lobby.host_user_id != SessionManager.UserId)
            {
                onComplete?.Invoke(TeamLobbyOperationResult.Fail("Seul l'hote peut demarrer la partie."));
                yield break;
            }

            if (!string.Equals(snapshot.Lobby.status, "lobby", StringComparison.OrdinalIgnoreCase))
            {
                onComplete?.Invoke(TeamLobbyOperationResult.Fail("Ce salon n'est plus en attente."));
                yield break;
            }

            if (!snapshot.IsFull || !HasFourDistinctPlayers(snapshot))
            {
                onComplete?.Invoke(TeamLobbyOperationResult.Fail("Il faut exactement 4 joueurs differents : 2 par equipe."));
                yield break;
            }

            OnlineMatchDto match = null;
            yield return CreateTeamMatchRoutine(snapshot, created => match = created);
            if (match == null)
            {
                onComplete?.Invoke(TeamLobbyOperationResult.Fail("Creation du match 2v2 impossible."));
                yield break;
            }

            bool patched = false;
            yield return PatchLobbyRoutine(snapshot.Lobby.id, "started", match.id, ok => patched = ok);
            if (!patched)
            {
                onComplete?.Invoke(TeamLobbyOperationResult.Fail("Match cree, mais salon non marque comme demarre."));
                yield break;
            }

            TeamLobbyOperationResult refreshed = null;
            yield return FetchTeamLobbyByIdRoutine(snapshot.Lobby.id, result => refreshed = result);
            if (refreshed?.Snapshot != null)
            {
                refreshed.Snapshot.Match = match;
            }

            onComplete?.Invoke(refreshed != null && refreshed.Success
                ? TeamLobbyOperationResult.Ok("Partie 2v2 lancee.", refreshed.Snapshot)
                : TeamLobbyOperationResult.Ok("Partie 2v2 lancee.", new TeamLobbySnapshot { Lobby = snapshot.Lobby, Players = snapshot.Players, Match = match }));
        }

        private IEnumerator FetchTeamLobbyByCodeRoutine(string lobbyCode, Action<TeamLobbyOperationResult> onComplete)
        {
            string url = $"{SupabaseSettings.Url}/rest/v1/online_lobbies?lobby_code=eq.{Escape(lobbyCode)}&status=in.(lobby,started)&select={LobbySelectWithTime}&order=created_at.desc&limit=1";
            yield return FetchTeamLobbyRoutine(url, onComplete);
        }

        private IEnumerator FetchTeamLobbyByIdRoutine(string lobbyId, Action<TeamLobbyOperationResult> onComplete)
        {
            if (string.IsNullOrWhiteSpace(lobbyId))
            {
                onComplete?.Invoke(TeamLobbyOperationResult.Fail("Salon invalide."));
                yield break;
            }

            string url = $"{SupabaseSettings.Url}/rest/v1/online_lobbies?id=eq.{Escape(lobbyId)}&select={LobbySelectWithTime}&limit=1";
            yield return FetchTeamLobbyRoutine(url, onComplete);
        }

        private IEnumerator FetchTeamLobbyRoutine(string lobbyUrl, Action<TeamLobbyOperationResult> onComplete)
        {
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(lobbyUrl, "GET", null),
                completed => request = completed);
            TeamLobbyDto lobby = null;
            bool shouldTryLegacy = false;
            using (request)
            {
                if (!IsSuccess(request))
                {
                    string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                    string lower = body.ToLowerInvariant();
                    shouldTryLegacy = lower.Contains("time_control_key")
                        || lower.Contains("initial_seconds")
                        || lower.Contains("increment_seconds");
                    if (!shouldTryLegacy)
                    {
                        onComplete?.Invoke(TeamLobbyOperationResult.Fail(ParseError(request, "Salon 2v2 introuvable. Verifiez le SQL Supabase.")));
                        yield break;
                    }
                }
                else
                {
                    var rows = SupabaseJson.FromArray<TeamLobbyDto>(request.downloadHandler.text);
                    lobby = rows.Count > 0 ? rows[0] : null;
                }
            }

            if (shouldTryLegacy)
            {
                string legacyUrl = lobbyUrl.Replace(LobbySelectWithTime, "id,lobby_code,game_kind,match_mode,host_user_id,status,match_id,created_at,updated_at");
                UnityWebRequest fallbackRequest = null;
                yield return SupabaseRequestHelper.SendAuthorizedRequest(
                    () => CreateJsonRequest(legacyUrl, "GET", null),
                    completed => fallbackRequest = completed);
                using (fallbackRequest)
                {
                    if (!IsSuccess(fallbackRequest))
                    {
                        onComplete?.Invoke(TeamLobbyOperationResult.Fail(ParseError(fallbackRequest, "Salon 2v2 introuvable. Verifiez le SQL Supabase.")));
                        yield break;
                    }

                    var rows = SupabaseJson.FromArray<TeamLobbyDto>(fallbackRequest.downloadHandler.text);
                    lobby = rows.Count > 0 ? rows[0] : null;
                }
            }

            if (lobby == null)
            {
                onComplete?.Invoke(TeamLobbyOperationResult.Fail("Salon introuvable."));
                yield break;
            }

            var snapshot = new TeamLobbySnapshot { Lobby = lobby };
            List<TeamLobbyPlayerDto> players = null;
            yield return FetchLobbyPlayersRoutine(lobby.id, fetched => players = fetched);
            snapshot.Players = players ?? new List<TeamLobbyPlayerDto>();

            if (!string.IsNullOrWhiteSpace(lobby.match_id))
            {
                OnlineOperationResult matchResult = null;
                yield return FetchMatchRoutine(lobby.match_id, result => matchResult = result);
                if (matchResult != null && matchResult.Success)
                {
                    snapshot.Match = matchResult.Match;
                }
            }

            onComplete?.Invoke(TeamLobbyOperationResult.Ok("Salon charge.", snapshot));
        }

        private IEnumerator FetchLobbyPlayersRoutine(string lobbyId, Action<List<TeamLobbyPlayerDto>> onComplete)
        {
            string url = $"{SupabaseSettings.Url}/rest/v1/online_lobby_players?lobby_id=eq.{Escape(lobbyId)}&select=id,lobby_id,user_id,username,team_id,slot_index,joined_at,updated_at&order=team_id.asc,slot_index.asc";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null),
                completed => request = completed);
            using (request)
            {
                onComplete?.Invoke(IsSuccess(request) ? SupabaseJson.FromArray<TeamLobbyPlayerDto>(request.downloadHandler.text) : new List<TeamLobbyPlayerDto>());
            }
        }

        private IEnumerator InsertLobbyPlayerRoutine(string lobbyId, TeamId team, int slotIndex, Action<bool> onComplete)
        {
            string username = string.IsNullOrWhiteSpace(SessionManager.Username) ? ShortId(SessionManager.UserId) : SessionManager.Username;
            string json = "{"
                + "\"lobby_id\":\"" + EscapeJson(lobbyId) + "\","
                + "\"user_id\":\"" + EscapeJson(SessionManager.UserId) + "\","
                + "\"username\":\"" + EscapeJson(username) + "\","
                + "\"team_id\":\"" + EscapeJson(OnlineSessionTransit.TeamName(team)) + "\","
                + "\"slot_index\":" + slotIndex + ","
                + "\"updated_at\":\"" + DateTime.UtcNow.ToString("o") + "\""
                + "}";

            string url = $"{SupabaseSettings.Url}/rest/v1/online_lobby_players";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "POST", json);
                    created.SetRequestHeader("Prefer", "return=minimal");
                    return created;
                },
                completed => request = completed);
            using (request)
            {
                bool ok = IsSuccess(request);
                if (!ok)
                {
                    Debug.LogWarning($"[2v2 Lobby] insert failed team={OnlineSessionTransit.TeamName(team)} slot={TeamLobbySnapshot.SlotName(team, slotIndex)} reason={ParseLobbyJoinError(request)}");
                }

                onComplete?.Invoke(ok);
            }
        }

        private IEnumerator PatchLobbyRoutine(string lobbyId, string status, string matchId, Action<bool> onComplete)
        {
            string json = "{\"status\":\"" + EscapeJson(status) + "\",\"updated_at\":\"" + DateTime.UtcNow.ToString("o") + "\"";
            if (!string.IsNullOrWhiteSpace(matchId))
            {
                json += ",\"match_id\":\"" + EscapeJson(matchId) + "\"";
            }

            json += "}";
            string url = $"{SupabaseSettings.Url}/rest/v1/online_lobbies?id=eq.{Escape(lobbyId)}";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "PATCH", json);
                    created.SetRequestHeader("Prefer", "return=minimal");
                    return created;
                },
                completed => request = completed);
            using (request)
            {
                onComplete?.Invoke(IsSuccess(request));
            }
        }

        private IEnumerator CreateTeamMatchRoutine(TeamLobbySnapshot snapshot, Action<OnlineMatchDto> onComplete)
        {
            var team1Player1 = snapshot.GetPlayer(TeamId.Team1, 0);
            var team1Player2 = snapshot.GetPlayer(TeamId.Team1, 1);
            var team2Player1 = snapshot.GetPlayer(TeamId.Team2, 0);
            var team2Player2 = snapshot.GetPlayer(TeamId.Team2, 1);
            var timeControl = TimeControlFromLobby(snapshot.Lobby);
            if (team1Player1 == null || team1Player2 == null || team2Player1 == null || team2Player2 == null)
            {
                onComplete?.Invoke(null);
                yield break;
            }

            string json = "{"
                + "\"game_kind\":\"Quixo\","
                + "\"match_mode\":\"Team2v2\","
                + "\"player1_id\":\"" + EscapeJson(team1Player1.user_id) + "\","
                + "\"player2_id\":\"" + EscapeJson(team2Player1.user_id) + "\","
                + "\"team1_player1_id\":\"" + EscapeJson(team1Player1.user_id) + "\","
                + "\"team1_player2_id\":\"" + EscapeJson(team1Player2.user_id) + "\","
                + "\"team2_player1_id\":\"" + EscapeJson(team2Player1.user_id) + "\","
                + "\"team2_player2_id\":\"" + EscapeJson(team2Player2.user_id) + "\","
                + "\"current_turn_id\":\"" + EscapeJson(team1Player1.user_id) + "\","
                + "\"current_turn_index\":0,"
                + "\"status\":\"active\","
                + "\"time_control_key\":\"" + EscapeJson(timeControl.Key) + "\","
                + "\"initial_seconds\":" + timeControl.InitialSeconds + ","
                + "\"increment_seconds\":" + timeControl.IncrementSeconds + ","
                + "\"updated_at\":\"" + DateTime.UtcNow.ToString("o") + "\""
                + "}";

            string url = $"{SupabaseSettings.Url}/rest/v1/online_matches";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "POST", json);
                    created.SetRequestHeader("Prefer", "return=representation");
                    return created;
                },
                completed => request = completed);
            using (request)
            {
                var rows = IsSuccess(request) ? SupabaseJson.FromArray<OnlineMatchDto>(request.downloadHandler.text) : new List<OnlineMatchDto>();
                onComplete?.Invoke(rows.Count > 0 ? rows[0] : null);
            }
        }

        private IEnumerator MatchmakingLoop(GameKind kind, Action<OnlineOperationResult> onMatched, Action<string> onStatus)
        {
            while (SessionManager.IsOnline)
            {
                OnlineOperationResult result = null;
                yield return TryFindOrCreateMatchRoutine(kind, r => result = r);

                if (result != null && result.Success && result.Match != null)
                {
                    onMatched?.Invoke(result);
                    _matchmakingRoutine = null;
                    yield break;
                }

                onStatus?.Invoke(result != null && !string.IsNullOrWhiteSpace(result.Message)
                    ? result.Message
                    : "Recherche d'un joueur...");
                yield return new WaitForSeconds(MatchmakingPollSeconds);
            }

            _matchmakingRoutine = null;
        }

        private IEnumerator TryFindOrCreateMatchRoutine(GameKind kind, Action<OnlineOperationResult> onComplete)
        {
            string gameKind = OnlineSessionTransit.GameKindName(kind);
            string localUserId = SessionManager.UserId;

            if (string.IsNullOrWhiteSpace(localUserId))
            {
                Debug.LogWarning("[Matchmaking] Refused invalid match creation: reason=local user id is empty");
                onComplete?.Invoke(OnlineOperationResult.Fail("Session utilisateur invalide."));
                yield break;
            }

            // 1) Si ma queue est deja matched avec un match actif, on entre directement.
            //    On verifie AVANT toute ecriture pour eviter qu'un heartbeat n'efface l'etat
            //    matched ecrit par l'autre client.
            MatchmakingQueueDto ownQueue = null;
            yield return FetchOwnQueueRoutine(gameKind, queue => ownQueue = queue);
            if (ownQueue != null && string.Equals(ownQueue.status, "matched", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(ownQueue.match_id))
            {
                OnlineOperationResult matchResult = null;
                yield return FetchMatchRoutine(ownQueue.match_id, result => matchResult = result);
                if (matchResult != null && matchResult.Success && matchResult.Match != null
                    && string.Equals(matchResult.Match.status, "active", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"[Matchmaking] Matched with match={matchResult.Match.id} (own queue already points to active match, p1={matchResult.Match.player1_id} p2={matchResult.Match.player2_id})");
                    onComplete?.Invoke(OnlineOperationResult.Ok("Adversaire trouve.", matchResult.Match));
                    yield break;
                }

                // La queue pointait sur un match termine/annule : on remet en waiting.
                Debug.LogWarning("[Matchmaking] Own queue points to non-active match; resetting to waiting.");
                yield return ResetOwnQueueToWaitingRoutine(gameKind);
                ownQueue.status = "waiting";
                ownQueue.match_id = null;
            }

            // 2) Fallback : un match actif (recent) existe deja avec moi en participant
            //    mais ma queue n'a pas encore ete PATCHee en matched. Le createur le fera ;
            //    en attendant on entre directement dans le match.
            OnlineMatchDto existing = null;
            yield return FetchActiveMatchForLocalRoutine(gameKind, match => existing = match);
            if (existing != null)
            {
                Debug.Log($"[Matchmaking] Matched with match={existing.id} (existing active match found via online_matches, p1={existing.player1_id} p2={existing.player2_id} turn={existing.current_turn_id})");
                // Best-effort : on PATCHe notre row waiting en matched. Echec silencieux ok :
                // si la row n'est pas waiting, le PATCH n'affecte aucune ligne.
                yield return PatchOwnWaitingQueueToMatchedRoutine(gameKind, existing.id);
                onComplete?.Invoke(OnlineOperationResult.Ok("Adversaire trouve.", existing));
                yield break;
            }

            // 3) Heartbeat : on PATCHe uniquement updated_at sur la row encore en waiting.
            //    On NE touche PAS au status ni au match_id : si l'autre client vient juste de
            //    nous claim, le PATCH ne matche aucune ligne (status != waiting) et l'etat
            //    matched est preserve.
            yield return HeartbeatOwnWaitingQueueRoutine(gameKind);

            // 3) Chercher un adversaire VRAI : status=waiting, match_id null, updated_at recent.
            MatchmakingQueueDto opponentQueue = null;
            yield return FetchWaitingOpponentRoutine(gameKind, queue => opponentQueue = queue);
            if (opponentQueue == null)
            {
                onComplete?.Invoke(OnlineOperationResult.Ok("Recherche d'un joueur..."));
                yield break;
            }

            Debug.Log($"[Matchmaking] Found opponent={opponentQueue.user_id} queue={opponentQueue.id}");

            // Tie-break deterministe : le joueur dont la ligne queue.created_at est la plus
            // recente cree le match. Le createur devient player2, l'autre player1.
            if (ShouldWaitForOpponentToCreate(ownQueue, opponentQueue))
            {
                Debug.Log($"[Matchmaking] Waiting queue={(ownQueue != null ? ownQueue.id : "(none)")} (opponent {opponentQueue.user_id} should create the match)");
                onComplete?.Invoke(OnlineOperationResult.Ok("En attente d'un adversaire..."));
                yield break;
            }

            // Re-fetch juste avant CreateMatch : evite la double creation si un autre client
            // l'a deja claimee entre temps.
            MatchmakingQueueDto fresh = null;
            yield return FetchQueueByIdRoutine(opponentQueue.id, q => fresh = q);
            if (!IsOpponentClaimable(fresh, localUserId))
            {
                Debug.LogWarning($"[Matchmaking] Refused invalid match creation: reason=opponent queue {opponentQueue.id} no longer claimable (status={fresh?.status} match_id={fresh?.match_id}).");
                onComplete?.Invoke(OnlineOperationResult.Ok("Recherche d'un joueur..."));
                yield break;
            }

            string player1Id = fresh.user_id;
            string player2Id = localUserId;
            if (!ValidateMatchEndpoints(gameKind, player1Id, player2Id, out string validationError))
            {
                Debug.LogWarning($"[Matchmaking] Refused invalid match creation: reason={validationError}");
                onComplete?.Invoke(OnlineOperationResult.Fail(validationError));
                yield break;
            }

            OnlineMatchDto match = null;
            yield return CreateMatchRoutine(gameKind, player1Id, player2Id, ActiveMatchmakingTimeControl(), created => match = created);
            if (match == null)
            {
                Debug.LogWarning("[Matchmaking] Failed reason=server rejected POST online_matches");
                // On reste en waiting : si l'autre client a deja cree un match, fallback step 2
                // (FetchActiveMatchForLocalRoutine) le rattrapera au prochain tour.
                yield return ResetOwnQueueToWaitingRoutine(gameKind);
                onComplete?.Invoke(OnlineOperationResult.Ok("Recherche d'un joueur..."));
                yield break;
            }

            Debug.Log($"[Matchmaking] Created match={match.id} p1={match.player1_id} p2={match.player2_id} turn={match.current_turn_id}");

            // Claim opponent + me en matched. Si le claim opponent echoue, le fallback step 2
            // chez lui le rattrapera grace a FetchActiveMatchForLocalRoutine.
            yield return PatchQueueRoutine(opponentQueue.id, "matched", match.id, null);
            yield return PatchOwnWaitingQueueToMatchedRoutine(gameKind, match.id);

            onComplete?.Invoke(OnlineOperationResult.Ok("Adversaire trouve.", match));
        }

        private static bool ShouldWaitForOpponentToCreate(MatchmakingQueueDto ownQueue, MatchmakingQueueDto opponentQueue)
        {
            if (ownQueue == null)
            {
                // Pas encore inscrit en queue : c'est moi le plus recent, je cree.
                return false;
            }

            string myCreated = ownQueue.created_at ?? string.Empty;
            string oppCreated = opponentQueue.created_at ?? string.Empty;
            int timeCompare = string.Compare(myCreated, oppCreated, StringComparison.Ordinal);
            if (timeCompare < 0)
            {
                // Je suis arrive avant : l'autre joueur est cense creer.
                return true;
            }

            if (timeCompare > 0)
            {
                // Je suis arrive apres : c'est a moi de creer.
                return false;
            }

            // Egalite parfaite : tie-break lexical par user_id (le plus grand cree).
            return string.Compare(SessionManager.UserId, opponentQueue.user_id, StringComparison.Ordinal) < 0;
        }

        private static bool IsOpponentClaimable(MatchmakingQueueDto opponent, string localUserId)
        {
            if (opponent == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(opponent.user_id) || opponent.user_id == localUserId)
            {
                return false;
            }

            if (!string.Equals(opponent.status, "waiting", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(opponent.match_id))
            {
                return false;
            }

            return IsTimestampFresh(opponent.updated_at, QueueFreshnessSeconds);
        }

        private static bool ValidateMatchEndpoints(string gameKind, string player1Id, string player2Id, out string error)
        {
            if (string.IsNullOrWhiteSpace(gameKind) || (gameKind != "Quixo" && gameKind != "Qomet"))
            {
                error = "Type de partie invalide.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(player1Id) || string.IsNullOrWhiteSpace(player2Id))
            {
                error = "Identifiants joueurs invalides.";
                return false;
            }

            if (player1Id == player2Id)
            {
                error = "Impossible de matcher un joueur avec lui-meme.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool IsTimestampFresh(string isoTimestamp, double maxAgeSeconds)
        {
            if (string.IsNullOrWhiteSpace(isoTimestamp))
            {
                return false;
            }

            if (!DateTime.TryParse(isoTimestamp, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime parsed))
            {
                return false;
            }

            DateTime utc = parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
            return (DateTime.UtcNow - utc).TotalSeconds <= maxAgeSeconds;
        }

        private IEnumerator FetchActiveMatchForLocalRoutine(string gameKind, Action<OnlineMatchDto> onComplete)
        {
            string userId = Escape(SessionManager.UserId);
            string sinceIso = Escape(IsoTimestamp(-ActiveMatchFreshnessSeconds));

            // Si on sait deja que le schema match n'a pas les colonnes cadence, on saute
            // direct au fallback sans filtrer la cadence. Sinon on tente d'abord la version
            // moderne ; en cas d'erreur schema, on retombe sur le legacy.
            if (!_matchSchemaWithoutTime)
            {
                string timeKey = Escape(ActiveMatchmakingTimeControl().Key);
                string url = $"{SupabaseSettings.Url}/rest/v1/online_matches?status=eq.active&game_kind=eq.{Escape(gameKind)}&time_control_key=eq.{timeKey}&or=(player1_id.eq.{userId},player2_id.eq.{userId})&updated_at=gte.{sinceIso}&select={MatchSelectWithTime}&order=created_at.desc&limit=1";
                UnityWebRequest request = null;
                yield return SupabaseRequestHelper.SendAuthorizedRequest(
                    () => CreateJsonRequest(url, "GET", null),
                    completed => request = completed);
                using (request)
                {
                    if (IsSuccess(request))
                    {
                        var rows = SupabaseJson.FromArray<OnlineMatchDto>(request.downloadHandler.text);
                        onComplete?.Invoke(rows.Count > 0 ? rows[0] : null);
                        yield break;
                    }

                    if (IsMissingTimeControlSchemaError(request))
                    {
                        Debug.LogWarning("[Matchmaking] online_matches sans colonnes cadence : fallback legacy active.");
                        _matchSchemaWithoutTime = true;
                    }
                    else
                    {
                        // Erreur reseau / autre : pas de retry, on renvoie null silencieusement.
                        onComplete?.Invoke(null);
                        yield break;
                    }
                }
            }

            // Legacy : sans filtre cadence ni colonnes cadence dans le select.
            string legacyUrl = $"{SupabaseSettings.Url}/rest/v1/online_matches?status=eq.active&game_kind=eq.{Escape(gameKind)}&or=(player1_id.eq.{userId},player2_id.eq.{userId})&updated_at=gte.{sinceIso}&select={MatchSelectNoTime}&order=created_at.desc&limit=1";
            UnityWebRequest legacyRequest = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(legacyUrl, "GET", null),
                completed => legacyRequest = completed);
            using (legacyRequest)
            {
                var rows = IsSuccess(legacyRequest) ? SupabaseJson.FromArray<OnlineMatchDto>(legacyRequest.downloadHandler.text) : new List<OnlineMatchDto>();
                onComplete?.Invoke(rows.Count > 0 ? rows[0] : null);
            }
        }

        private IEnumerator FetchOwnQueueRoutine(string gameKind, Action<MatchmakingQueueDto> onComplete)
        {
            // Cas moderne : DB a les colonnes cadence -> on filtre dessus.
            if (!_queueSchemaWithoutTime)
            {
                string timeKey = Escape(ActiveMatchmakingTimeControl().Key);
                string url = $"{SupabaseSettings.Url}/rest/v1/matchmaking_queue?user_id=eq.{Escape(SessionManager.UserId)}&game_kind=eq.{Escape(gameKind)}&time_control_key=eq.{timeKey}&status=in.(waiting,matched)&select={QueueSelectWithTime}&limit=1";
                UnityWebRequest request = null;
                yield return SupabaseRequestHelper.SendAuthorizedRequest(
                    () => CreateJsonRequest(url, "GET", null),
                    completed => request = completed);
                using (request)
                {
                    if (IsSuccess(request))
                    {
                        var rows = SupabaseJson.FromArray<MatchmakingQueueDto>(request.downloadHandler.text);
                        onComplete?.Invoke(rows.Count > 0 ? rows[0] : null);
                        yield break;
                    }

                    if (IsMissingTimeControlSchemaError(request))
                    {
                        Debug.LogWarning("[Matchmaking] matchmaking_queue sans colonnes cadence : fallback legacy active. Section 13 de SUPABASE_SETUP.md a executer pour avoir le filtrage par cadence.");
                        _queueSchemaWithoutTime = true;
                    }
                    else
                    {
                        onComplete?.Invoke(null);
                        yield break;
                    }
                }
            }

            // Legacy : pas de filtre cadence, select sans colonnes cadence.
            string legacyUrl = $"{SupabaseSettings.Url}/rest/v1/matchmaking_queue?user_id=eq.{Escape(SessionManager.UserId)}&game_kind=eq.{Escape(gameKind)}&status=in.(waiting,matched)&select={QueueSelectLegacy}&limit=1";
            UnityWebRequest legacyRequest = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(legacyUrl, "GET", null),
                completed => legacyRequest = completed);
            using (legacyRequest)
            {
                var rows = IsSuccess(legacyRequest) ? SupabaseJson.FromArray<MatchmakingQueueDto>(legacyRequest.downloadHandler.text) : new List<MatchmakingQueueDto>();
                onComplete?.Invoke(rows.Count > 0 ? rows[0] : null);
            }
        }

        private IEnumerator FetchWaitingOpponentRoutine(string gameKind, Action<MatchmakingQueueDto> onComplete)
        {
            // Anti-fantome : on exige une row waiting, match_id null, updated_at recent.
            string sinceIso = Escape(IsoTimestamp(-QueueFreshnessSeconds));
            string baseFilter = $"game_kind=eq.{Escape(gameKind)}&status=eq.waiting&user_id=neq.{Escape(SessionManager.UserId)}&match_id=is.null&updated_at=gte.{sinceIso}";

            if (!_queueSchemaWithoutTime)
            {
                string timeKey = Escape(ActiveMatchmakingTimeControl().Key);
                string url = $"{SupabaseSettings.Url}/rest/v1/matchmaking_queue?{baseFilter}&time_control_key=eq.{timeKey}&select={QueueSelectWithTime}&order=created_at.asc&limit=1";
                UnityWebRequest request = null;
                yield return SupabaseRequestHelper.SendAuthorizedRequest(
                    () => CreateJsonRequest(url, "GET", null),
                    completed => request = completed);
                using (request)
                {
                    if (IsSuccess(request))
                    {
                        var rows = SupabaseJson.FromArray<MatchmakingQueueDto>(request.downloadHandler.text);
                        onComplete?.Invoke(rows.Count > 0 ? rows[0] : null);
                        yield break;
                    }

                    if (IsMissingTimeControlSchemaError(request))
                    {
                        _queueSchemaWithoutTime = true;
                    }
                    else
                    {
                        onComplete?.Invoke(null);
                        yield break;
                    }
                }
            }

            // Legacy : sans filtre cadence (les anciennes bases matchaient sans cadence,
            // on garde la compat). Documente dans SUPABASE_SETUP.md section 13.
            string legacyUrl = $"{SupabaseSettings.Url}/rest/v1/matchmaking_queue?{baseFilter}&select={QueueSelectLegacy}&order=created_at.asc&limit=1";
            UnityWebRequest legacyRequest = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(legacyUrl, "GET", null),
                completed => legacyRequest = completed);
            using (legacyRequest)
            {
                var rows = IsSuccess(legacyRequest) ? SupabaseJson.FromArray<MatchmakingQueueDto>(legacyRequest.downloadHandler.text) : new List<MatchmakingQueueDto>();
                onComplete?.Invoke(rows.Count > 0 ? rows[0] : null);
            }
        }

        private IEnumerator FetchQueueByIdRoutine(string queueId, Action<MatchmakingQueueDto> onComplete)
        {
            string select = _queueSchemaWithoutTime ? QueueSelectLegacy : QueueSelectWithTime;
            string url = $"{SupabaseSettings.Url}/rest/v1/matchmaking_queue?id=eq.{Escape(queueId)}&select={select}&limit=1";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null),
                completed => request = completed);
            using (request)
            {
                if (IsSuccess(request))
                {
                    var rows = SupabaseJson.FromArray<MatchmakingQueueDto>(request.downloadHandler.text);
                    onComplete?.Invoke(rows.Count > 0 ? rows[0] : null);
                    yield break;
                }

                if (IsMissingTimeControlSchemaError(request))
                {
                    _queueSchemaWithoutTime = true;
                    string legacyUrl = $"{SupabaseSettings.Url}/rest/v1/matchmaking_queue?id=eq.{Escape(queueId)}&select={QueueSelectLegacy}&limit=1";
                    UnityWebRequest legacyRequest = null;
                    yield return SupabaseRequestHelper.SendAuthorizedRequest(
                        () => CreateJsonRequest(legacyUrl, "GET", null),
                        completed => legacyRequest = completed);
                    using (legacyRequest)
                    {
                        var rows = IsSuccess(legacyRequest) ? SupabaseJson.FromArray<MatchmakingQueueDto>(legacyRequest.downloadHandler.text) : new List<MatchmakingQueueDto>();
                        onComplete?.Invoke(rows.Count > 0 ? rows[0] : null);
                    }

                    yield break;
                }

                onComplete?.Invoke(null);
            }
        }

        private IEnumerator FetchInvitesWithFallbackRoutine(string url, string legacyUrl, Action<List<MatchInviteDto>> onComplete)
        {
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null),
                completed => request = completed);
            using (request)
            {
                if (IsSuccess(request))
                {
                    onComplete?.Invoke(SupabaseJson.FromArray<MatchInviteDto>(request.downloadHandler.text));
                    yield break;
                }

                string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                if (!IsMissingTimeControlSchemaError(request))
                {
                    Debug.LogWarning($"[Invite] Load invites failed: {ParseInviteError(request, "Chargement invitations impossible.")}");
                    onComplete?.Invoke(new List<MatchInviteDto>());
                    yield break;
                }
            }

            UnityWebRequest fallbackRequest = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(legacyUrl, "GET", null),
                completed => fallbackRequest = completed);
            using (fallbackRequest)
            {
                if (!IsSuccess(fallbackRequest))
                {
                    Debug.LogWarning($"[Invite] Load invites legacy failed: {ParseInviteError(fallbackRequest, "Chargement invitations impossible.")}");
                }

                onComplete?.Invoke(IsSuccess(fallbackRequest)
                    ? SupabaseJson.FromArray<MatchInviteDto>(fallbackRequest.downloadHandler.text)
                    : new List<MatchInviteDto>());
            }
        }

        private IEnumerator CancelAllOwnQueuesRoutine(string gameKind)
        {
            string nowIso = DateTime.UtcNow.ToString("o");
            string json = "{\"status\":\"cancelled\",\"match_id\":null,\"updated_at\":\"" + EscapeJson(nowIso) + "\"}";
            string url = $"{SupabaseSettings.Url}/rest/v1/matchmaking_queue?user_id=eq.{Escape(SessionManager.UserId)}&game_kind=eq.{Escape(gameKind)}&status=in.(waiting,matched)";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "PATCH", json);
                    created.SetRequestHeader("Prefer", "return=minimal");
                    return created;
                },
                completed => request = completed);
            request?.Dispose();
        }

        private IEnumerator EnsureFreshOwnQueueRoutine(string gameKind)
        {
            // On force created_at = now pour repartir sur des bases propres : merge-duplicates
            // mettra a jour created_at vers l'instant present meme si une ligne existait deja.
            yield return UpsertOwnQueueRoutine(gameKind, "waiting", null, refreshCreatedAt: true);
        }

        private IEnumerator UpsertOwnQueueRoutine(string gameKind, string status, string matchId, bool refreshCreatedAt)
        {
            string nowIso = DateTime.UtcNow.ToString("o");
            string url = $"{SupabaseSettings.Url}/rest/v1/matchmaking_queue?on_conflict=user_id,game_kind";

            // Premier essai : avec cadence si on n'est pas deja en mode legacy.
            if (!_queueSchemaWithoutTime)
            {
                string modernJson = BuildQueueUpsertJson(gameKind, status, matchId, refreshCreatedAt, nowIso, withTime: true);
                UnityWebRequest request = null;
                yield return SupabaseRequestHelper.SendAuthorizedRequest(
                    () =>
                    {
                        var created = CreateJsonRequest(url, "POST", modernJson);
                        created.SetRequestHeader("Prefer", "resolution=merge-duplicates,return=minimal");
                        return created;
                    },
                    completed => request = completed);
                using (request)
                {
                    if (IsSuccess(request))
                    {
                        yield break;
                    }

                    if (IsMissingTimeControlSchemaError(request))
                    {
                        Debug.LogWarning("[Matchmaking] matchmaking_queue sans colonnes cadence : fallback legacy upsert. Section 13 de SUPABASE_SETUP.md a executer pour activer le filtrage par cadence.");
                        _queueSchemaWithoutTime = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[Matchmaking] Failed reason={ParseError(request, "Upsert matchmaking_queue echoue.")}");
                        yield break;
                    }
                }
            }

            // Fallback : on retente sans les colonnes cadence pour ne pas bloquer les anciennes bases.
            string legacyJson = BuildQueueUpsertJson(gameKind, status, matchId, refreshCreatedAt, nowIso, withTime: false);
            UnityWebRequest legacyRequest = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "POST", legacyJson);
                    created.SetRequestHeader("Prefer", "resolution=merge-duplicates,return=minimal");
                    return created;
                },
                completed => legacyRequest = completed);
            using (legacyRequest)
            {
                if (!IsSuccess(legacyRequest))
                {
                    Debug.LogWarning($"[Matchmaking] Failed reason={ParseError(legacyRequest, "Upsert matchmaking_queue (legacy) echoue.")}");
                }
            }
        }

        private string BuildQueueUpsertJson(string gameKind, string status, string matchId, bool refreshCreatedAt, string nowIso, bool withTime)
        {
            var sb = new StringBuilder(256);
            sb.Append('{');
            sb.Append("\"user_id\":\"").Append(EscapeJson(SessionManager.UserId)).Append("\",");
            sb.Append("\"game_kind\":\"").Append(EscapeJson(gameKind)).Append("\",");
            if (withTime)
            {
                var timeControl = ActiveMatchmakingTimeControl();
                sb.Append("\"time_control_key\":\"").Append(EscapeJson(timeControl.Key)).Append("\",");
                sb.Append("\"initial_seconds\":").Append(timeControl.InitialSeconds).Append(',');
                sb.Append("\"increment_seconds\":").Append(timeControl.IncrementSeconds).Append(',');
            }
            sb.Append("\"status\":\"").Append(EscapeJson(status)).Append("\",");
            sb.Append("\"updated_at\":\"").Append(nowIso).Append('\"');
            if (refreshCreatedAt)
            {
                sb.Append(",\"created_at\":\"").Append(nowIso).Append('\"');
            }
            sb.Append(",\"match_id\":");
            if (string.IsNullOrWhiteSpace(matchId))
            {
                sb.Append("null");
            }
            else
            {
                sb.Append('\"').Append(EscapeJson(matchId)).Append('\"');
            }
            sb.Append('}');
            return sb.ToString();
        }

        // Heartbeat sur la row encore en waiting : on PATCHe seulement updated_at.
        // Si l'autre client vient de nous claim (status='matched'), le filtre status=eq.waiting
        // ne matche aucune ligne et le PATCH est un no-op : l'etat matched est preserve.
        private IEnumerator HeartbeatOwnWaitingQueueRoutine(string gameKind)
        {
            string nowIso = DateTime.UtcNow.ToString("o");
            string json = "{\"updated_at\":\"" + EscapeJson(nowIso) + "\"}";
            string url = $"{SupabaseSettings.Url}/rest/v1/matchmaking_queue?user_id=eq.{Escape(SessionManager.UserId)}&game_kind=eq.{Escape(gameKind)}&status=eq.waiting";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "PATCH", json);
                    created.SetRequestHeader("Prefer", "return=minimal");
                    return created;
                },
                completed => request = completed);
            request?.Dispose();
        }

        // Passe ma row waiting -> matched sans toucher au time_control_key existant.
        // No-op silencieux si la row n'est plus waiting (ex : un autre client l'a annulee).
        private IEnumerator PatchOwnWaitingQueueToMatchedRoutine(string gameKind, string matchId)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                yield break;
            }

            string nowIso = DateTime.UtcNow.ToString("o");
            var sb = new StringBuilder(128);
            sb.Append('{');
            sb.Append("\"status\":\"matched\",");
            sb.Append("\"match_id\":\"").Append(EscapeJson(matchId)).Append("\",");
            sb.Append("\"updated_at\":\"").Append(EscapeJson(nowIso)).Append('\"');
            sb.Append('}');
            string url = $"{SupabaseSettings.Url}/rest/v1/matchmaking_queue?user_id=eq.{Escape(SessionManager.UserId)}&game_kind=eq.{Escape(gameKind)}&status=eq.waiting";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "PATCH", sb.ToString());
                    created.SetRequestHeader("Prefer", "return=minimal");
                    return created;
                },
                completed => request = completed);
            request?.Dispose();
        }

        // Remet ma row matched -> waiting (apres avoir constate que le match cible est
        // termine/annule, ou que le POST online_matches a echoue). On n'utilise pas l'upsert
        // pour ne pas reecrire created_at/cadence si la row existe deja.
        private IEnumerator ResetOwnQueueToWaitingRoutine(string gameKind)
        {
            string nowIso = DateTime.UtcNow.ToString("o");
            string json = "{\"status\":\"waiting\",\"match_id\":null,\"updated_at\":\"" + EscapeJson(nowIso) + "\"}";
            string url = $"{SupabaseSettings.Url}/rest/v1/matchmaking_queue?user_id=eq.{Escape(SessionManager.UserId)}&game_kind=eq.{Escape(gameKind)}";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "PATCH", json);
                    created.SetRequestHeader("Prefer", "return=minimal");
                    return created;
                },
                completed => request = completed);
            request?.Dispose();
        }

        private static string IsoTimestamp(double offsetSeconds)
        {
            return DateTime.UtcNow.AddSeconds(offsetSeconds).ToString("o");
        }

        private TurnTimerSettings.TimeControlOption ActiveMatchmakingTimeControl()
        {
            return _matchmakingTimeControl ?? TurnTimerSettings.SelectedOption;
        }

        private static TurnTimerSettings.TimeControlOption TimeControlFromInvite(MatchInviteDto invite)
        {
            return invite == null
                ? TurnTimerSettings.SelectedOption
                : TurnTimerSettings.OptionForNetwork(invite.time_control_key, invite.initial_seconds, invite.increment_seconds);
        }

        private static TurnTimerSettings.TimeControlOption TimeControlFromLobby(TeamLobbyDto lobby)
        {
            return lobby == null
                ? TurnTimerSettings.SelectedOption
                : TurnTimerSettings.OptionForNetwork(lobby.time_control_key, lobby.initial_seconds, lobby.increment_seconds);
        }

        private IEnumerator PatchQueueRoutine(string queueId, string status, string matchId, Action<bool> onComplete)
        {
            string json = "{\"status\":\"" + EscapeJson(status) + "\",\"updated_at\":\"" + DateTime.UtcNow.ToString("o") + "\"";
            if (!string.IsNullOrWhiteSpace(matchId))
            {
                json += ",\"match_id\":\"" + EscapeJson(matchId) + "\"";
            }

            json += "}";
            string url = $"{SupabaseSettings.Url}/rest/v1/matchmaking_queue?id=eq.{Escape(queueId)}";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "PATCH", json);
                    created.SetRequestHeader("Prefer", "return=minimal");
                    return created;
                },
                completed => request = completed);
            using (request)
            {
                onComplete?.Invoke(IsSuccess(request));
            }
        }

        private IEnumerator CancelMatchmakingRoutine(GameKind kind, Action<OnlineOperationResult> onComplete)
        {
            string gameKind = OnlineSessionTransit.GameKindName(kind);
            // On annule UNIQUEMENT les rows waiting : on ne touche pas a une row matched dont
            // le match peut encore etre actif (l'autre client est peut-etre dans GameplayScene).
            string json = "{\"status\":\"cancelled\",\"updated_at\":\"" + DateTime.UtcNow.ToString("o") + "\"}";
            string url = $"{SupabaseSettings.Url}/rest/v1/matchmaking_queue?user_id=eq.{Escape(SessionManager.UserId)}&game_kind=eq.{Escape(gameKind)}&status=eq.waiting";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "PATCH", json);
                    created.SetRequestHeader("Prefer", "return=minimal");
                    return created;
                },
                completed => request = completed);
            using (request)
            {
                if (IsSuccess(request))
                {
                    Debug.Log($"[Matchmaking] Cancelled queue user={SessionManager.UserId} game={gameKind}");
                    onComplete?.Invoke(OnlineOperationResult.Ok("Recherche annulee."));
                }
                else
                {
                    string reason = ParseError(request, "Annulation impossible.");
                    Debug.LogWarning($"[Matchmaking] Failed reason=cancel rejected ({reason})");
                    onComplete?.Invoke(OnlineOperationResult.Fail(reason));
                }
            }
        }

        private IEnumerator CreateMatchRoutine(string gameKind, string player1Id, string player2Id, TurnTimerSettings.TimeControlOption timeControl, Action<OnlineMatchDto> onComplete)
        {
            timeControl ??= TurnTimerSettings.SelectedOption;
            if (!ValidateMatchEndpoints(gameKind, player1Id, player2Id, out string validationError))
            {
                Debug.LogWarning($"[Invite] Refused invalid match creation: reason={validationError} (gameKind={gameKind} p1={player1Id} p2={player2Id})");
                onComplete?.Invoke(null);
                yield break;
            }

            UnityWebRequest request = null;
            yield return PostMatchRequest(gameKind, player1Id, player2Id, timeControl, withTime: true, completed => request = completed);
            using (request)
            {
                if (IsSuccess(request))
                {
                    var rows = SupabaseJson.FromArray<OnlineMatchDto>(request.downloadHandler.text);
                    onComplete?.Invoke(rows.Count > 0 ? rows[0] : null);
                    yield break;
                }

                if (IsMissingTimeControlSchemaError(request))
                {
                    Debug.LogWarning("[Invite] Missing SQL migration for timer columns; creating match without cadence fields.");
                    UnityWebRequest legacyRequest = null;
                    yield return PostMatchRequest(gameKind, player1Id, player2Id, timeControl, withTime: false, completed => legacyRequest = completed);
                    using (legacyRequest)
                    {
                        var rows = IsSuccess(legacyRequest)
                            ? SupabaseJson.FromArray<OnlineMatchDto>(legacyRequest.downloadHandler.text)
                            : new List<OnlineMatchDto>();
                        if (rows.Count == 0 && legacyRequest != null)
                        {
                            Debug.LogWarning($"[Invite] Failed reason={ParseInviteError(legacyRequest, "Creation du match impossible.")}");
                        }

                        onComplete?.Invoke(rows.Count > 0 ? rows[0] : null);
                    }

                    yield break;
                }

                Debug.LogWarning($"[Invite] Failed reason={ParseInviteError(request, "Creation du match impossible.")}");
                onComplete?.Invoke(null);
            }
        }

        private IEnumerator FetchMatchRoutine(string matchId, Action<OnlineOperationResult> onComplete)
        {
            string url = $"{SupabaseSettings.Url}/rest/v1/online_matches?id=eq.{Escape(matchId)}&select={MatchSelectWithTime}&limit=1";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null),
                completed => request = completed);
            bool shouldTryNoTime = false;
            bool shouldTryLegacy = false;
            using (request)
            {
                if (!IsSuccess(request))
                {
                    string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                    string lower = body.ToLowerInvariant();
                    shouldTryNoTime = lower.Contains("time_control_key")
                        || lower.Contains("initial_seconds")
                        || lower.Contains("increment_seconds");
                    shouldTryLegacy = lower.Contains("match_mode")
                        || lower.Contains("team1_player1_id")
                        || lower.Contains("current_turn_index");
                    if (!shouldTryNoTime && !shouldTryLegacy)
                    {
                        onComplete?.Invoke(OnlineOperationResult.Fail(ParseError(request, "Match introuvable.")));
                        yield break;
                    }
                }
                else
                {
                    var rows = SupabaseJson.FromArray<OnlineMatchDto>(request.downloadHandler.text);
                    onComplete?.Invoke(rows.Count > 0
                        ? OnlineOperationResult.Ok("Match charge.", rows[0])
                        : OnlineOperationResult.Fail("Match introuvable."));
                    yield break;
                }
            }

            if (shouldTryNoTime)
            {
                string noTimeUrl = $"{SupabaseSettings.Url}/rest/v1/online_matches?id=eq.{Escape(matchId)}&select={MatchSelectNoTime}&limit=1";
                UnityWebRequest noTimeRequest = null;
                yield return SupabaseRequestHelper.SendAuthorizedRequest(
                    () => CreateJsonRequest(noTimeUrl, "GET", null),
                    completed => noTimeRequest = completed);
                using (noTimeRequest)
                {
                    if (IsSuccess(noTimeRequest))
                    {
                        var rows = SupabaseJson.FromArray<OnlineMatchDto>(noTimeRequest.downloadHandler.text);
                        onComplete?.Invoke(rows.Count > 0
                            ? OnlineOperationResult.Ok("Match charge.", rows[0])
                            : OnlineOperationResult.Fail("Match introuvable."));
                        yield break;
                    }

                    string body = noTimeRequest.downloadHandler != null ? noTimeRequest.downloadHandler.text : string.Empty;
                    shouldTryLegacy = body.ToLowerInvariant().Contains("match_mode")
                        || body.ToLowerInvariant().Contains("team1_player1_id")
                        || body.ToLowerInvariant().Contains("current_turn_index");
                    if (!shouldTryLegacy)
                    {
                        onComplete?.Invoke(OnlineOperationResult.Fail(ParseError(noTimeRequest, "Match introuvable.")));
                        yield break;
                    }
                }
            }

            string fallbackUrl = $"{SupabaseSettings.Url}/rest/v1/online_matches?id=eq.{Escape(matchId)}&select={MatchSelectLegacy}&limit=1";
            UnityWebRequest fallbackRequest = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(fallbackUrl, "GET", null),
                completed => fallbackRequest = completed);
            using (fallbackRequest)
            {
                if (!IsSuccess(fallbackRequest))
                {
                    onComplete?.Invoke(OnlineOperationResult.Fail(ParseError(fallbackRequest, "Match introuvable.")));
                    yield break;
                }

                var rows = SupabaseJson.FromArray<OnlineMatchDto>(fallbackRequest.downloadHandler.text);
                onComplete?.Invoke(rows.Count > 0
                    ? OnlineOperationResult.Ok("Match charge.", rows[0])
                    : OnlineOperationResult.Fail("Match introuvable."));
            }
        }

        private IEnumerator FetchMovesAfterRoutine(string matchId, int lastMoveNumber, Action<List<OnlineMoveDto>> onComplete)
        {
            string url = $"{SupabaseSettings.Url}/rest/v1/online_moves?match_id=eq.{Escape(matchId)}&move_number=gt.{lastMoveNumber}&select=id,match_id,player_id,move_number,move_payload,created_at&order=move_number.asc";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null),
                completed => request = completed);
            using (request)
            {
                onComplete?.Invoke(IsSuccess(request) ? SupabaseJson.FromArray<OnlineMoveDto>(request.downloadHandler.text) : new List<OnlineMoveDto>());
            }
        }

        private IEnumerator SubmitMoveRoutine(
            OnlineMatchDto match,
            OnlineMovePayload payload,
            string nextTurnId,
            string winnerId,
            string winnerTeam,
            int currentTurnIndex,
            Action<OnlineOperationResult> onComplete)
        {
            int nextMoveNumber = 1;
            yield return FetchLastMoveNumberRoutine(match.id, number => nextMoveNumber = number + 1);

            var move = new OnlineMoveCreateRequest
            {
                match_id = match.id,
                player_id = SessionManager.UserId,
                move_number = nextMoveNumber,
                move_payload = payload
            };

            string url = $"{SupabaseSettings.Url}/rest/v1/online_moves";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "POST", JsonUtility.ToJson(move));
                    created.SetRequestHeader("Prefer", "return=minimal");
                    return created;
                },
                completed => request = completed);
            using (request)
            {
                if (!IsSuccess(request))
                {
                    onComplete?.Invoke(OnlineOperationResult.Fail(ParseError(request, "Envoi du coup impossible.")));
                    yield break;
                }
            }

            Debug.Log($"[Online] Sent move #{nextMoveNumber} match={match.id} action={payload.action} dir={payload.direction} from=({payload.fromRow},{payload.fromCol}) to=({payload.toRow},{payload.toCol}) sel=({payload.selectedRow},{payload.selectedCol}) nextTurn={nextTurnId}");

            string status = string.IsNullOrWhiteSpace(winnerId) && string.IsNullOrWhiteSpace(winnerTeam) ? "active" : "finished";
            yield return PatchMatchRoutine(match.id, nextTurnId, status, winnerId, winnerTeam, currentTurnIndex, onComplete);
        }

        private IEnumerator FetchLastMoveNumberRoutine(string matchId, Action<int> onComplete)
        {
            string url = $"{SupabaseSettings.Url}/rest/v1/online_moves?match_id=eq.{Escape(matchId)}&select=move_number&order=move_number.desc&limit=1";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null),
                completed => request = completed);
            using (request)
            {
                var rows = IsSuccess(request) ? SupabaseJson.FromArray<OnlineMoveDto>(request.downloadHandler.text) : new List<OnlineMoveDto>();
                onComplete?.Invoke(rows.Count > 0 ? rows[0].move_number : 0);
            }
        }

        private IEnumerator PatchMatchRoutine(string matchId, string currentTurnId, string status, string winnerId, string winnerTeam, int currentTurnIndex, Action<OnlineOperationResult> onComplete)
        {
            string json = "{\"current_turn_id\":\"" + EscapeJson(currentTurnId) + "\",\"status\":\"" + EscapeJson(status) + "\",\"updated_at\":\"" + DateTime.UtcNow.ToString("o") + "\"";
            if (!string.IsNullOrWhiteSpace(winnerId))
            {
                json += ",\"winner_id\":\"" + EscapeJson(winnerId) + "\"";
            }

            if (!string.IsNullOrWhiteSpace(winnerTeam))
            {
                json += ",\"winner_team\":\"" + EscapeJson(winnerTeam) + "\"";
            }

            if (currentTurnIndex >= 0)
            {
                json += ",\"current_turn_index\":" + currentTurnIndex;
            }

            json += "}";
            string url = $"{SupabaseSettings.Url}/rest/v1/online_matches?id=eq.{Escape(matchId)}";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "PATCH", json);
                    created.SetRequestHeader("Prefer", "return=representation");
                    return created;
                },
                completed => request = completed);
            using (request)
            {
                if (!IsSuccess(request))
                {
                    onComplete?.Invoke(OnlineOperationResult.Fail(ParseError(request, "Mise a jour du match impossible.")));
                    yield break;
                }

                var rows = SupabaseJson.FromArray<OnlineMatchDto>(request.downloadHandler.text);
                onComplete?.Invoke(rows.Count > 0
                    ? OnlineOperationResult.Ok("Match mis a jour.", rows[0])
                    : OnlineOperationResult.Ok("Match mis a jour."));
            }
        }

        private void CancelLocalMatchmakingLoop()
        {
            if (_matchmakingRoutine != null)
            {
                StopCoroutine(_matchmakingRoutine);
                _matchmakingRoutine = null;
            }
        }

        private static bool HasFourDistinctPlayers(TeamLobbySnapshot snapshot)
        {
            if (snapshot == null || snapshot.Players == null || snapshot.Players.Count != 4)
            {
                return false;
            }

            var ids = new HashSet<string>();
            foreach (var player in snapshot.Players)
            {
                if (player == null || string.IsNullOrWhiteSpace(player.user_id))
                {
                    return false;
                }

                if (!ids.Add(player.user_id))
                {
                    return false;
                }
            }

            return snapshot.GetPlayer(TeamId.Team1, 0) != null
                && snapshot.GetPlayer(TeamId.Team1, 1) != null
                && snapshot.GetPlayer(TeamId.Team2, 0) != null
                && snapshot.GetPlayer(TeamId.Team2, 1) != null;
        }

        private static string GenerateLobbyCode()
        {
            string raw = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
            return raw;
        }

        private static string NormalizeLobbyCode(string code)
        {
            return (code ?? string.Empty).Trim().Replace(" ", string.Empty).ToUpperInvariant();
        }

        private static string TeamDisplayName(TeamId team)
        {
            return team == TeamId.Team1 ? "equipe 1" : team == TeamId.Team2 ? "equipe 2" : "equipe inconnue";
        }

        private static string TeamJoinFailureMessage(TeamId team, TeamLobbySnapshot snapshot)
        {
            if (snapshot != null && snapshot.Players != null && snapshot.Players.Count >= 4)
            {
                return "Salon complet.";
            }

            if (snapshot != null && snapshot.IsTeamFull(team))
            {
                return team == TeamId.Team1 ? "Equipe 1 complete." : "Equipe 2 complete.";
            }

            return "Impossible de rejoindre cette place. Rafraichissez le salon ou verifiez le SQL Supabase (section 15).";
        }

        private static string ParseLobbyJoinError(UnityWebRequest request)
        {
            string body = request?.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            string lower = body.ToLowerInvariant();
            if (lower.Contains("duplicate key") || lower.Contains("23505"))
            {
                return "slot deja occupe (contrainte unique)";
            }

            if (lower.Contains("violates row-level security") || lower.Contains("42501"))
            {
                return "policy RLS bloque l'insertion";
            }

            return ParseError(request, "insertion impossible");
        }

        private static string ShortId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "joueur";
            }

            return value.Length <= 8 ? value : value.Substring(0, 8);
        }

        private static bool EnsureOnline(Action<OnlineOperationResult> onComplete)
        {
            if (!SupabaseSettings.IsConfigured)
            {
                onComplete?.Invoke(OnlineOperationResult.Fail("Service en ligne indisponible."));
                return false;
            }

            if (!SessionManager.IsOnline)
            {
                onComplete?.Invoke(OnlineOperationResult.Fail("Connectez-vous pour jouer en ligne."));
                return false;
            }

            return true;
        }

        private static UnityWebRequest CreateJsonRequest(string url, string method, string json)
        {
            return SupabaseRequestHelper.CreateAuthorizedJsonRequest(url, method, json);
        }

        private static bool IsSuccess(UnityWebRequest request)
        {
            return SupabaseRequestHelper.IsSuccess(request);
        }

        private static string ParseError(UnityWebRequest request, string fallback)
        {
            return SupabaseRequestHelper.ParseError(request, fallback);
        }

        private static string Escape(string value)
        {
            return UnityWebRequest.EscapeURL(value ?? string.Empty);
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private IEnumerator PostInviteRequest(string friendUserId, string gameKind, TurnTimerSettings.TimeControlOption timeControl, bool withTime, Action<UnityWebRequest> onComplete)
        {
            string json = withTime
                ? JsonUtility.ToJson(new MatchInviteCreateRequest
                {
                    from_user_id = SessionManager.UserId,
                    to_user_id = friendUserId,
                    game_kind = gameKind,
                    status = "pending",
                    time_control_key = timeControl.Key,
                    initial_seconds = timeControl.InitialSeconds,
                    increment_seconds = timeControl.IncrementSeconds
                })
                : "{"
                    + "\"from_user_id\":\"" + EscapeJson(SessionManager.UserId) + "\","
                    + "\"to_user_id\":\"" + EscapeJson(friendUserId) + "\","
                    + "\"game_kind\":\"" + EscapeJson(gameKind) + "\","
                    + "\"status\":\"pending\""
                    + "}";

            string url = $"{SupabaseSettings.Url}/rest/v1/match_invites";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "POST", json);
                    created.SetRequestHeader("Prefer", "return=representation");
                    return created;
                },
                completed => request = completed);
            onComplete?.Invoke(request);
        }

        private IEnumerator PostMatchRequest(string gameKind, string player1Id, string player2Id, TurnTimerSettings.TimeControlOption timeControl, bool withTime, Action<UnityWebRequest> onComplete)
        {
            string json = withTime
                ? JsonUtility.ToJson(new OnlineMatchCreateRequest
                {
                    game_kind = gameKind,
                    player1_id = player1Id,
                    player2_id = player2Id,
                    current_turn_id = player1Id,
                    status = "active",
                    time_control_key = timeControl.Key,
                    initial_seconds = timeControl.InitialSeconds,
                    increment_seconds = timeControl.IncrementSeconds
                })
                : "{"
                    + "\"game_kind\":\"" + EscapeJson(gameKind) + "\","
                    + "\"player1_id\":\"" + EscapeJson(player1Id) + "\","
                    + "\"player2_id\":\"" + EscapeJson(player2Id) + "\","
                    + "\"current_turn_id\":\"" + EscapeJson(player1Id) + "\","
                    + "\"status\":\"active\""
                    + "}";

            string url = $"{SupabaseSettings.Url}/rest/v1/online_matches";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "POST", json);
                    created.SetRequestHeader("Prefer", "return=representation");
                    return created;
                },
                completed => request = completed);
            onComplete?.Invoke(request);
        }

        private static void CompleteInviteCreate(UnityWebRequest request, Action<OnlineOperationResult> onComplete)
        {
            var invites = SupabaseJson.FromArray<MatchInviteDto>(request.downloadHandler.text);
            var created = invites.Count > 0 ? invites[0] : null;
            Debug.Log($"[Invite] Created invite id={created?.id ?? "(unknown)"}");
            onComplete?.Invoke(OnlineOperationResult.Ok("Invitation envoyee.", null, created));
        }

        private static List<MatchInviteDto> FilterFreshPendingInvites(List<MatchInviteDto> invites)
        {
            var filtered = new List<MatchInviteDto>();
            if (invites == null)
            {
                return filtered;
            }

            foreach (var invite in invites)
            {
                if (invite == null || invite.status != "pending")
                {
                    continue;
                }

                if (!IsTimestampFresh(invite.created_at, PendingInviteFreshnessSeconds))
                {
                    continue;
                }

                filtered.Add(invite);
            }

            return filtered;
        }

        private static bool IsMissingTimeControlSchemaError(UnityWebRequest request)
        {
            if (request == null)
            {
                return false;
            }

            string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            string lower = body.ToLowerInvariant();
            return lower.Contains("time_control_key")
                || lower.Contains("initial_seconds")
                || lower.Contains("increment_seconds")
                || lower.Contains("pgrst204")
                || (lower.Contains("column") && lower.Contains("does not exist") && lower.Contains("time"));
        }

        private static string ParseInviteError(UnityWebRequest request, string fallback)
        {
            if (IsMissingTimeControlSchemaError(request))
            {
                return "Migration Supabase timer manquante. Executez le SQL documente dans SUPABASE_SETUP.md (section 13).";
            }

            return ParseError(request, fallback);
        }
    }
}
