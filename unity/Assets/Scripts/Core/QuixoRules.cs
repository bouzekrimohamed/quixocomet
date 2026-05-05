using System.Collections.Generic;

namespace QuixoUnity.Core
{
    public static class QuixoRules
    {
        public static bool IsBorder(int size, int row, int col)
        {
            return row == 0 || col == 0 || row == size - 1 || col == size - 1;
        }

        public static bool CanSelect(BoardState state, int row, int col)
        {
            var value = state.Cells[row, col];
            return IsBorder(state.Size, row, col) && (value == PlayerMark.None || value == state.CurrentPlayer);
        }

        public static List<MoveDirection> AllowedDirections(BoardState state, int row, int col)
        {
            var result = new List<MoveDirection>();
            if (!CanSelect(state, row, col))
            {
                return result;
            }

            if (row < state.Size - 1) result.Add(MoveDirection.Up);
            if (row > 0) result.Add(MoveDirection.Down);
            if (col < state.Size - 1) result.Add(MoveDirection.Left);
            if (col > 0) result.Add(MoveDirection.Right);
            return result;
        }

        public static bool ApplyMove(BoardState state, int row, int col, MoveDirection direction)
        {
            if (!AllowedDirections(state, row, col).Contains(direction))
            {
                return false;
            }

            var player = state.CurrentPlayer;
            if (direction == MoveDirection.Down)
            {
                for (int r = row; r > 0; r--)
                {
                    state.Cells[r, col] = state.Cells[r - 1, col];
                }

                state.Cells[0, col] = player;
                return true;
            }

            if (direction == MoveDirection.Up)
            {
                for (int r = row; r < state.Size - 1; r++)
                {
                    state.Cells[r, col] = state.Cells[r + 1, col];
                }

                state.Cells[state.Size - 1, col] = player;
                return true;
            }

            if (direction == MoveDirection.Right)
            {
                for (int c = col; c > 0; c--)
                {
                    state.Cells[row, c] = state.Cells[row, c - 1];
                }

                state.Cells[row, 0] = player;
                return true;
            }

            for (int c = col; c < state.Size - 1; c++)
            {
                state.Cells[row, c] = state.Cells[row, c + 1];
            }

            state.Cells[row, state.Size - 1] = player;
            return true;
        }

        public static PlayerMark CheckWinner(BoardState state, PlayerMark movedPlayer)
        {
            var opponent = movedPlayer == PlayerMark.Player1 ? PlayerMark.Player2 : PlayerMark.Player1;
            bool playerHasLine = false;
            bool opponentHasLine = false;

            for (int row = 0; row < state.Size; row++)
            {
                if (CheckRow(state, row, movedPlayer)) playerHasLine = true;
                if (CheckRow(state, row, opponent)) opponentHasLine = true;
            }

            for (int col = 0; col < state.Size; col++)
            {
                if (CheckColumn(state, col, movedPlayer)) playerHasLine = true;
                if (CheckColumn(state, col, opponent)) opponentHasLine = true;
            }

            if (CheckDiagMain(state, movedPlayer) || CheckDiagSecond(state, movedPlayer))
            {
                playerHasLine = true;
            }

            if (CheckDiagMain(state, opponent) || CheckDiagSecond(state, opponent))
            {
                opponentHasLine = true;
            }

            if (opponentHasLine) return opponent;
            if (playerHasLine) return movedPlayer;
            return PlayerMark.None;
        }

        private static bool CheckRow(BoardState state, int row, PlayerMark player)
        {
            for (int col = 0; col < state.Size; col++)
            {
                if (state.Cells[row, col] != player) return false;
            }

            return true;
        }

        private static bool CheckColumn(BoardState state, int col, PlayerMark player)
        {
            for (int row = 0; row < state.Size; row++)
            {
                if (state.Cells[row, col] != player) return false;
            }

            return true;
        }

        private static bool CheckDiagMain(BoardState state, PlayerMark player)
        {
            for (int i = 0; i < state.Size; i++)
            {
                if (state.Cells[i, i] != player) return false;
            }

            return true;
        }

        private static bool CheckDiagSecond(BoardState state, PlayerMark player)
        {
            for (int i = 0; i < state.Size; i++)
            {
                if (state.Cells[i, state.Size - 1 - i] != player) return false;
            }

            return true;
        }
    }
}
