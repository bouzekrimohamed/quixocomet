using System;

namespace QuixoUnity.Core
{
    public enum QuixoDotOwner
    {
        None = 0,
        Team1Player1 = 1,
        Team1Player2 = 2,
        Team2Player1 = 3,
        Team2Player2 = 4
    }

    [Serializable]
    public sealed class BoardState
    {
        public int Size { get; }
        public PlayerMark[,] Cells { get; }
        public QuixoDotOwner[,] DotOwners { get; }
        public PlayerMark CurrentPlayer { get; private set; }
        public PlayerMark Winner { get; private set; }

        public BoardState(int size)
        {
            Size = size;
            Cells = new PlayerMark[size, size];
            DotOwners = new QuixoDotOwner[size, size];
            CurrentPlayer = PlayerMark.Player1;
            Winner = PlayerMark.None;
        }

        public void SetCurrentPlayer(PlayerMark player)
        {
            CurrentPlayer = player;
        }

        public void SetWinner(PlayerMark winner)
        {
            Winner = winner;
        }

        public void Reset()
        {
            for (int r = 0; r < Size; r++)
            {
                for (int c = 0; c < Size; c++)
                {
                    Cells[r, c] = PlayerMark.None;
                    DotOwners[r, c] = QuixoDotOwner.None;
                }
            }

            CurrentPlayer = PlayerMark.Player1;
            Winner = PlayerMark.None;
        }

        public BoardState Clone()
        {
            var clone = new BoardState(Size);
            for (int r = 0; r < Size; r++)
            {
                for (int c = 0; c < Size; c++)
                {
                    clone.Cells[r, c] = Cells[r, c];
                    clone.DotOwners[r, c] = DotOwners[r, c];
                }
            }

            clone.CurrentPlayer = CurrentPlayer;
            clone.Winner = Winner;
            return clone;
        }
    }
}
