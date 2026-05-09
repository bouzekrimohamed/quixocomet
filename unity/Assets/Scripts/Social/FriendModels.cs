using System;
using System.Collections.Generic;
using QuixoUnity.Auth;

namespace QuixoUnity.Social
{
    [Serializable]
    public sealed class FriendDto
    {
        public string id;
        public string requester_id;
        public string receiver_id;
        public string status;
        public string created_at;
    }

    [Serializable]
    public sealed class FriendCreateRequest
    {
        public string requester_id;
        public string receiver_id;
        public string status;
    }

    [Serializable]
    public sealed class FriendStatusUpdate
    {
        public string status;
    }

    public sealed class FriendListItem
    {
        public string RequestId;
        public string UserId;
        public string Username;
        public string DisplayName;
        public string Status;
    }

    public sealed class FriendSummary
    {
        public readonly List<FriendListItem> Requests = new();
        public readonly List<FriendListItem> Friends = new();
    }

    public sealed class SocialOperationResult
    {
        public bool Success;
        public string Message;
        public FriendSummary Summary;
        public ProfileDto Profile;

        public static SocialOperationResult Ok(string message, FriendSummary summary = null, ProfileDto profile = null)
        {
            return new SocialOperationResult
            {
                Success = true,
                Message = message,
                Summary = summary,
                Profile = profile
            };
        }

        public static SocialOperationResult Fail(string message)
        {
            return new SocialOperationResult
            {
                Success = false,
                Message = message
            };
        }
    }
}
