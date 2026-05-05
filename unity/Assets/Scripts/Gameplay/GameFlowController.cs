using System.Collections.Generic;
using QuixoUnity.Core;
using QuixoUnity.UI;
using UnityEngine;

namespace QuixoUnity.Gameplay
{
    public sealed class GameFlowController : MonoBehaviour
    {
        [SerializeField] private GameKind gameKind = GameKind.Quixo;
        [SerializeField] private BoardViewRenderer boardView = null!;
        [SerializeField] private HudView hudView = null!;

        private IGameRules _rules = null!;
        private BoardState _state = null!;
        private Vector2Int? _selected;

        private void Start()
        {
            gameKind = SceneTransit.SelectedGame;
            _rules = CreateRules(gameKind);
            _state = new BoardState(_rules.BoardSize);
            _rules.SetupInitialState(_state);

            boardView.Initialize(_state.Size, HandleCellClick);
            boardView.Render(_state, null);
            hudView.Bind(this);
            hudView.SetTurn(_state.CurrentPlayer);
            hudView.SetInfo("Selectionnez une piece.");
            hudView.SetDirections(new List<MoveDirection>());
        }

        public void SelectGame(GameKind kind)
        {
            gameKind = kind;
            _rules = CreateRules(gameKind);
            _state = new BoardState(_rules.BoardSize);
            _rules.SetupInitialState(_state);
            _selected = null;
            boardView.Initialize(_state.Size, HandleCellClick);
            boardView.Render(_state, null);
            hudView.SetTurn(_state.CurrentPlayer);
            hudView.SetInfo("Nouvelle partie.");
            hudView.SetDirections(new List<MoveDirection>());
        }

        public void RestartGame()
        {
            _rules.SetupInitialState(_state);
            _selected = null;
            boardView.Render(_state, null);
            hudView.SetTurn(_state.CurrentPlayer);
            hudView.SetInfo("Partie reinitialisee.");
            hudView.SetDirections(new List<MoveDirection>());
        }

        public void PlayDirection(MoveDirection direction)
        {
            if (_selected == null || _state.Winner != PlayerMark.None)
            {
                return;
            }

            var origin = _selected.Value;
            bool moved = _rules.TryDirectionalMove(_state, origin.x, origin.y, direction);
            if (!moved)
            {
                hudView.SetInfo("Mouvement invalide.");
                return;
            }

            EndTurn(origin);
        }

        private void HandleCellClick(int row, int col)
        {
            if (_state.Winner != PlayerMark.None)
            {
                return;
            }

            if (_selected == null)
            {
                if (!_rules.TrySelect(_state, row, col))
                {
                    hudView.SetInfo("Piece invalide.");
                    return;
                }

                _selected = new Vector2Int(row, col);
                hudView.SetInfo("Piece selectionnee.");
                hudView.SetDirections(_rules.GetDirections(_state, row, col));
                boardView.Render(_state, _selected);
                return;
            }

            if (gameKind == GameKind.Qomet)
            {
                var src = _selected.Value;
                bool moved = _rules.TryMoveToCell(_state, src.x, src.y, row, col);
                if (!moved)
                {
                    if (_rules.TrySelect(_state, row, col))
                    {
                        _selected = new Vector2Int(row, col);
                        hudView.SetInfo("Nouvelle piece selectionnee.");
                        boardView.Render(_state, _selected);
                    }
                    else
                    {
                        _selected = null;
                        hudView.SetInfo("Deplacement invalide.");
                        boardView.Render(_state, null);
                    }

                    return;
                }

                EndTurn(src);
                return;
            }

            if (_rules.TrySelect(_state, row, col))
            {
                _selected = new Vector2Int(row, col);
                hudView.SetDirections(_rules.GetDirections(_state, row, col));
                hudView.SetInfo("Selection mise a jour.");
                boardView.Render(_state, _selected);
            }
            else
            {
                _selected = null;
                hudView.SetDirections(new List<MoveDirection>());
                boardView.Render(_state, null);
                hudView.SetInfo("Selection annulee.");
            }
        }

        private void EndTurn(Vector2Int fromCell)
        {
            var movedPlayer = _state.CurrentPlayer;
            var winner = _rules.EvaluateWinner(_state, movedPlayer);
            _selected = null;
            hudView.SetDirections(new List<MoveDirection>());
            boardView.AnimateBoardChange(_state, fromCell);

            if (winner != PlayerMark.None)
            {
                _state.SetWinner(winner);
                boardView.Render(_state, null);
                hudView.SetInfo($"Victoire: {winner}");
                hudView.SetTurn(_state.CurrentPlayer);
                return;
            }

            _state.SetCurrentPlayer(movedPlayer == PlayerMark.Player1 ? PlayerMark.Player2 : PlayerMark.Player1);
            boardView.Render(_state, null);
            hudView.SetTurn(_state.CurrentPlayer);
            hudView.SetInfo("Coup valide.");
        }

        private static IGameRules CreateRules(GameKind kind)
        {
            return kind == GameKind.Quixo ? new QuixoGameRulesAdapter() : new QometGameRulesAdapter();
        }
    }
}
