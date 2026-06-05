using System;
using System.Collections.Generic;
using QuixoUnity.Core;
using QuixoUnity.Gameplay;
using UnityEngine;

namespace QuixoUnity.Online
{
    public enum OnlineGameMode
    {
        Local = 0,
        Online = 1
    }

    public enum MatchMode
    {
        OneVsOne = 0,
        Team2v2 = 1
    }

    public enum TeamId
    {
        None = 0,
        Team1 = 1,
        Team2 = 2
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
        public string time_control_key;
        public int initial_seconds;
        public int increment_seconds;
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
        public string time_control_key;
        public int initial_seconds;
        public int increment_seconds;
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
        public string time_control_key;
        public int initial_seconds;
        public int increment_seconds;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public sealed class MatchmakingQueueCreateRequest
    {
        public string user_id;
        public string game_kind;
        public string status;
        public string time_control_key;
        public int initial_seconds;
        public int increment_seconds;
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
        public string match_mode;
        public string player1_id;
        public string player2_id;
        public string team1_player1_id;
        public string team1_player2_id;
        public string team2_player1_id;
        public string team2_player2_id;
        public string current_turn_id;
        public int current_turn_index;
        public string status;
        public string winner_id;
        public string winner_team;
        public string time_control_key;
        public int initial_seconds;
        public int increment_seconds;
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
        public string time_control_key;
        public int initial_seconds;
        public int increment_seconds;
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
        public string matchMode;
        public string team;
        public string playerId;
        public string action;
        public int selectedRow = -1;
        public int selectedCol = -1;
        public string direction;
        public string dotOwner;
        public string dotOwnerUserId;
        public int fromRow = -1;
        public int fromCol = -1;
        public int toRow = -1;
        public int toCol = -1;
        public string fromNode;
        public string toNode;
    }

    [Serializable]
    public sealed class TeamLobbyDto
    {
        public string id;
        public string lobby_code;
        public string game_kind;
        public string match_mode;
        public string host_user_id;
        public string status;
        public string match_id;
        public string time_control_key;
        public int initial_seconds;
        public int increment_seconds;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public sealed class TeamLobbyPlayerDto
    {
        public string id;
        public string lobby_id;
        public string user_id;
        public string username;
        public string team_id;
        public int slot_index;
        public string joined_at;
        public string updated_at;
    }

    public sealed class TeamLobbySnapshot
    {
        public TeamLobbyDto Lobby;
        public List<TeamLobbyPlayerDto> Players = new();
        public OnlineMatchDto Match;

        public bool IsStarted => Lobby != null
            && string.Equals(Lobby.status, "started", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(Lobby.match_id);

        public TeamLobbyPlayerDto GetPlayer(TeamId team, int slotIndex)
        {
            string teamName = OnlineSessionTransit.TeamName(team);
            foreach (var player in Players)
            {
                if (player != null
                    && string.Equals(player.team_id, teamName, StringComparison.OrdinalIgnoreCase)
                    && player.slot_index == slotIndex)
                {
                    return player;
                }
            }

            return null;
        }

        public int CountTeam(TeamId team)
        {
            int count = 0;
            string teamName = OnlineSessionTransit.TeamName(team);
            foreach (var player in Players)
            {
                if (player != null && string.Equals(player.team_id, teamName, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        public bool HasUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            foreach (var player in Players)
            {
                if (player != null && player.user_id == userId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsFull => CountTeam(TeamId.Team1) == 2 && CountTeam(TeamId.Team2) == 2 && Players.Count == 4;
    }

    public sealed class TeamLobbyOperationResult
    {
        public bool Success;
        public string Message;
        public TeamLobbySnapshot Snapshot;

        public static TeamLobbyOperationResult Ok(string message, TeamLobbySnapshot snapshot = null)
        {
            return new TeamLobbyOperationResult
            {
                Success = true,
                Message = message,
                Snapshot = snapshot
            };
        }

        public static TeamLobbyOperationResult Fail(string message)
        {
            return new TeamLobbyOperationResult
            {
                Success = false,
                Message = message
            };
        }
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
        public static MatchMode SelectedMatchMode;
        public static int CurrentTurnIndex;
        public static string Team1Player1Id;
        public static string Team1Player2Id;
        public static string Team2Player1Id;
        public static string Team2Player2Id;
        public static string Team1Player1Username;
        public static string Team1Player2Username;
        public static string Team2Player1Username;
        public static string Team2Player2Username;
        // Cadence negociee pour cette partie en ligne. 0+0 = sans limite.
        public static int TurnTimeSeconds;
        public static string TimeControlKey;
        public static int InitialSeconds;
        public static int IncrementSeconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ClearOnApplicationStart()
        {
            Clear();
        }

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
            SelectedMatchMode = ParseMatchMode(match.match_mode);
            CurrentTurnIndex = match.current_turn_index;
            ApplyTimeControl(match);
            OpponentUserId = match.player1_id == localUserId ? match.player2_id : match.player1_id;
            OpponentUsername = string.IsNullOrWhiteSpace(opponentUsername) ? ShortId(OpponentUserId) : opponentUsername;
        }

        public static void StartTeam(OnlineMatchDto match, string localUserId, TeamLobbySnapshot lobby)
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
            CurrentTurnIndex = match.current_turn_index;
            SelectedGameKind = ParseGameKind(match.game_kind);
            SelectedMatchMode = MatchMode.Team2v2;
            Team1Player1Id = match.team1_player1_id;
            Team1Player2Id = match.team1_player2_id;
            Team2Player1Id = match.team2_player1_id;
            Team2Player2Id = match.team2_player2_id;
            ApplyTimeControl(match);
            ApplyLobbyNames(lobby);
            OpponentUserId = FirstOpponentOf(localUserId);
            OpponentUsername = TeamLabel(OpposingTeam(TeamForUser(localUserId)));
        }

        public static bool IsValidForLocalPlayer(OnlineMatchDto match, string localUserId)
        {
            if (match == null
                || string.IsNullOrWhiteSpace(match.id)
                || string.IsNullOrWhiteSpace(localUserId))
            {
                return false;
            }

            if (ParseMatchMode(match.match_mode) == MatchMode.Team2v2)
            {
                return match.game_kind == GameKindName(GameKind.Quixo)
                    && !string.IsNullOrWhiteSpace(match.team1_player1_id)
                    && !string.IsNullOrWhiteSpace(match.team1_player2_id)
                    && !string.IsNullOrWhiteSpace(match.team2_player1_id)
                    && !string.IsNullOrWhiteSpace(match.team2_player2_id)
                    && (localUserId == match.team1_player1_id
                        || localUserId == match.team1_player2_id
                        || localUserId == match.team2_player1_id
                        || localUserId == match.team2_player2_id);
            }

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
            SelectedMatchMode = ParseMatchMode(match.match_mode);
            CurrentTurnIndex = match.current_turn_index;
            Team1Player1Id = match.team1_player1_id;
            Team1Player2Id = match.team1_player2_id;
            Team2Player1Id = match.team2_player1_id;
            Team2Player2Id = match.team2_player2_id;
            ApplyTimeControl(match);
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
            SelectedMatchMode = MatchMode.OneVsOne;
            CurrentTurnIndex = 0;
            Team1Player1Id = string.Empty;
            Team1Player2Id = string.Empty;
            Team2Player1Id = string.Empty;
            Team2Player2Id = string.Empty;
            Team1Player1Username = string.Empty;
            Team1Player2Username = string.Empty;
            Team2Player1Username = string.Empty;
            Team2Player2Username = string.Empty;
            TurnTimeSeconds = 0;
            TimeControlKey = string.Empty;
            InitialSeconds = 0;
            IncrementSeconds = 0;
        }

        public static void ApplyTimeControl(OnlineMatchDto match)
        {
            if (match == null)
            {
                return;
            }

            var option = TurnTimerSettings.OptionForNetwork(match.time_control_key, match.initial_seconds, match.increment_seconds);
            TimeControlKey = option.Key;
            InitialSeconds = option.InitialSeconds;
            IncrementSeconds = option.IncrementSeconds;
            TurnTimeSeconds = option.InitialSeconds;
        }

        public static PlayerMark LocalPlayerMark()
        {
            if (SelectedMatchMode == MatchMode.Team2v2)
            {
                return TeamForUser(LocalUserId) == TeamId.Team1 ? PlayerMark.Player1 : PlayerMark.Player2;
            }

            return LocalUserId == Player1Id ? PlayerMark.Player1 : PlayerMark.Player2;
        }

        public static PlayerMark PlayerMarkForUser(string userId)
        {
            if (SelectedMatchMode == MatchMode.Team2v2)
            {
                return TeamForUser(userId) == TeamId.Team1 ? PlayerMark.Player1 : PlayerMark.Player2;
            }

            return userId == Player1Id ? PlayerMark.Player1 : PlayerMark.Player2;
        }

        public static string OpponentOf(string userId)
        {
            return userId == Player1Id ? Player2Id : Player1Id;
        }

        public static bool IsTeam2v2 => SelectedMatchMode == MatchMode.Team2v2;

        public static string MatchModeName(MatchMode mode)
        {
            return mode == MatchMode.Team2v2 ? "Team2v2" : "OneVsOne";
        }

        public static MatchMode ParseMatchMode(string value)
        {
            return string.Equals(value, "Team2v2", StringComparison.OrdinalIgnoreCase)
                ? MatchMode.Team2v2
                : MatchMode.OneVsOne;
        }

        public static string TeamName(TeamId team)
        {
            return team == TeamId.Team2 ? "Team2" : team == TeamId.Team1 ? "Team1" : string.Empty;
        }

        public static TeamId ParseTeam(string value)
        {
            if (string.Equals(value, "Team1", StringComparison.OrdinalIgnoreCase))
            {
                return TeamId.Team1;
            }

            return string.Equals(value, "Team2", StringComparison.OrdinalIgnoreCase) ? TeamId.Team2 : TeamId.None;
        }

        public static TeamId TeamForUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return TeamId.None;
            }

            if (userId == Team1Player1Id || userId == Team1Player2Id)
            {
                return TeamId.Team1;
            }

            return userId == Team2Player1Id || userId == Team2Player2Id ? TeamId.Team2 : TeamId.None;
        }

        public static TeamId OpposingTeam(TeamId team)
        {
            return team == TeamId.Team1 ? TeamId.Team2 : team == TeamId.Team2 ? TeamId.Team1 : TeamId.None;
        }

        public static string UserIdForTurnIndex(int index)
        {
            int normalized = ((index % 4) + 4) % 4;
            return normalized switch
            {
                0 => Team1Player1Id,
                1 => Team2Player1Id,
                2 => Team1Player2Id,
                3 => Team2Player2Id,
                _ => Team1Player1Id
            };
        }

        public static QuixoDotOwner DotOwnerForUser(string userId)
        {
            if (userId == Team1Player1Id)
            {
                return QuixoDotOwner.Team1Player1;
            }

            if (userId == Team1Player2Id)
            {
                return QuixoDotOwner.Team1Player2;
            }

            if (userId == Team2Player1Id)
            {
                return QuixoDotOwner.Team2Player1;
            }

            return userId == Team2Player2Id ? QuixoDotOwner.Team2Player2 : QuixoDotOwner.None;
        }

        public static string UserIdForDotOwner(QuixoDotOwner owner)
        {
            return owner switch
            {
                QuixoDotOwner.Team1Player1 => Team1Player1Id,
                QuixoDotOwner.Team1Player2 => Team1Player2Id,
                QuixoDotOwner.Team2Player1 => Team2Player1Id,
                QuixoDotOwner.Team2Player2 => Team2Player2Id,
                _ => string.Empty
            };
        }

        public static TeamId TeamForDotOwner(QuixoDotOwner owner)
        {
            return owner == QuixoDotOwner.Team1Player1 || owner == QuixoDotOwner.Team1Player2
                ? TeamId.Team1
                : owner == QuixoDotOwner.Team2Player1 || owner == QuixoDotOwner.Team2Player2
                    ? TeamId.Team2
                    : TeamId.None;
        }

        public static bool TryParseDotOwner(string value, out QuixoDotOwner owner)
        {
            if (Enum.TryParse(value, true, out owner))
            {
                return true;
            }

            owner = QuixoDotOwner.None;
            return false;
        }

        public static string BoardSideForUser(string userId)
        {
            return DotOwnerForUser(userId) switch
            {
                QuixoDotOwner.Team1Player1 => "Bas",
                QuixoDotOwner.Team2Player1 => "Droite",
                QuixoDotOwner.Team1Player2 => "Haut",
                QuixoDotOwner.Team2Player2 => "Gauche",
                _ => "Inconnue"
            };
        }

        public static string BoardSideForDotOwner(QuixoDotOwner owner)
        {
            return owner switch
            {
                QuixoDotOwner.Team1Player1 => "Bas",
                QuixoDotOwner.Team2Player1 => "Droite",
                QuixoDotOwner.Team1Player2 => "Haut",
                QuixoDotOwner.Team2Player2 => "Gauche",
                _ => "Inconnue"
            };
        }

        public static int TurnIndexForUser(string userId)
        {
            if (userId == Team1Player1Id)
            {
                return 0;
            }

            if (userId == Team2Player1Id)
            {
                return 1;
            }

            if (userId == Team1Player2Id)
            {
                return 2;
            }

            return userId == Team2Player2Id ? 3 : 0;
        }

        public static string UsernameForUser(string userId)
        {
            if (userId == Team1Player1Id)
            {
                return SafeName(Team1Player1Username, userId);
            }

            if (userId == Team1Player2Id)
            {
                return SafeName(Team1Player2Username, userId);
            }

            if (userId == Team2Player1Id)
            {
                return SafeName(Team2Player1Username, userId);
            }

            if (userId == Team2Player2Id)
            {
                return SafeName(Team2Player2Username, userId);
            }

            return ShortId(userId);
        }

        public static string TeamLabel(TeamId team)
        {
            return team == TeamId.Team1
                ? $"{UsernameForUser(Team1Player1Id)} + {UsernameForUser(Team1Player2Id)}"
                : team == TeamId.Team2
                    ? $"{UsernameForUser(Team2Player1Id)} + {UsernameForUser(Team2Player2Id)}"
                    : "equipe inconnue";
        }

        public static string TeammateOf(string userId)
        {
            if (userId == Team1Player1Id)
            {
                return Team1Player2Id;
            }

            if (userId == Team1Player2Id)
            {
                return Team1Player1Id;
            }

            if (userId == Team2Player1Id)
            {
                return Team2Player2Id;
            }

            return userId == Team2Player2Id ? Team2Player1Id : string.Empty;
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

        private static string FirstOpponentOf(string userId)
        {
            TeamId team = TeamForUser(userId);
            return OpposingTeam(team) == TeamId.Team1 ? Team1Player1Id : Team2Player1Id;
        }

        private static void ApplyLobbyNames(TeamLobbySnapshot lobby)
        {
            Team1Player1Username = ReadLobbyName(lobby, TeamId.Team1, 0);
            Team1Player2Username = ReadLobbyName(lobby, TeamId.Team1, 1);
            Team2Player1Username = ReadLobbyName(lobby, TeamId.Team2, 0);
            Team2Player2Username = ReadLobbyName(lobby, TeamId.Team2, 1);
        }

        private static string ReadLobbyName(TeamLobbySnapshot lobby, TeamId team, int slotIndex)
        {
            var player = lobby?.GetPlayer(team, slotIndex);
            return player != null && !string.IsNullOrWhiteSpace(player.username)
                ? player.username
                : ShortId(player?.user_id);
        }

        private static string SafeName(string username, string userId)
        {
            return string.IsNullOrWhiteSpace(username) ? ShortId(userId) : username;
        }
    }
}
