using UnityEngine;

namespace QuixoUnity.Auth
{
    public static class SupabaseSettings
    {
        public const string ProjectUrl = "https://wcwufabumabolxhmpexc.supabase.co";
        public const string AnonKey = "sb_publishable_PwbgvZXpUn07HsvFRghnPg_R_9T5W3H";
        public const string PasswordResetRedirectUrl = "https://bouzekrimohamed.github.io/quixocomet/reset-password/";
        public const string EmailConfirmationRedirectUrl = "https://bouzekrimohamed.github.io/quixocomet/email-confirmed/";

        private const string ProjectUrlPrefsKey = "quixo_supabase_project_url";
        private const string AnonKeyPrefsKey = "quixo_supabase_anon_key";

        public static string Url
        {
            get
            {
                string value = PlayerPrefs.GetString(ProjectUrlPrefsKey, ProjectUrl);
                return NormalizeUrl(value);
            }
        }

        public static string PublicAnonKey
        {
            get
            {
                return PlayerPrefs.GetString(AnonKeyPrefsKey, AnonKey).Trim();
            }
        }

        public static bool IsConfigured
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(PublicAnonKey);
            }
        }

        public static void SaveLocalOverride(string projectUrl, string anonKey)
        {
            PlayerPrefs.SetString(ProjectUrlPrefsKey, NormalizeUrl(projectUrl));
            PlayerPrefs.SetString(AnonKeyPrefsKey, anonKey.Trim());
            PlayerPrefs.Save();
        }

        private static string NormalizeUrl(string url)
        {
            return string.IsNullOrWhiteSpace(url) ? string.Empty : url.Trim().TrimEnd('/');
        }
    }
}
