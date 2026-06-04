using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using QuixoUnity.Auth;
using QuixoUnity.Core;
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

        private Coroutine _matchmakingRoutine;

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
            _matchmakingRoutine = StartCoroutine(MatchmakingBootstrapAndLoop(kind, onMatched, onStatus));
        }

        private IEnumerator MatchmakingBootstrapAndLoop(GameKind kind, Action<OnlineOperationResult> onMatched, Action<string> onStatus)
        {
            string gameKind = OnlineSessionTransit.GameKindName(kind);
            Debug.Log($"[Matchmaking] Start user={SessionManager.UserId} game={gameKind}");
            // 1. On annule toute ancienne ligne (waiting/matched) pour partir propre.
            yield return CancelAllOwnQueuesRoutine(gameKind);
            // 2. On (re)pose une ligne waiting toute fraiche : created_at = now, match_id = null.
            yield return EnsureFreshOwnQueueRoutine(gameKind);
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
            Action<OnlineOperationResult> onComplete)
        {
            if (!EnsureOnline(onComplete))
            {
                return;
            }

            if (match == null || payload == null)
            {
                onComplete?.Invoke(OnlineOperationResult.Fail("Coup online invalide."));
                return;
            }

            StartCoroutine(SubmitMoveRoutine(match, payload, nextTurnId, winnerId, onComplete));
        }

        public void UpdateMatchFinished(OnlineMatchDto match, string winnerId, Action<OnlineOperationResult> onComplete)
        {
            if (!EnsureOnline(onComplete) || match == null)
            {
                return;
            }

            StartCoroutine(PatchMatchRoutine(match.id, match.current_turn_id, "finished", winnerId, onComplete));
        }

        private IEnumerator SendInviteRoutine(string friendUserId, GameKind kind, Action<OnlineOperationResult> onComplete)
        {
            var payload = new MatchInviteCreateRequest
            {
                from_user_id = SessionManager.UserId,
                to_user_id = friendUserId,
                game_kind = OnlineSessionTransit.GameKindName(kind),
                status = "pending"
            };

            string url = $"{SupabaseSettings.Url}/rest/v1/match_invites";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "POST", JsonUtility.ToJson(payload));
                    created.SetRequestHeader("Prefer", "return=representation");
                    return created;
                },
                completed => request = completed);
            using (request)
            {
                if (!IsSuccess(request))
                {
                    onComplete?.Invoke(OnlineOperationResult.Fail(ParseError(request, "Invitation impossible.")));
                    yield break;
                }

                var invites = SupabaseJson.FromArray<MatchInviteDto>(request.downloadHandler.text);
                onComplete?.Invoke(OnlineOperationResult.Ok("Invitation envoyee.", null, invites.Count > 0 ? invites[0] : null));
            }
        }

        private IEnumerator LoadPendingInvitesRoutine(Action<List<MatchInviteDto>> onComplete)
        {
            string userId = Escape(SessionManager.UserId);
            string url = $"{SupabaseSettings.Url}/rest/v1/match_invites?to_user_id=eq.{userId}&status=eq.pending&select=id,from_user_id,to_user_id,game_kind,status,match_id,created_at,updated_at&order=created_at.desc";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null),
                completed => request = completed);
            using (request)
            {
                onComplete?.Invoke(IsSuccess(request) ? SupabaseJson.FromArray<MatchInviteDto>(request.downloadHandler.text) : new List<MatchInviteDto>());
            }
        }

        private IEnumerator LoadAcceptedSentInvitesRoutine(Action<List<MatchInviteDto>> onComplete)
        {
            string userId = Escape(SessionManager.UserId);
            string url = $"{SupabaseSettings.Url}/rest/v1/match_invites?from_user_id=eq.{userId}&status=eq.accepted&match_id=not.is.null&select=id,from_user_id,to_user_id,game_kind,status,match_id,created_at,updated_at&order=updated_at.desc&limit=1";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null),
                completed => request = completed);
            using (request)
            {
                onComplete?.Invoke(IsSuccess(request) ? SupabaseJson.FromArray<MatchInviteDto>(request.downloadHandler.text) : new List<MatchInviteDto>());
            }
        }

        private IEnumerator AcceptInviteRoutine(MatchInviteDto invite, Action<OnlineOperationResult> onComplete)
        {
            OnlineMatchDto match = null;
            yield return CreateMatchRoutine(invite.game_kind, invite.from_user_id, invite.to_user_id, created => match = created);
            if (match == null)
            {
                onComplete?.Invoke(OnlineOperationResult.Fail("Creation du match impossible."));
                yield break;
            }

            yield return UpdateInviteRoutine(invite.id, "accepted", match.id, result =>
            {
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
                    : OnlineOperationResult.Fail(ParseError(request, "Mise a jour invitation impossible.")));
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

            // Heartbeat : on rafraichit updated_at pour que les autres ne nous considerent pas
            // comme une ligne fantome. On NE TOUCHE PAS a created_at ici.
            yield return UpsertOwnQueueRoutine(gameKind, "waiting", null, refreshCreatedAt: false);

            // 1) Fallback : un match actif (recent) existe deja avec moi en participant.
            OnlineMatchDto existing = null;
            yield return FetchActiveMatchForLocalRoutine(gameKind, match => existing = match);
            if (existing != null)
            {
                Debug.Log($"[Matchmaking] Matched queue match={existing.id} (existing active match found, p1={existing.player1_id} p2={existing.player2_id} turn={existing.current_turn_id})");
                yield return UpsertOwnQueueRoutine(gameKind, "matched", existing.id, refreshCreatedAt: false);
                onComplete?.Invoke(OnlineOperationResult.Ok("Adversaire trouve.", existing));
                yield break;
            }

            // 2) Si ma queue est marquee matched, on suit le match -- mais SEULEMENT s'il est actif.
            MatchmakingQueueDto ownQueue = null;
            yield return FetchOwnQueueRoutine(gameKind, queue => ownQueue = queue);
            if (ownQueue != null && ownQueue.status == "matched" && !string.IsNullOrWhiteSpace(ownQueue.match_id))
            {
                OnlineOperationResult matchResult = null;
                yield return FetchMatchRoutine(ownQueue.match_id, result => matchResult = result);
                if (matchResult != null && matchResult.Success && matchResult.Match != null
                    && string.Equals(matchResult.Match.status, "active", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"[Matchmaking] Matched queue match={matchResult.Match.id} (own queue points to active match)");
                    onComplete?.Invoke(OnlineOperationResult.Ok("Adversaire trouve.", matchResult.Match));
                    yield break;
                }

                // La queue pointait sur un match termine/annule : on nettoie.
                Debug.LogWarning("[Matchmaking] Own queue points to non-active match; resetting to waiting.");
                yield return UpsertOwnQueueRoutine(gameKind, "waiting", null, refreshCreatedAt: false);
                ownQueue.status = "waiting";
                ownQueue.match_id = null;
            }

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
            yield return CreateMatchRoutine(gameKind, player1Id, player2Id, created => match = created);
            if (match == null)
            {
                Debug.LogWarning("[Matchmaking] Refused invalid match creation: reason=server rejected POST online_matches");
                yield return UpsertOwnQueueRoutine(gameKind, "waiting", null, refreshCreatedAt: false);
                onComplete?.Invoke(OnlineOperationResult.Ok("Recherche d'un joueur..."));
                yield break;
            }

            Debug.Log($"[Matchmaking] Created match={match.id} p1={match.player1_id} p2={match.player2_id} turn={match.current_turn_id}");

            // Claim opponent + me en matched. Si le claim opponent echoue, le fallback step 1
            // chez lui le rattrapera grace a FetchActiveMatchForLocalRoutine.
            yield return PatchQueueRoutine(opponentQueue.id, "matched", match.id, null);
            yield return UpsertOwnQueueRoutine(gameKind, "matched", match.id, refreshCreatedAt: false);

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
            string url = $"{SupabaseSettings.Url}/rest/v1/online_matches?status=eq.active&game_kind=eq.{Escape(gameKind)}&or=(player1_id.eq.{userId},player2_id.eq.{userId})&updated_at=gte.{sinceIso}&select=id,game_kind,player1_id,player2_id,current_turn_id,status,winner_id,created_at,updated_at&order=created_at.desc&limit=1";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null),
                completed => request = completed);
            using (request)
            {
                var rows = IsSuccess(request) ? SupabaseJson.FromArray<OnlineMatchDto>(request.downloadHandler.text) : new List<OnlineMatchDto>();
                onComplete?.Invoke(rows.Count > 0 ? rows[0] : null);
            }
        }

        private IEnumerator FetchOwnQueueRoutine(string gameKind, Action<MatchmakingQueueDto> onComplete)
        {
            string url = $"{SupabaseSettings.Url}/rest/v1/matchmaking_queue?user_id=eq.{Escape(SessionManager.UserId)}&game_kind=eq.{Escape(gameKind)}&status=in.(waiting,matched)&select=id,user_id,game_kind,status,match_id,created_at,updated_at&limit=1";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null),
                completed => request = completed);
            using (request)
            {
                var rows = IsSuccess(request) ? SupabaseJson.FromArray<MatchmakingQueueDto>(request.downloadHandler.text) : new List<MatchmakingQueueDto>();
                onComplete?.Invoke(rows.Count > 0 ? rows[0] : null);
            }
        }

        private IEnumerator FetchWaitingOpponentRoutine(string gameKind, Action<MatchmakingQueueDto> onComplete)
        {
            // Anti-fantome : on exige une row waiting, match_id null, updated_at recent.
            string sinceIso = Escape(IsoTimestamp(-QueueFreshnessSeconds));
            string url = $"{SupabaseSettings.Url}/rest/v1/matchmaking_queue?game_kind=eq.{Escape(gameKind)}&status=eq.waiting&user_id=neq.{Escape(SessionManager.UserId)}&match_id=is.null&updated_at=gte.{sinceIso}&select=id,user_id,game_kind,status,match_id,created_at,updated_at&order=created_at.asc&limit=1";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null),
                completed => request = completed);
            using (request)
            {
                var rows = IsSuccess(request) ? SupabaseJson.FromArray<MatchmakingQueueDto>(request.downloadHandler.text) : new List<MatchmakingQueueDto>();
                onComplete?.Invoke(rows.Count > 0 ? rows[0] : null);
            }
        }

        private IEnumerator FetchQueueByIdRoutine(string queueId, Action<MatchmakingQueueDto> onComplete)
        {
            string url = $"{SupabaseSettings.Url}/rest/v1/matchmaking_queue?id=eq.{Escape(queueId)}&select=id,user_id,game_kind,status,match_id,created_at,updated_at&limit=1";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null),
                completed => request = completed);
            using (request)
            {
                var rows = IsSuccess(request) ? SupabaseJson.FromArray<MatchmakingQueueDto>(request.downloadHandler.text) : new List<MatchmakingQueueDto>();
                onComplete?.Invoke(rows.Count > 0 ? rows[0] : null);
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
            var sb = new StringBuilder(256);
            sb.Append('{');
            sb.Append("\"user_id\":\"").Append(EscapeJson(SessionManager.UserId)).Append("\",");
            sb.Append("\"game_kind\":\"").Append(EscapeJson(gameKind)).Append("\",");
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
            string json = sb.ToString();

            string url = $"{SupabaseSettings.Url}/rest/v1/matchmaking_queue?on_conflict=user_id,game_kind";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "POST", json);
                    created.SetRequestHeader("Prefer", "resolution=merge-duplicates,return=minimal");
                    return created;
                },
                completed => request = completed);
            request?.Dispose();
        }

        private static string IsoTimestamp(double offsetSeconds)
        {
            return DateTime.UtcNow.AddSeconds(offsetSeconds).ToString("o");
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
                onComplete?.Invoke(IsSuccess(request)
                    ? OnlineOperationResult.Ok("Recherche annulee.")
                    : OnlineOperationResult.Fail(ParseError(request, "Annulation impossible.")));
            }
        }

        private IEnumerator CreateMatchRoutine(string gameKind, string player1Id, string player2Id, Action<OnlineMatchDto> onComplete)
        {
            if (!ValidateMatchEndpoints(gameKind, player1Id, player2Id, out string validationError))
            {
                Debug.LogWarning($"[Matchmaking] Refused invalid match creation: reason={validationError} (gameKind={gameKind} p1={player1Id} p2={player2Id})");
                onComplete?.Invoke(null);
                yield break;
            }

            var payload = new OnlineMatchCreateRequest
            {
                game_kind = gameKind,
                player1_id = player1Id,
                player2_id = player2Id,
                current_turn_id = player1Id,
                status = "active"
            };

            string url = $"{SupabaseSettings.Url}/rest/v1/online_matches";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "POST", JsonUtility.ToJson(payload));
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

        private IEnumerator FetchMatchRoutine(string matchId, Action<OnlineOperationResult> onComplete)
        {
            string url = $"{SupabaseSettings.Url}/rest/v1/online_matches?id=eq.{Escape(matchId)}&select=id,game_kind,player1_id,player2_id,current_turn_id,status,winner_id,created_at,updated_at&limit=1";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null),
                completed => request = completed);
            using (request)
            {
                if (!IsSuccess(request))
                {
                    onComplete?.Invoke(OnlineOperationResult.Fail(ParseError(request, "Match introuvable.")));
                    yield break;
                }

                var rows = SupabaseJson.FromArray<OnlineMatchDto>(request.downloadHandler.text);
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

        private IEnumerator SubmitMoveRoutine(OnlineMatchDto match, OnlineMovePayload payload, string nextTurnId, string winnerId, Action<OnlineOperationResult> onComplete)
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

            string status = string.IsNullOrWhiteSpace(winnerId) ? "active" : "finished";
            yield return PatchMatchRoutine(match.id, nextTurnId, status, winnerId, onComplete);
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

        private IEnumerator PatchMatchRoutine(string matchId, string currentTurnId, string status, string winnerId, Action<OnlineOperationResult> onComplete)
        {
            string json = "{\"current_turn_id\":\"" + EscapeJson(currentTurnId) + "\",\"status\":\"" + EscapeJson(status) + "\",\"updated_at\":\"" + DateTime.UtcNow.ToString("o") + "\"";
            if (!string.IsNullOrWhiteSpace(winnerId))
            {
                json += ",\"winner_id\":\"" + EscapeJson(winnerId) + "\"";
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
    }
}
