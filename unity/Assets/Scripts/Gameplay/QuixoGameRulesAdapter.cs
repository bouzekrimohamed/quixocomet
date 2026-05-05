using System.Collections.Generic;
using QuixoUnity.Core;

namespace QuixoUnity.Gameplay
{
    public sealed class QuixoGameRulesAdapter : IGameRules
    {
        public GameKind Kind => GameKind.Quixo;
        public int BoardSize => 5;

        public void SetupInitialState(BoardState state)
        {
            state.Reset();
        }

        public bool TrySelect(BoardState state, int row, int col)
        {
            return QuixoRules.CanSelect(state, row, col);
        }

        public IReadOnlyList<MoveDirection> GetDirections(BoardState state, int row, int col)
        {
            return QuixoRules.AllowedDirections(state, row, col);
        }

        public bool TryDirectionalMove(BoardState state, int row, int col, MoveDirection direction)
        {
            return QuixoRules.ApplyMove(state, row, col, direction);
        }

        public bool TryMoveToCell(BoardState state, int srcRow, int srcCol, int dstRow, int dstCol)
        {
            return false;
        }

        public PlayerMark EvaluateWinner(BoardState state, PlayerMark movedPlayer)
        {
            return QuixoRules.CheckWinner(state, movedPlayer);
        }
    }
}
