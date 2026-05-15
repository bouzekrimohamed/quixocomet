using System.Collections.Generic;
using QuixoUnity.Core;

namespace QuixoUnity.Gameplay
{
    public sealed class QometGameRulesAdapter : IGameRules
    {
        public GameKind Kind => GameKind.Qomet;
        public int BoardSize => QometRules.BoardSize;

        public int Player1Reserve { get; private set; } = QometRules.StarReservePerPlayer;
        public int Player2Reserve { get; private set; } = QometRules.StarReservePerPlayer;
        public string LastMessage { get; private set; } = string.Empty;

        private QometMoveRecord? _lastMove;

        public void SetupInitialState(BoardState state)
        {
            state.Reset();
            Player1Reserve = QometRules.StarReservePerPlayer;
            Player2Reserve = QometRules.StarReservePerPlayer;
            _lastMove = null;
            LastMessage = "Plateau vide. Posez une etoile ou selectionnez une etoile deja posee.";
        }

        public bool TrySelect(BoardState state, int row, int col)
        {
            return QometRules.CanSelect(state, row, col);
        }

        public IReadOnlyList<MoveDirection> GetDirections(BoardState state, int row, int col)
        {
            return new List<MoveDirection>();
        }

        public bool TryDirectionalMove(BoardState state, int row, int col, MoveDirection direction)
        {
            return false;
        }

        public bool TryMoveToCell(BoardState state, int srcRow, int srcCol, int dstRow, int dstCol)
        {
            int player1Reserve = Player1Reserve;
            int player2Reserve = Player2Reserve;
            bool moved = QometRules.TryMove(
                state,
                srcRow,
                srcCol,
                dstRow,
                dstCol,
                _lastMove,
                ref player1Reserve,
                ref player2Reserve,
                out var moveRecord,
                out var message);

            LastMessage = message;
            if (moved)
            {
                Player1Reserve = player1Reserve;
                Player2Reserve = player2Reserve;
                _lastMove = moveRecord;
            }

            return moved;
        }

        public PlayerMark EvaluateWinner(BoardState state, PlayerMark movedPlayer)
        {
            PlayerMark winner = QometRules.CheckWinner(state, movedPlayer);
            if (winner != PlayerMark.None
                && QometRules.TryFindWinningSquare(state, winner, out var squareIds))
            {
                string colorLabel = winner == PlayerMark.Player1 ? "jaune" : "rouge";
                LastMessage = $"Victoire {colorLabel} : carre {QometRules.FormatSquare(squareIds)}.";
            }

            return winner;
        }

        public bool TryPlace(BoardState state, int row, int col)
        {
            int reserve = GetReserve(state.CurrentPlayer);
            bool placed = QometRules.TryPlace(state, row, col, ref reserve, out var message);
            LastMessage = message;
            if (!placed)
            {
                return false;
            }

            SetReserve(state.CurrentPlayer, reserve);
            _lastMove = null;
            return true;
        }

        public bool HasReserve(PlayerMark player)
        {
            return GetReserve(player) > 0;
        }

        public string ReserveStatus()
        {
            return $"Reserve jaune: {Player1Reserve} | Reserve rouge: {Player2Reserve}";
        }

        private int GetReserve(PlayerMark player)
        {
            return player == PlayerMark.Player1 ? Player1Reserve : Player2Reserve;
        }

        private void SetReserve(PlayerMark player, int reserve)
        {
            if (player == PlayerMark.Player1)
            {
                Player1Reserve = reserve;
                return;
            }

            Player2Reserve = reserve;
        }
    }
}
