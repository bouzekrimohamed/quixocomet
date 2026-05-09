using UnityEngine;

namespace QuixoUnity.Auth
{
    public static class SessionManager
    {
        private const string AccessTokenKey = "Quixo.Auth.AccessToken";
        private const string RefreshTokenKey = "Quixo.Auth.RefreshToken";
        private const string UserIdKey = "Quixo.Auth.UserId";
        private const string EmailKey = "Quixo.Auth.Email";
        private const string UsernameKey = "Quixo.Auth.Username";
        private const string OfflineKey = "Quixo.Auth.Offline";

        public static bool HasSession => IsOffline || !string.IsNullOrWhiteSpace(AccessToken);
        public static bool IsOnline => !IsOffline && !string.IsNullOrWhiteSpace(AccessToken);
        public static bool IsOffline => PlayerPrefs.GetInt(OfflineKey, 0) == 1;
        public static string AccessToken => PlayerPrefs.GetString(AccessTokenKey, string.Empty);
        public static string RefreshToken => PlayerPrefs.GetString(RefreshTokenKey, string.Empty);
        public static string UserId => PlayerPrefs.GetString(UserIdKey, string.Empty);
        public static string Email => PlayerPrefs.GetString(EmailKey, string.Empty);
        public static string Username => PlayerPrefs.GetString(UsernameKey, IsOffline ? "Invite" : string.Empty);

        public static void SaveSession(AuthResponse response, ProfileDto profile = null)
        {
            if (response == null)
            {
                return;
            }

            PlayerPrefs.SetInt(OfflineKey, 0);
            PlayerPrefs.SetString(AccessTokenKey, response.access_token ?? string.Empty);
            PlayerPrefs.SetString(RefreshTokenKey, response.refresh_token ?? string.Empty);

            if (response.user != null)
            {
                PlayerPrefs.SetString(UserIdKey, response.user.id ?? string.Empty);
                PlayerPrefs.SetString(EmailKey, response.user.email ?? string.Empty);
            }

            if (profile != null && !string.IsNullOrWhiteSpace(profile.username))
            {
                PlayerPrefs.SetString(UsernameKey, profile.username);
            }

            PlayerPrefs.Save();
        }

        public static void SaveProfile(ProfileDto profile)
        {
            if (profile == null)
            {
                return;
            }

            PlayerPrefs.SetString(UsernameKey, profile.username ?? string.Empty);
            PlayerPrefs.Save();
        }

        public static void StartOffline(string username = "Invite")
        {
            ClearSession(false);
            PlayerPrefs.SetInt(OfflineKey, 1);
            PlayerPrefs.SetString(UsernameKey, string.IsNullOrWhiteSpace(username) ? "Invite" : username.Trim());
            PlayerPrefs.Save();
        }

        public static void ClearSession(bool save = true)
        {
            PlayerPrefs.DeleteKey(AccessTokenKey);
            PlayerPrefs.DeleteKey(RefreshTokenKey);
            PlayerPrefs.DeleteKey(UserIdKey);
            PlayerPrefs.DeleteKey(EmailKey);
            PlayerPrefs.DeleteKey(UsernameKey);
            PlayerPrefs.DeleteKey(OfflineKey);

            if (save)
            {
                PlayerPrefs.Save();
            }
        }
    }
}
