using System.Collections.Generic;
using QuixoUnity.Core;

namespace QuixoUnity.Gameplay
{
    public sealed class QometGameRulesAdapter : IGameRules
    {
        public GameKind Kind => GameKind.Qomet;
        public int BoardSize => 5;

        public void SetupInitialState(BoardState state)
        {
            state.Reset();
            for (int c = 0; c < state.Size; c++)
            {
                state.Cells[0, c] = PlayerMark.Player1;
                state.Cells[state.Size - 1, c] = PlayerMark.Player2;
            }
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
            return QometRules.ApplyMove(state, srcRow, srcCol, dstRow, dstCol);
        }

        public PlayerMark EvaluateWinner(BoardState state, PlayerMark movedPlayer)
        {
            return QometRules.CheckWinner(state, movedPlayer);
        }
    }
}
