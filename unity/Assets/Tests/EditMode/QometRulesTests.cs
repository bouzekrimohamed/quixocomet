using NUnit.Framework;
using QuixoUnity.Core;
using QuixoUnity.Gameplay;

namespace QuixoUnity.Tests.EditMode
{
    public sealed class QometRulesTests
    {
        [Test]
        public void SetupInitialState_PlacesBothLines()
        {
            var state = new BoardState(5);
            var rules = new QometGameRulesAdapter();
            rules.SetupInitialState(state);

            for (int c = 0; c < 5; c++)
            {
                Assert.That(state.Cells[0, c], Is.EqualTo(PlayerMark.Player1));
                Assert.That(state.Cells[4, c], Is.EqualTo(PlayerMark.Player2));
            }
        }

        [Test]
        public void CanMove_OnlyOrthogonalAdjacent()
        {
            var state = new BoardState(5);
            var rules = new QometGameRulesAdapter();
            rules.SetupInitialState(state);
            state.Cells[1, 0] = PlayerMark.None;

            Assert.That(QometRules.CanMove(state, 0, 0, 1, 0), Is.True);
            Assert.That(QometRules.CanMove(state, 0, 0, 1, 1), Is.False);
        }

        [Test]
        public void CheckWinner_TargetRowReached()
        {
            var state = new BoardState(5);
            state.Cells[4, 3] = PlayerMark.Player1;

            var winner = QometRules.CheckWinner(state, PlayerMark.Player1);
            Assert.That(winner, Is.EqualTo(PlayerMark.Player1));
        }
    }
}
