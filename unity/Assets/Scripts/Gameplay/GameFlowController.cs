using System.Collections.Generic;
using QuixoUnity.Core;
using QuixoUnity.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuixoUnity.Gameplay
{
    public sealed class GameFlowController : MonoBehaviour
    {
        private const string MenuSceneName = "MenuScene";

        [SerializeField] private GameKind gameKind = GameKind.Quixo;
        [SerializeField] private BoardViewRenderer boardView = null!;
        [SerializeField] private HudView hudView = null!;

        private IGameRules _rules = null!;
        private BoardState _state = null!;
        private Vector2Int? _selected;
        private bool _ready;
        private bool _loadingMenu;

        private void Start()
        {
            StartGame(SceneTransit.SelectedGame, OpeningMessage(SceneTransit.SelectedGame));
        }

        public void SelectGame(GameKind kind)
        {
            StartGame(kind, OpeningMessage(kind));
        }

        public void RestartGame()
        {
            if (!ResolveReferences())
            {
                _ready = false;
                return;
            }

            StartGame(gameKind, "Partie reinitialisee.");
        }

        public void PlayDirection(MoveDirection direction)
        {
            if (!EnsureReady())
            {
                return;
            }

            if (gameKind != GameKind.Quixo)
            {
                hudView.SetInfo("Ce mode se joue en cliquant une case voisine vide.");
                return;
            }

            if (_state.Winner != PlayerMark.None)
            {
                hudView.SetInfo($"{WinnerMessage(_state.Winner)} Recommencez ou revenez au menu.");
                return;
            }

            if (_selected == null)
            {
                hudView.SetInfo("Selectionnez un cube du bord avant de choisir une direction.");
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
            if (!EnsureReady())
            {
                return;
            }

            if (_state.Winner != PlayerMark.None)
            {
                hudView.SetInfo($"{WinnerMessage(_state.Winner)} Recommencez ou revenez au menu.");
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
                var directions = _rules.GetDirections(_state, row, col);
                hudView.SetDirections(directions);
                hudView.SetInfo(gameKind == GameKind.Qomet
                    ? "Piece selectionnee. Cliquez une case voisine vide."
                    : SelectionMessage(directions));
                boardView.Render(_state, _selected);
                return;
            }

            if (gameKind == GameKind.Qomet)
            {
                var src = _selected.Value;
                if (src.x == row && src.y == col)
                {
                    _selected = null;
                    hudView.SetDirections(new List<MoveDirection>());
                    hudView.SetInfo("Selection annulee.");
                    boardView.Render(_state, null);
                    return;
                }

                bool moved = _rules.TryMoveToCell(_state, src.x, src.y, row, col);
                if (!moved)
                {
                    if (_rules.TrySelect(_state, row, col))
                    {
                        _selected = new Vector2Int(row, col);
                        hudView.SetInfo("Nouvelle piece selectionnee. Cliquez une case voisine vide.");
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
                var directions = _rules.GetDirections(_state, row, col);
                hudView.SetDirections(directions);
                hudView.SetInfo(SelectionMessage(directions));
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
                hudView.SetInfo(WinnerMessage(winner));
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

        public void ReturnToMenu()
        {
            if (_loadingMenu)
            {
                return;
            }

            if (Application.CanStreamedLevelBeLoaded(MenuSceneName))
            {
                _loadingMenu = true;
                SceneManager.LoadScene(MenuSceneName);
                return;
            }

            Debug.LogWarning($"Scene '{MenuSceneName}' introuvable. Ajoutez-la aux Build Settings.", this);
            if (hudView != null)
            {
                hudView.SetInfo("Scene MenuScene introuvable. Verifiez les Build Settings.");
            }
        }

        private void StartGame(GameKind kind, string message)
        {
            _ready = false;
            if (!ResolveReferences())
            {
                enabled = false;
                return;
            }

            gameKind = kind;
            _rules = CreateRules(gameKind);
            _state = new BoardState(_rules.BoardSize);
            _rules.SetupInitialState(_state);
            _selected = null;
            _loadingMenu = false;

            hudView.Bind(this);
            hudView.SetDirections(new List<MoveDirection>());
            boardView.Initialize(_state.Size, HandleCellClick);
            boardView.Render(_state, null);
            hudView.SetTurn(_state.CurrentPlayer);
            hudView.SetInfo(message);
            _ready = true;
        }

        private bool EnsureReady()
        {
            if (_ready && _rules != null && _state != null && boardView != null && hudView != null)
            {
                return true;
            }

            Debug.LogWarning("GameFlowController called before the game was ready.", this);
            return false;
        }

        private bool ResolveReferences()
        {
            if (boardView == null)
            {
                boardView = FindObjectOfType<BoardViewRenderer>();
            }

            if (hudView == null)
            {
                hudView = FindObjectOfType<HudView>();
            }

            if (boardView != null && hudView != null)
            {
                return true;
            }

            Debug.LogError("GameFlowController requires BoardViewRenderer and HudView references.", this);
            return false;
        }

        private static string OpeningMessage(GameKind kind)
        {
            return kind == GameKind.Quixo
                ? "Choisissez un cube du bord libre ou a vous, puis une direction."
                : "Choisissez une piece a vous, puis cliquez une case voisine vide.";
        }

        private static string SelectionMessage(IReadOnlyList<MoveDirection> directions)
        {
            if (directions == null || directions.Count == 0)
            {
                return "Piece selectionnee.";
            }

            return "Piece selectionnee. Choisissez une direction.";
        }

        private static string WinnerMessage(PlayerMark winner)
        {
            return $"Victoire: {FormatPlayer(winner)}";
        }

        private static string FormatPlayer(PlayerMark player)
        {
            return player == PlayerMark.Player1 ? "Joueur 1 (X)" : "Joueur 2 (O)";
        }
    }
}
