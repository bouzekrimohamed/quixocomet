using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using QuixoUnity.Auth;
using UnityEngine;
using UnityEngine.Networking;

namespace QuixoUnity.Social
{
    public sealed class FriendService : MonoBehaviour
    {
        private string _lastError;

        public void SendFriendRequestByUsername(string username, Action<SocialOperationResult> onComplete)
        {
            if (!EnsureOnline(onComplete))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                onComplete?.Invoke(SocialOperationResult.Fail("Entrez un username."));
                return;
            }

            StartCoroutine(SendFriendRequestRoutine(username.Trim(), onComplete));
        }

        public void LoadSummary(Action<SocialOperationResult> onComplete)
        {
            if (!EnsureOnline(onComplete))
            {
                return;
            }

            StartCoroutine(LoadSummaryRoutine(onComplete));
        }

        public void AcceptRequest(string requestId, Action<SocialOperationResult> onComplete)
        {
            UpdateRequestStatus(requestId, "accepted", onComplete);
        }

        public void RejectRequest(string requestId, Action<SocialOperationResult> onComplete)
        {
            UpdateRequestStatus(requestId, "rejected", onComplete);
        }

        private IEnumerator SendFriendRequestRoutine(string username, Action<SocialOperationResult> onComplete)
        {
            SocialOperationResult lookup = null;
            yield return FindProfileByUsernameRoutine(username, result => lookup = result);

            if (lookup == null || !lookup.Success || lookup.Profile == null)
            {
                onComplete?.Invoke(lookup ?? SocialOperationResult.Fail("Utilisateur introuvable."));
                yield break;
            }

            if (lookup.Profile.id == SessionManager.UserId)
            {
                onComplete?.Invoke(SocialOperationResult.Fail("Vous ne pouvez pas vous ajouter vous-meme."));
                yield break;
            }

            bool relationExists = false;
            string relationMessage = string.Empty;
            yield return CheckExistingRelationRoutine(lookup.Profile.id, (exists, message) =>
            {
                relationExists = exists;
                relationMessage = message;
            });

            if (relationExists)
            {
                onComplete?.Invoke(SocialOperationResult.Fail(relationMessage));
                yield break;
            }

            yield return CreateFriendRequestRoutine(lookup.Profile, onComplete);
        }

        private IEnumerator FindProfileByUsernameRoutine(string username, Action<SocialOperationResult> onComplete)
        {
            string escapedUsername = UnityWebRequest.EscapeURL(NormalizeUsernameForLookup(username));
            string url = $"{SupabaseSettings.Url}/rest/v1/profiles?username=eq.{escapedUsername}&select=id,username,display_name,created_at&limit=1";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null),
                completed => request = completed);
            using (request)
            {
                if (!IsSuccess(request))
                {
                    onComplete?.Invoke(SocialOperationResult.Fail(ParseError(request, "Recherche impossible.")));
                    yield break;
                }

                var profiles = SupabaseJson.FromArray<ProfileDto>(request.downloadHandler.text);
                if (profiles.Count == 0)
                {
                    onComplete?.Invoke(SocialOperationResult.Fail("Utilisateur introuvable."));
                    yield break;
                }

                onComplete?.Invoke(SocialOperationResult.Ok("Profil trouve.", null, profiles[0]));
            }
        }

        private IEnumerator CheckExistingRelationRoutine(string targetProfileId, Action<bool, string> onComplete)
        {
            string currentUserId = UnityWebRequest.EscapeURL(SessionManager.UserId);
            string targetId = UnityWebRequest.EscapeURL(targetProfileId);
            string url = $"{SupabaseSettings.Url}/rest/v1/friends?or=(and(requester_id.eq.{currentUserId},receiver_id.eq.{targetId}),and(requester_id.eq.{targetId},receiver_id.eq.{currentUserId}))&select=id,requester_id,receiver_id,status,created_at&limit=1";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null),
                completed => request = completed);
            using (request)
            {
                if (!IsSuccess(request))
                {
                    onComplete?.Invoke(true, ParseError(request, "Verification impossible."));
                    yield break;
                }

                var relations = SupabaseJson.FromArray<FriendDto>(request.downloadHandler.text);
                if (relations.Count == 0)
                {
                    onComplete?.Invoke(false, string.Empty);
                    yield break;
                }

                string message = relations[0].status == "accepted"
                    ? "Vous etes deja amis."
                    : "Demande deja existante.";
                onComplete?.Invoke(true, message);
            }
        }

        private IEnumerator CreateFriendRequestRoutine(ProfileDto receiver, Action<SocialOperationResult> onComplete)
        {
            var payload = new FriendCreateRequest
            {
                requester_id = SessionManager.UserId,
                receiver_id = receiver.id,
                status = "pending"
            };

            string url = $"{SupabaseSettings.Url}/rest/v1/friends";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "POST", UnityEngine.JsonUtility.ToJson(payload));
                    created.SetRequestHeader("Prefer", "return=minimal");
                    return created;
                },
                completed => request = completed);
            using (request)
            {
                if (!IsSuccess(request))
                {
                    onComplete?.Invoke(SocialOperationResult.Fail(ParseError(request, "Demande deja envoyee ou impossible.")));
                    yield break;
                }

                onComplete?.Invoke(SocialOperationResult.Ok("Demande envoyee."));
            }
        }

        private void UpdateRequestStatus(string requestId, string status, Action<SocialOperationResult> onComplete)
        {
            if (!EnsureOnline(onComplete))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(requestId))
            {
                onComplete?.Invoke(SocialOperationResult.Fail("Demande invalide."));
                return;
            }

            StartCoroutine(UpdateRequestStatusRoutine(requestId, status, onComplete));
        }

        private IEnumerator UpdateRequestStatusRoutine(string requestId, string status, Action<SocialOperationResult> onComplete)
        {
            var payload = new FriendStatusUpdate { status = status };
            string id = UnityWebRequest.EscapeURL(requestId);
            string userId = UnityWebRequest.EscapeURL(SessionManager.UserId);
            string url = $"{SupabaseSettings.Url}/rest/v1/friends?id=eq.{id}&receiver_id=eq.{userId}";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "PATCH", UnityEngine.JsonUtility.ToJson(payload));
                    created.SetRequestHeader("Prefer", "return=minimal");
                    return created;
                },
                completed => request = completed);
            using (request)
            {
                if (!IsSuccess(request))
                {
                    onComplete?.Invoke(SocialOperationResult.Fail(ParseError(request, "Mise a jour impossible.")));
                    yield break;
                }

                onComplete?.Invoke(SocialOperationResult.Ok(status == "accepted" ? "Demande acceptee." : "Demande refusee."));
            }
        }

        private IEnumerator LoadSummaryRoutine(Action<SocialOperationResult> onComplete)
        {
            string userId = UnityWebRequest.EscapeURL(SessionManager.UserId);
            string requestsUrl = $"{SupabaseSettings.Url}/rest/v1/friends?receiver_id=eq.{userId}&status=eq.pending&select=id,requester_id,receiver_id,status,created_at";
            string friendsUrl = $"{SupabaseSettings.Url}/rest/v1/friends?status=eq.accepted&or=(requester_id.eq.{userId},receiver_id.eq.{userId})&select=id,requester_id,receiver_id,status,created_at";

            List<FriendDto> requests = null;
            List<FriendDto> friends = null;

            _lastError = string.Empty;
            yield return FetchFriendsRoutine(requestsUrl, result => requests = result);
            yield return FetchFriendsRoutine(friendsUrl, result => friends = result);

            if (requests == null || friends == null)
            {
                onComplete?.Invoke(SocialOperationResult.Fail(string.IsNullOrWhiteSpace(_lastError) ? "Impossible de charger les amis." : _lastError));
                yield break;
            }

            var summary = new FriendSummary();
            var profileIds = new HashSet<string>();
            foreach (var request in requests)
            {
                if (!string.IsNullOrWhiteSpace(request.requester_id))
                {
                    profileIds.Add(request.requester_id);
                }
            }

            foreach (var friend in friends)
            {
                string otherId = friend.requester_id == SessionManager.UserId ? friend.receiver_id : friend.requester_id;
                if (!string.IsNullOrWhiteSpace(otherId))
                {
                    profileIds.Add(otherId);
                }
            }

            Dictionary<string, ProfileDto> profiles = null;
            yield return FetchProfilesRoutine(profileIds, result => profiles = result);
            profiles ??= new Dictionary<string, ProfileDto>();

            foreach (var request in requests)
            {
                summary.Requests.Add(ToListItem(request, request.requester_id, profiles));
            }

            foreach (var friend in friends)
            {
                string otherId = friend.requester_id == SessionManager.UserId ? friend.receiver_id : friend.requester_id;
                summary.Friends.Add(ToListItem(friend, otherId, profiles));
            }

            onComplete?.Invoke(SocialOperationResult.Ok("Amis charges.", summary));
        }

        private IEnumerator FetchFriendsRoutine(string url, Action<List<FriendDto>> onComplete)
        {
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null),
                completed => request = completed);
            using (request)
            {
                if (!IsSuccess(request))
                {
                    _lastError = ParseError(request, "Impossible de charger les amis.");
                    onComplete?.Invoke(null);
                    yield break;
                }

                onComplete?.Invoke(SupabaseJson.FromArray<FriendDto>(request.downloadHandler.text));
            }
        }

        private IEnumerator FetchProfilesRoutine(HashSet<string> ids, Action<Dictionary<string, ProfileDto>> onComplete)
        {
            var result = new Dictionary<string, ProfileDto>();
            if (ids == null || ids.Count == 0)
            {
                onComplete?.Invoke(result);
                yield break;
            }

            string joinedIds = string.Join(",", ids);
            string url = $"{SupabaseSettings.Url}/rest/v1/profiles?id=in.({joinedIds})&select=id,username,display_name,created_at";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null),
                completed => request = completed);
            using (request)
            {
                if (!IsSuccess(request))
                {
                    _lastError = ParseError(request, "Impossible de charger les profils amis.");
                    onComplete?.Invoke(result);
                    yield break;
                }

                foreach (var profile in SupabaseJson.FromArray<ProfileDto>(request.downloadHandler.text))
                {
                    if (!string.IsNullOrWhiteSpace(profile.id))
                    {
                        result[profile.id] = profile;
                    }
                }

                onComplete?.Invoke(result);
            }
        }

        private static FriendListItem ToListItem(FriendDto friend, string profileId, Dictionary<string, ProfileDto> profiles)
        {
            profiles.TryGetValue(profileId ?? string.Empty, out ProfileDto profile);
            return new FriendListItem
            {
                RequestId = friend.id,
                UserId = profileId,
                Username = profile != null ? profile.username : ShortId(profileId),
                DisplayName = profile != null ? profile.display_name : string.Empty,
                Status = friend.status
            };
        }

        private static bool EnsureOnline(Action<SocialOperationResult> onComplete)
        {
            if (!SupabaseSettings.IsConfigured)
            {
                onComplete?.Invoke(SocialOperationResult.Fail("Supabase n'est pas configure."));
                return false;
            }

            if (!SessionManager.IsOnline)
            {
                onComplete?.Invoke(SocialOperationResult.Fail("Connectez-vous pour utiliser les amis."));
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

        private static string ShortId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "joueur inconnu";
            }

            return value.Length <= 8 ? value : value.Substring(0, 8);
        }

        private static string NormalizeUsernameForLookup(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (char c in raw.Trim().ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }
    }
}
