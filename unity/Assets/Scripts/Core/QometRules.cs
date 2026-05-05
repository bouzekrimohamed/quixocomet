namespace QuixoUnity.Core
{
    public static class QometRules
    {
        public static bool InBounds(int size, int row, int col)
        {
            return row >= 0 && row < size && col >= 0 && col < size;
        }

        public static bool CanSelect(BoardState state, int row, int col)
        {
            return state.Cells[row, col] == state.CurrentPlayer;
        }

        public static bool CanMove(BoardState state, int srcRow, int srcCol, int dstRow, int dstCol)
        {
            if (!InBounds(state.Size, dstRow, dstCol)) return false;
            if (state.Cells[dstRow, dstCol] != PlayerMark.None) return false;
            return Abs(srcRow - dstRow) + Abs(srcCol - dstCol) == 1;
        }

        public static bool ApplyMove(BoardState state, int srcRow, int srcCol, int dstRow, int dstCol)
        {
            if (!CanSelect(state, srcRow, srcCol)) return false;
            if (!CanMove(state, srcRow, srcCol, dstRow, dstCol)) return false;

            state.Cells[srcRow, srcCol] = PlayerMark.None;
            state.Cells[dstRow, dstCol] = state.CurrentPlayer;
            return true;
        }

        public static PlayerMark CheckWinner(BoardState state, PlayerMark movedPlayer)
        {
            int targetRow = movedPlayer == PlayerMark.Player1 ? state.Size - 1 : 0;
            for (int col = 0; col < state.Size; col++)
            {
                if (state.Cells[targetRow, col] == movedPlayer)
                {
                    return movedPlayer;
                }
            }

            var other = movedPlayer == PlayerMark.Player1 ? PlayerMark.Player2 : PlayerMark.Player1;
            if (!HasLegalMove(state, other))
            {
                return movedPlayer;
            }

            return PlayerMark.None;
        }

        private static bool HasLegalMove(BoardState state, PlayerMark player)
        {
            int[] dr = { -1, 1, 0, 0 };
            int[] dc = { 0, 0, -1, 1 };
            for (int r = 0; r < state.Size; r++)
            {
                for (int c = 0; c < state.Size; c++)
                {
                    if (state.Cells[r, c] != player) continue;
                    for (int i = 0; i < 4; i++)
                    {
                        int nr = r + dr[i];
                        int nc = c + dc[i];
                        if (InBounds(state.Size, nr, nc) && state.Cells[nr, nc] == PlayerMark.None)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static int Abs(int value)
        {
            return value < 0 ? -value : value;
        }
    }
}
