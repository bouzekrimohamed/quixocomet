using System.Collections.Generic;
using QuixoUnity.Core;

namespace QuixoUnity.Gameplay
{
    public interface IGameRules
    {
        GameKind Kind { get; }
        int BoardSize { get; }
        void SetupInitialState(BoardState state);
        bool TrySelect(BoardState state, int row, int col);
        IReadOnlyList<MoveDirection> GetDirections(BoardState state, int row, int col);
        bool TryDirectionalMove(BoardState state, int row, int col, MoveDirection direction);
        bool TryMoveToCell(BoardState state, int srcRow, int srcCol, int dstRow, int dstCol);
        PlayerMark EvaluateWinner(BoardState state, PlayerMark movedPlayer);
    }
}
