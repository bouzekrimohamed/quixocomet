using System;
using QuixoUnity.Core;
using UnityEngine;

namespace QuixoUnity.Online
{
    public enum OnlineGameMode
    {
        Local = 0,
        Online = 1
    }

    [Serializable]
    public sealed class UserPresenceDto
    {
        public string user_id;
        public string username;
        public string status;
        public string last_seen_at;
    }

    [Serializable]
    public sealed class PresenceUpsertRequest
    {
        public string user_id;
        public string username;
        public string status;
        public string last_seen_at;
    }

    [Serializable]
    public sealed class MatchInviteDto
    {
        public string id;
        public string from_user_id;
        public string to_user_id;
        public string game_kind;
        public string status;
        public string match_id;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public sealed class MatchInviteCreateRequest
    {
        public string from_user_id;
        public string to_user_id;
        public string game_kind;
        public string status;
    }

    [Serializable]
    public sealed class MatchInviteUpdateRequest
    {
        public string status;
        public string match_id;
        public string updated_at;
    }

    [Serializable]
    public sealed class MatchmakingQueueDto
    {
        public string id;
        public string user_id;
        public string game_kind;
        public string status;
        public string match_id;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public sealed class MatchmakingQueueCreateRequest
    {
        public string user_id;
        public string game_kind;
        public string status;
        public string updated_at;
    }

    [Serializable]
    public sealed class MatchmakingQueueUpdateRequest
    {
        public string status;
        public string match_id;
        public string updated_at;
    }

    [Serializable]
    public sealed class OnlineMatchDto
    {
        public string id;
        public string game_kind;
        public string player1_id;
        public string player2_id;
        public string current_turn_id;
        public string status;
        public string winner_id;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public sealed class OnlineMatchCreateRequest
    {
        public string game_kind;
        public string player1_id;
        public string player2_id;
        public string current_turn_id;
        public string status;
    }

    [Serializable]
    public sealed class OnlineMatchUpdateRequest
    {
        public string current_turn_id;
        public string status;
        public string winner_id;
        public string updated_at;
    }

    [Serializable]
    public sealed class OnlineMoveDto
    {
        public string id;
        public string match_id;
        public string player_id;
        public int move_number;
        public OnlineMovePayload move_payload;
        public string created_at;
    }

    [Serializable]
    public sealed class OnlineMoveCreateRequest
    {
        public string match_id;
        public string player_id;
        public int move_number;
        public OnlineMovePayload move_payload;
    }

    [Serializable]
    public sealed class OnlineMovePayload
    {
        public string gameKind;
        public string action;
        public int selectedRow = -1;
        public int selectedCol = -1;
        public string direction;
        public int fromRow = -1;
        public int fromCol = -1;
        public int toRow = -1;
        public int toCol = -1;
        public string fromNode;
        public string toNode;
    }

    public sealed class OnlineOperationResult
    {
        public bool Success;
        public string Message;
        public OnlineMatchDto Match;
        public MatchInviteDto Invite;

        public static OnlineOperationResult Ok(string message, OnlineMatchDto match = null, MatchInviteDto invite = null)
        {
            return new OnlineOperationResult
            {
                Success = true,
                Message = message,
                Match = match,
                Invite = invite
            };
        }

        public static OnlineOperationResult Fail(string message)
        {
            return new OnlineOperationResult
            {
                Success = false,
                Message = message
            };
        }
    }

    public static class OnlineSessionTransit
    {
        public static bool IsOnlineMatch;
        public static string MatchId;
        public static string LocalUserId;
        public static string OpponentUserId;
        public static string OpponentUsername;
        public static string Player1Id;
        public static string Player2Id;
        public static string CurrentTurnId;
        public static GameKind SelectedGameKind;

        public static void Start(OnlineMatchDto match, string localUserId, string opponentUsername = "")
        {
            if (!IsValidForLocalPlayer(match, localUserId))
            {
                Clear();
                return;
            }

            IsOnlineMatch = true;
            MatchId = match.id;
            LocalUserId = localUserId;
            Player1Id = match.player1_id;
            Player2Id = match.player2_id;
            CurrentTurnId = match.current_turn_id;
            SelectedGameKind = ParseGameKind(match.game_kind);
            OpponentUserId = match.player1_id == localUserId ? match.player2_id : match.player1_id;
            OpponentUsername = string.IsNullOrWhiteSpace(opponentUsername) ? ShortId(OpponentUserId) : opponentUsername;
        }

        public static bool IsValidForLocalPlayer(OnlineMatchDto match, string localUserId)
        {
            return match != null
                && !string.IsNullOrWhiteSpace(match.id)
                && !string.IsNullOrWhiteSpace(localUserId)
                && !string.IsNullOrWhiteSpace(match.player1_id)
                && !string.IsNullOrWhiteSpace(match.player2_id)
                && match.player1_id != match.player2_id
                && (localUserId == match.player1_id || localUserId == match.player2_id);
        }

        public static void UpdateMatch(OnlineMatchDto match)
        {
            if (match == null)
            {
                return;
            }

            CurrentTurnId = match.current_turn_id;
            Player1Id = match.player1_id;
            Player2Id = match.player2_id;
        }

        public static void Clear()
        {
            IsOnlineMatch = false;
            MatchId = string.Empty;
            LocalUserId = string.Empty;
            OpponentUserId = string.Empty;
            OpponentUsername = string.Empty;
            Player1Id = string.Empty;
            Player2Id = string.Empty;
            CurrentTurnId = string.Empty;
            SelectedGameKind = QuixoUnity.Core.GameKind.Quixo;
        }

        public static PlayerMark LocalPlayerMark()
        {
            return LocalUserId == Player1Id ? PlayerMark.Player1 : PlayerMark.Player2;
        }

        public static PlayerMark PlayerMarkForUser(string userId)
        {
            return userId == Player1Id ? PlayerMark.Player1 : PlayerMark.Player2;
        }

        public static string OpponentOf(string userId)
        {
            return userId == Player1Id ? Player2Id : Player1Id;
        }

        public static GameKind ParseGameKind(string value)
        {
            return string.Equals(value, "Qomet", StringComparison.OrdinalIgnoreCase) ? GameKind.Qomet : GameKind.Quixo;
        }

        public static string GameKindName(GameKind kind)
        {
            return kind == GameKind.Qomet ? "Qomet" : "Quixo";
        }

        public static string NodeName(int row, int col)
        {
            return QometGraph.TryGetNode(row, col, out var node) ? node.Id : $"{row}:{col}";
        }

        private static string ShortId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "adversaire";
            }

            return value.Length <= 8 ? value : value.Substring(0, 8);
        }
    }
}
