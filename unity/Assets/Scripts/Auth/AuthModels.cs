using System;
using System.Collections.Generic;

namespace QuixoUnity.Auth
{
    [Serializable]
    public sealed class AuthEmailRequest
    {
        public string email;
        public string password;
    }

    [Serializable]
    public sealed class PasswordRecoveryRequest
    {
        public string email;
        public string redirect_to;
    }

    [Serializable]
    public sealed class SupabaseUser
    {
        public string id;
        public string email;
    }

    [Serializable]
    public sealed class AuthResponse
    {
        public string access_token;
        public string refresh_token;
        public int expires_in;
        public SupabaseUser user;
        public string error;
        public string error_description;
        public string msg;
    }

    [Serializable]
    public sealed class ProfileDto
    {
        public string id;
        public string username;
        public string display_name;
        public string email;
        public string created_at;
    }

    [Serializable]
    public sealed class ProfileUpsertRequest
    {
        public string id;
        public string username;
        public string display_name;
        public string email;
    }

    public sealed class AuthOperationResult
    {
        public bool Success;
        public string Message;
        public AuthResponse Session;
        public ProfileDto Profile;

        public static AuthOperationResult Ok(string message, AuthResponse session = null, ProfileDto profile = null)
        {
            return new AuthOperationResult
            {
                Success = true,
                Message = message,
                Session = session,
                Profile = profile
            };
        }

        public static AuthOperationResult Fail(string message)
        {
            return new AuthOperationResult
            {
                Success = false,
                Message = message
            };
        }
    }

    [Serializable]
    internal sealed class JsonArrayWrapper<T>
    {
        public T[] items;
    }

    public static class SupabaseJson
    {
        public static List<T> FromArray<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<T>();
            }

            string wrapped = "{\"items\":" + json + "}";
            var wrapper = UnityEngine.JsonUtility.FromJson<JsonArrayWrapper<T>>(wrapped);
            return wrapper?.items != null ? new List<T>(wrapper.items) : new List<T>();
        }
    }
}
