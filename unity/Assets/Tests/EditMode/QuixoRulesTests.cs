using NUnit.Framework;
using QuixoUnity.Core;

namespace QuixoUnity.Tests.EditMode
{
    public sealed class QuixoRulesTests
    {
        [Test]
        public void CanSelect_BorderNeutral_ReturnsTrue()
        {
            var state = new BoardState(5);
            Assert.That(QuixoRules.CanSelect(state, 0, 2), Is.True);
        }

        [Test]
        public void CanSelect_CenterCell_ReturnsFalse()
        {
            var state = new BoardState(5);
            Assert.That(QuixoRules.CanSelect(state, 2, 2), Is.False);
        }

        [Test]
        public void ApplyMove_CannotReinsertSamePlace()
        {
            var state = new BoardState(5);
            var dirs = QuixoRules.AllowedDirections(state, 0, 2);
            Assert.That(dirs.Contains(MoveDirection.Down), Is.False);
        }

        [Test]
        public void CheckWinner_AdversaryLinePriority()
        {
            var state = new BoardState(5);
            state.SetCurrentPlayer(PlayerMark.Player1);
            for (int c = 0; c < 5; c++)
            {
                state.Cells[0, c] = PlayerMark.Player2;
            }

            var winner = QuixoRules.CheckWinner(state, PlayerMark.Player1);
            Assert.That(winner, Is.EqualTo(PlayerMark.Player2));
        }
    }
}
