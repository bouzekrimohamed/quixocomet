using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using QuixoUnity.Auth;
using QuixoUnity.Social;
using UnityEngine;
using UnityEngine.Networking;

namespace QuixoUnity.Online
{
    public sealed class OnlinePresenceService : MonoBehaviour
    {
        private const float HeartbeatInterval = 10f;
        private const double OnlineThresholdSeconds = 30.0;

        private Coroutine _heartbeatRoutine;

        public void StartPresence()
        {
            if (!SessionManager.IsOnline)
            {
                return;
            }

            StopPresence();
            _heartbeatRoutine = StartCoroutine(HeartbeatLoop());
        }

        public void StopPresence()
        {
            if (_heartbeatRoutine != null)
            {
                StopCoroutine(_heartbeatRoutine);
                _heartbeatRoutine = null;
            }
        }

        public void SendHeartbeat(Action<bool> onComplete = null)
        {
            if (!SessionManager.IsOnline)
            {
                onComplete?.Invoke(false);
                return;
            }

            StartCoroutine(SendHeartbeatRoutine(onComplete));
        }

        public void GetFriendsPresence(IEnumerable<FriendListItem> friends, Action<Dictionary<string, bool>> onComplete)
        {
            if (!SessionManager.IsOnline)
            {
                onComplete?.Invoke(new Dictionary<string, bool>());
                return;
            }

            var ids = new HashSet<string>();
            if (friends != null)
            {
                foreach (var friend in friends)
                {
                    if (friend != null && !string.IsNullOrWhiteSpace(friend.UserId))
                    {
                        ids.Add(friend.UserId);
                    }
                }
            }

            StartCoroutine(GetPresenceRoutine(ids, onComplete));
        }

        public static bool IsUserOnline(string lastSeenAt)
        {
            if (string.IsNullOrWhiteSpace(lastSeenAt) || !DateTime.TryParse(lastSeenAt, out var parsed))
            {
                return false;
            }

            DateTime utc = parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
            return (DateTime.UtcNow - utc).TotalSeconds <= OnlineThresholdSeconds;
        }

        private IEnumerator HeartbeatLoop()
        {
            while (SessionManager.IsOnline)
            {
                yield return SendHeartbeatRoutine(null);
                yield return new WaitForSeconds(HeartbeatInterval);
            }
        }

        private IEnumerator SendHeartbeatRoutine(Action<bool> onComplete)
        {
            var payload = new PresenceUpsertRequest
            {
                user_id = SessionManager.UserId,
                username = SessionManager.Username,
                status = "online",
                last_seen_at = DateTime.UtcNow.ToString("o")
            };

            string url = $"{SupabaseSettings.Url}/rest/v1/user_presence?on_conflict=user_id";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () =>
                {
                    var created = CreateJsonRequest(url, "POST", JsonUtility.ToJson(payload));
                    created.SetRequestHeader("Prefer", "resolution=merge-duplicates,return=minimal");
                    return created;
                },
                completed => request = completed);
            using (request)
            {
                onComplete?.Invoke(IsSuccess(request));
            }
        }

        private IEnumerator GetPresenceRoutine(HashSet<string> ids, Action<Dictionary<string, bool>> onComplete)
        {
            var result = new Dictionary<string, bool>();
            if (ids == null || ids.Count == 0)
            {
                onComplete?.Invoke(result);
                yield break;
            }

            string joined = string.Join(",", ids);
            string url = $"{SupabaseSettings.Url}/rest/v1/user_presence?user_id=in.({joined})&select=user_id,username,status,last_seen_at";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null),
                completed => request = completed);
            using (request)
            {
                if (!IsSuccess(request))
                {
                    onComplete?.Invoke(result);
                    yield break;
                }

                foreach (var presence in SupabaseJson.FromArray<UserPresenceDto>(request.downloadHandler.text))
                {
                    if (!string.IsNullOrWhiteSpace(presence.user_id))
                    {
                        result[presence.user_id] = string.Equals(presence.status, "online", StringComparison.OrdinalIgnoreCase)
                            && IsUserOnline(presence.last_seen_at);
                    }
                }

                onComplete?.Invoke(result);
            }
        }

        private static UnityWebRequest CreateJsonRequest(string url, string method, string json)
        {
            return SupabaseRequestHelper.CreateAuthorizedJsonRequest(url, method, json);
        }

        private static bool IsSuccess(UnityWebRequest request)
        {
            return SupabaseRequestHelper.IsSuccess(request);
        }
    }
}
