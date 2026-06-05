using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace QuixoUnity.Auth
{
    public static class SupabaseRequestHelper
    {
        public const string SessionExpiredMessage = "Session expiree. Reconnectez-vous.";
        public const string EmailNotConfirmedMessage = "Veuillez confirmer votre email avant de vous connecter.";
        public const string EmailRateLimitMessage = "Service email temporairement limite. Reessayez plus tard.";

        public static string MapAuthError(UnityWebRequest request, string fallback, bool isSignup)
        {
            if (request == null)
            {
                return fallback;
            }

            if (IsEmailRateLimit(request))
            {
                return EmailRateLimitMessage;
            }

            if (!isSignup && IsEmailNotConfirmed(request))
            {
                return EmailNotConfirmedMessage;
            }

            return ParseError(request, fallback);
        }

        public static bool IsEmailRateLimit(UnityWebRequest request)
        {
            if (request == null)
            {
                return false;
            }

            string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            return IsEmailRateLimitBody(body, request.responseCode);
        }

        public static bool IsEmailNotConfirmed(UnityWebRequest request)
        {
            if (request == null)
            {
                return false;
            }

            string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            return IsEmailNotConfirmedBody(body);
        }

        private static bool IsEmailRateLimitBody(string body, long responseCode)
        {
            if (string.IsNullOrWhiteSpace(body) && responseCode != 429)
            {
                return false;
            }

            string lowerBody = body.ToLowerInvariant();
            return lowerBody.Contains("email rate limit")
                || lowerBody.Contains("over_email_send_rate_limit")
                || lowerBody.Contains("rate limit exceeded")
                || lowerBody.Contains("too many requests")
                || lowerBody.Contains("smtp")
                || lowerBody.Contains("error sending")
                || lowerBody.Contains("unable to send")
                || lowerBody.Contains("mail send")
                || (responseCode == 429 && (lowerBody.Contains("email") || lowerBody.Contains("mail") || string.IsNullOrWhiteSpace(body)));
        }

        private static bool IsEmailNotConfirmedBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            string lowerBody = body.ToLowerInvariant();
            return lowerBody.Contains("email not confirmed")
                || lowerBody.Contains("email_not_confirmed")
                || lowerBody.Contains("confirm your email")
                || lowerBody.Contains("email address not confirmed");
        }

        public static IEnumerator SendAuthorizedRequest(Func<UnityWebRequest> createRequest, Action<UnityWebRequest> onComplete)
        {
            if (createRequest == null)
            {
                onComplete?.Invoke(null);
                yield break;
            }

            var request = createRequest();
            yield return request.SendWebRequest();

            if (!ShouldRefreshSession(request))
            {
                onComplete?.Invoke(request);
                yield break;
            }

            bool refreshed = false;
            yield return RefreshSessionRoutine(result => refreshed = result);
            if (!refreshed)
            {
                onComplete?.Invoke(request);
                yield break;
            }

            request.Dispose();
            request = createRequest();
            yield return request.SendWebRequest();
            onComplete?.Invoke(request);
        }

        public static IEnumerator RefreshSessionRoutine(Action<bool> onComplete)
        {
            if (!SupabaseSettings.IsConfigured || string.IsNullOrWhiteSpace(SessionManager.RefreshToken))
            {
                SessionManager.ClearSession();
                onComplete?.Invoke(false);
                yield break;
            }

            var payload = new RefreshTokenRequest
            {
                refresh_token = SessionManager.RefreshToken
            };

            string url = $"{SupabaseSettings.Url}/auth/v1/token?grant_type=refresh_token";
            using var request = CreateJsonRequest(url, "POST", JsonUtility.ToJson(payload), string.Empty);
            yield return request.SendWebRequest();

            if (!IsSuccess(request))
            {
                SessionManager.ClearSession();
                onComplete?.Invoke(false);
                yield break;
            }

            var response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
            if (response == null || string.IsNullOrWhiteSpace(response.access_token))
            {
                SessionManager.ClearSession();
                onComplete?.Invoke(false);
                yield break;
            }

            SessionManager.UpdateSession(response);
            onComplete?.Invoke(true);
        }

        public static UnityWebRequest CreateJsonRequest(string url, string method, string json, string accessToken)
        {
            var request = new UnityWebRequest(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer()
            };

            if (json != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            request.SetRequestHeader("apikey", SupabaseSettings.PublicAnonKey);
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            }

            return request;
        }

        public static UnityWebRequest CreateAuthorizedJsonRequest(string url, string method, string json)
        {
            return CreateJsonRequest(url, method, json, SessionManager.AccessToken);
        }

        public static bool IsSuccess(UnityWebRequest request)
        {
            return request != null
                && request.result == UnityWebRequest.Result.Success
                && request.responseCode >= 200
                && request.responseCode < 300;
        }

        public static bool ShouldRefreshSession(UnityWebRequest request)
        {
            if (request == null)
            {
                return false;
            }

            if (request.responseCode == 401 || request.responseCode == 403)
            {
                return true;
            }

            string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            string lowerBody = body.ToLowerInvariant();
            return lowerBody.Contains("pgrst303")
                || lowerBody.Contains("jwt expired")
                || lowerBody.Contains("invalid jwt")
                || lowerBody.Contains("jwt");
        }

        public static string ParseError(UnityWebRequest request, string fallback)
        {
            if (request == null)
            {
                return fallback;
            }

            if (request.result == UnityWebRequest.Result.ConnectionError
                || request.result == UnityWebRequest.Result.DataProcessingError)
            {
                return "Connexion internet ou serveur indisponible.";
            }

            string body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            string lowerBody = body.ToLowerInvariant();
            if (request.responseCode == 401 || IsJwtExpiredBody(body))
            {
                return SessionExpiredMessage;
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return fallback;
            }

            if (lowerBody.Contains("violates row-level security") || lowerBody.Contains("42501"))
            {
                return "Action non autorisee par Supabase. Verifiez les policies RLS.";
            }

            if (request.responseCode == 403)
            {
                return "Action non autorisee par Supabase. Verifiez votre session ou les policies RLS.";
            }

            if (lowerBody.Contains("duplicate key") || lowerBody.Contains("23505"))
            {
                return "Vous etes deja amis ou une demande existe deja.";
            }

            var parsed = JsonUtility.FromJson<SupabaseErrorResponse>(body);
            if (parsed != null)
            {
                string message = FirstNonEmpty(parsed.message, parsed.msg, parsed.error_description, parsed.error);
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return ToHumanMessage(message, fallback);
                }
            }

            return fallback;
        }

        private static bool IsJwtExpiredBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            string lowerBody = body.ToLowerInvariant();
            return lowerBody.Contains("pgrst303")
                || lowerBody.Contains("jwt expired")
                || lowerBody.Contains("invalid jwt");
        }

        private static string ToHumanMessage(string message, string fallback)
        {
            string lower = message.ToLowerInvariant();
            if (lower.Contains("jwt expired") || lower.Contains("invalid jwt"))
            {
                return SessionExpiredMessage;
            }

            if (lower.Contains("duplicate key"))
            {
                return "Vous etes deja amis ou une demande existe deja.";
            }

            if (lower.Contains("invalid login") || lower.Contains("invalid credentials"))
            {
                return "Identifiants incorrects.";
            }

            if (IsEmailNotConfirmedBody(message))
            {
                return EmailNotConfirmedMessage;
            }

            if (IsEmailRateLimitBody(message, 0))
            {
                return EmailRateLimitMessage;
            }

            if (message.StartsWith("{", StringComparison.Ordinal))
            {
                return fallback;
            }

            return message.Length > 160 ? message.Substring(0, 160) : message;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }
    }
}
