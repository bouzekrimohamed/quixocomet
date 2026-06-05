using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace QuixoUnity.Auth
{
    public sealed class AuthService : MonoBehaviour
    {
        private const string PendingUsernamePrefix = "Quixo.Auth.PendingUsername.";

        public void Register(string email, string password, string username, Action<AuthOperationResult> onComplete)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                onComplete?.Invoke(AuthOperationResult.Fail("Entrez un username."));
                return;
            }

            if (!ValidateInput(email, password, onComplete))
            {
                return;
            }

            string cleanEmail = email.Trim();
            SavePendingUsername(cleanEmail, username);
            StartCoroutine(AuthRoutine("signup", cleanEmail, password, username, true, onComplete));
        }

        public void Login(string identifier, string password, Action<AuthOperationResult> onComplete)
        {
            if (!ValidateLoginInput(identifier, password, onComplete))
            {
                return;
            }

            StartCoroutine(LoginRoutine(identifier.Trim(), password, onComplete));
        }

        public void SendPasswordReset(string email, Action<AuthOperationResult> onComplete)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                onComplete?.Invoke(AuthOperationResult.Fail("Entrez votre email."));
                return;
            }

            if (!email.Contains("@"))
            {
                onComplete?.Invoke(AuthOperationResult.Fail("Pour reinitialiser le mot de passe, entrez votre email."));
                return;
            }

            if (!SupabaseSettings.IsConfigured)
            {
                onComplete?.Invoke(AuthOperationResult.Fail("Service en ligne indisponible."));
                return;
            }

            StartCoroutine(PasswordResetRoutine(email.Trim(), onComplete));
        }

        public void FetchCurrentProfile(Action<AuthOperationResult> onComplete)
        {
            if (!SupabaseSettings.IsConfigured)
            {
                onComplete?.Invoke(AuthOperationResult.Fail("Service en ligne indisponible."));
                return;
            }

            if (!SessionManager.IsOnline)
            {
                onComplete?.Invoke(AuthOperationResult.Fail("Aucune session en ligne active."));
                return;
            }

            StartCoroutine(FetchProfileRoutine(SessionManager.UserId, onComplete));
        }

        public void RefreshSession(Action<AuthOperationResult> onComplete)
        {
            StartCoroutine(RefreshSessionRoutine(onComplete));
        }

        public void Logout()
        {
            SessionManager.ClearSession();
        }

        private IEnumerator RefreshSessionRoutine(Action<AuthOperationResult> onComplete)
        {
            bool refreshed = false;
            yield return SupabaseRequestHelper.RefreshSessionRoutine(result => refreshed = result);
            onComplete?.Invoke(refreshed
                ? AuthOperationResult.Ok("Session rafraichie.")
                : AuthOperationResult.Fail(SupabaseRequestHelper.SessionExpiredMessage));
        }

        private IEnumerator LoginRoutine(string identifier, string password, Action<AuthOperationResult> onComplete)
        {
            if (identifier.Contains("@"))
            {
                yield return AuthRoutine("token?grant_type=password", identifier, password, string.Empty, false, onComplete);
                yield break;
            }

            string resolvedEmail = null;
            string lookupError = null;
            yield return ResolveEmailFromUsernameRoutine(identifier, result =>
            {
                if (result.Success && result.Profile != null)
                {
                    resolvedEmail = result.Profile.email;
                }
                else
                {
                    lookupError = result.Message;
                }
            });

            if (string.IsNullOrWhiteSpace(resolvedEmail))
            {
                onComplete?.Invoke(AuthOperationResult.Fail(string.IsNullOrWhiteSpace(lookupError) ? "Utilisateur introuvable." : lookupError));
                yield break;
            }

            yield return AuthRoutine("token?grant_type=password", resolvedEmail, password, string.Empty, false, onComplete);
        }

        private IEnumerator AuthRoutine(string endpoint, string email, string password, string requestedUsername, bool createProfile, Action<AuthOperationResult> onComplete)
        {
            if (!SupabaseSettings.IsConfigured)
            {
                onComplete?.Invoke(AuthOperationResult.Fail("Service en ligne indisponible."));
                yield break;
            }

            var payload = new AuthEmailRequest
            {
                email = email,
                password = password
            };

            string url = BuildAuthRequestUrl(endpoint, createProfile);

            using var request = CreateJsonRequest(url, "POST", UnityEngine.JsonUtility.ToJson(payload), string.Empty);
            yield return request.SendWebRequest();

            if (!IsSuccess(request))
            {
                string message = SupabaseRequestHelper.MapAuthError(
                    request,
                    createProfile ? "Inscription impossible." : "Connexion impossible.",
                    createProfile);
                onComplete?.Invoke(AuthOperationResult.Fail(message));
                yield break;
            }

            var authResponse = UnityEngine.JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
            if (authResponse == null || authResponse.user == null)
            {
                onComplete?.Invoke(AuthOperationResult.Fail("Reponse Supabase invalide."));
                yield break;
            }

            if (string.IsNullOrWhiteSpace(authResponse.access_token))
            {
                onComplete?.Invoke(AuthOperationResult.Ok("Compte cree. Verifiez votre email avant de vous connecter.", authResponse));
                yield break;
            }

            SessionManager.SaveSession(authResponse);
            if (createProfile)
            {
                string username = string.IsNullOrWhiteSpace(requestedUsername) ? GenerateUsername(email) : SanitizeUsername(requestedUsername);
                yield return EnsureProfileRoutine(authResponse, username, onComplete, "Compte cree. Connexion reussie.");
                yield break;
            }

            AuthOperationResult profileResult = null;
            yield return FetchProfileRoutine(authResponse.user.id, result => profileResult = result);
            if (profileResult != null && profileResult.Success)
            {
                onComplete?.Invoke(AuthOperationResult.Ok("Connecte.", authResponse, profileResult.Profile));
                yield break;
            }

            string fallbackUsername = ConsumePendingUsername(email);
            if (string.IsNullOrWhiteSpace(fallbackUsername))
            {
                fallbackUsername = GenerateUsername(email);
            }

            yield return EnsureProfileRoutine(authResponse, fallbackUsername, onComplete);
        }

        private IEnumerator EnsureProfileRoutine(AuthResponse session, string username, Action<AuthOperationResult> onComplete, string successMessage = "Connecte.")
        {
            string displayName = username;
            var payload = new ProfileUpsertRequest
            {
                id = session.user.id,
                username = username,
                display_name = displayName,
                email = session.user.email
            };

            string url = $"{SupabaseSettings.Url}/rest/v1/profiles?on_conflict=id";
            using var request = CreateJsonRequest(url, "POST", UnityEngine.JsonUtility.ToJson(payload), session.access_token);
            request.SetRequestHeader("Prefer", "resolution=merge-duplicates,return=representation");
            yield return request.SendWebRequest();

            if (!IsSuccess(request))
            {
                onComplete?.Invoke(AuthOperationResult.Fail(ParseError(request, "Session creee, mais le profil n'a pas pu etre cree.")));
                yield break;
            }

            var profiles = SupabaseJson.FromArray<ProfileDto>(request.downloadHandler.text);
            ProfileDto profile = profiles.Count > 0 ? profiles[0] : new ProfileDto
            {
                id = session.user.id,
                username = username,
                display_name = displayName,
                email = session.user.email
            };

            SessionManager.SaveSession(session, profile);
            onComplete?.Invoke(AuthOperationResult.Ok(successMessage, session, profile));
        }

        private IEnumerator FetchProfileRoutine(string userId, Action<AuthOperationResult> onComplete)
        {
            string url = $"{SupabaseSettings.Url}/rest/v1/profiles?id=eq.{UnityWebRequest.EscapeURL(userId)}&select=id,username,display_name,email,created_at";
            UnityWebRequest request = null;
            yield return SupabaseRequestHelper.SendAuthorizedRequest(
                () => CreateJsonRequest(url, "GET", null, SessionManager.AccessToken),
                completed => request = completed);
            using (request)
            {
                if (!IsSuccess(request))
                {
                    onComplete?.Invoke(AuthOperationResult.Fail(ParseError(request, "Profil inaccessible.")));
                    yield break;
                }

                var profiles = SupabaseJson.FromArray<ProfileDto>(request.downloadHandler.text);
                if (profiles.Count == 0)
                {
                    onComplete?.Invoke(AuthOperationResult.Fail("Profil introuvable."));
                    yield break;
                }

                SessionManager.SaveProfile(profiles[0]);
                onComplete?.Invoke(AuthOperationResult.Ok("Profil charge.", null, profiles[0]));
            }
        }

        private IEnumerator ResolveEmailFromUsernameRoutine(string username, Action<AuthOperationResult> onComplete)
        {
            if (!SupabaseSettings.IsConfigured)
            {
                onComplete?.Invoke(AuthOperationResult.Fail("Service en ligne indisponible."));
                yield break;
            }

            string safeUsername = UnityWebRequest.EscapeURL(NormalizeUsernameForLookup(username));
            string url = $"{SupabaseSettings.Url}/rest/v1/profiles?username=eq.{safeUsername}&select=id,username,display_name,email,created_at&limit=1";
            using var request = CreateJsonRequest(url, "GET", null, string.Empty);
            yield return request.SendWebRequest();

            if (!IsSuccess(request))
            {
                onComplete?.Invoke(AuthOperationResult.Fail(ParseError(request, "Utilisateur introuvable.")));
                yield break;
            }

            var profiles = SupabaseJson.FromArray<ProfileDto>(request.downloadHandler.text);
            if (profiles.Count == 0 || string.IsNullOrWhiteSpace(profiles[0].email))
            {
                onComplete?.Invoke(AuthOperationResult.Fail("Utilisateur introuvable."));
                yield break;
            }

            onComplete?.Invoke(AuthOperationResult.Ok("Utilisateur trouve.", null, profiles[0]));
        }

        private IEnumerator PasswordResetRoutine(string email, Action<AuthOperationResult> onComplete)
        {
            var payload = new PasswordRecoveryRequest
            {
                email = email,
                redirect_to = SupabaseSettings.PasswordResetRedirectUrl
            };

            string url = $"{SupabaseSettings.Url}/auth/v1/recover";
            using var request = CreateJsonRequest(url, "POST", UnityEngine.JsonUtility.ToJson(payload), string.Empty);
            yield return request.SendWebRequest();

            if (!IsSuccess(request))
            {
                string message = SupabaseRequestHelper.IsEmailRateLimit(request)
                    ? SupabaseRequestHelper.EmailRateLimitMessage
                    : ParseError(request, "Email de reinitialisation impossible.");
                onComplete?.Invoke(AuthOperationResult.Fail(message));
                yield break;
            }

            onComplete?.Invoke(AuthOperationResult.Ok("Email de reinitialisation envoye."));
        }

        private static string BuildAuthRequestUrl(string endpoint, bool isSignup)
        {
            string url = $"{SupabaseSettings.Url}/auth/v1/{endpoint}";
            if (!isSignup)
            {
                return url;
            }

            string redirect = SupabaseSettings.EmailConfirmationRedirectUrl?.Trim();
            if (string.IsNullOrWhiteSpace(redirect))
            {
                return url;
            }

            return url + "?redirect_to=" + UnityWebRequest.EscapeURL(redirect);
        }

        private static UnityWebRequest CreateJsonRequest(string url, string method, string json, string accessToken)
        {
            return SupabaseRequestHelper.CreateJsonRequest(url, method, json, accessToken);
        }

        private static bool ValidateInput(string email, string password, Action<AuthOperationResult> onComplete)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                onComplete?.Invoke(AuthOperationResult.Fail("Entrez votre email."));
                return false;
            }

            if (!email.Contains("@"))
            {
                onComplete?.Invoke(AuthOperationResult.Fail("Email invalide."));
                return false;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                onComplete?.Invoke(AuthOperationResult.Fail("Entrez votre mot de passe."));
                return false;
            }

            if (password.Length < 6)
            {
                onComplete?.Invoke(AuthOperationResult.Fail("Mot de passe trop court. Minimum 6 caracteres."));
                return false;
            }

            return true;
        }

        private static bool ValidateLoginInput(string identifier, string password, Action<AuthOperationResult> onComplete)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                onComplete?.Invoke(AuthOperationResult.Fail("Entrez votre email ou username."));
                return false;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                onComplete?.Invoke(AuthOperationResult.Fail("Entrez votre mot de passe."));
                return false;
            }

            if (password.Length < 6)
            {
                onComplete?.Invoke(AuthOperationResult.Fail("Mot de passe trop court. Minimum 6 caracteres."));
                return false;
            }

            return true;
        }

        private static bool IsSuccess(UnityWebRequest request)
        {
            return SupabaseRequestHelper.IsSuccess(request);
        }

        private static string ParseError(UnityWebRequest request, string fallback)
        {
            return SupabaseRequestHelper.ParseError(request, fallback);
        }

        private static string GenerateUsername(string email)
        {
            string prefix = email.Split('@')[0];
            return SanitizeUsername(prefix) + UnityEngine.Random.Range(100, 999).ToString();
        }

        private static void SavePendingUsername(string email, string username)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            string value = string.IsNullOrWhiteSpace(username) ? GenerateUsername(email) : SanitizeUsername(username);
            PlayerPrefs.SetString(PendingUsernamePrefix + email.ToLowerInvariant(), value);
            PlayerPrefs.Save();
        }

        private static string ConsumePendingUsername(string email)
        {
            string key = PendingUsernamePrefix + email.ToLowerInvariant();
            string value = PlayerPrefs.GetString(key, string.Empty);
            if (!string.IsNullOrWhiteSpace(value))
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }

            return value;
        }

        private static string SanitizeUsername(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "player" + UnityEngine.Random.Range(1000, 9999).ToString();
            }

            var builder = new StringBuilder();
            foreach (char c in raw.Trim().ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
                {
                    builder.Append(c);
                }
            }

            if (builder.Length < 3)
            {
                builder.Append(UnityEngine.Random.Range(100, 999).ToString());
            }

            return builder.ToString();
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
