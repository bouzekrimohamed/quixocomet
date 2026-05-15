using System.Collections.Generic;
using UnityEngine;

namespace QuixoUnity.Core
{
    public readonly struct QometMoveRecord
    {
        public readonly Vector2Int FromNode;
        public readonly Vector2Int ToNode;
        public readonly PlayerMark MovedPlayer;

        public QometMoveRecord(Vector2Int fromNode, Vector2Int toNode, PlayerMark movedPlayer)
        {
            FromNode = fromNode;
            ToNode = toNode;
            MovedPlayer = movedPlayer;
        }
    }

    public static class QometRules
    {
        public const int BoardSize = QometGraph.BoardSize;
        public const int StarReservePerPlayer = 7;

        // Carres gagnants Qomet : chaque entree est un quadruplet de noeuds (par Id)
        // qui forme un carre geometrique sur les VisualPosition ET dont les cotes
        // suivent les lignes visibles du plateau.
        // Liste pre-calculee : evite les "carres fantomes" detectes par calcul flottant.
        private static readonly string[][] WinningSquares =
        {
            // 3 carres concentriques axe-alignes
            new[] { "A", "C", "Y", "W" }, // grand carre exterieur (cote 6)
            new[] { "D", "F", "V", "T" }, // carre moyen (cote 4)
            new[] { "G", "I", "S", "Q" }, // petit carre interieur (cote 2)
            // 4 mini carres axe-alignes autour du centre M (cote 2)
            new[] { "D", "E", "M", "L" },
            new[] { "E", "F", "N", "M" },
            new[] { "L", "M", "U", "T" },
            new[] { "M", "N", "V", "U" },
            // Losanges (carres a 45 degres) centres ou decentres
            new[] { "E", "N", "U", "L" }, // losange moyen
            new[] { "A", "M", "W", "J" }, // grand losange gauche
            new[] { "C", "P", "Y", "M" }, // grand losange droit
            new[] { "D", "M", "T", "K" }, // losange moyen gauche
            new[] { "F", "O", "V", "M" }, // losange moyen droit
            new[] { "E", "I", "M", "G" }, // petit losange haut
            new[] { "I", "N", "S", "M" }, // petit losange droit
            new[] { "M", "S", "U", "Q" }, // petit losange bas
            new[] { "G", "M", "Q", "L" }, // petit losange gauche
        };

        public static bool InBounds(int size, int row, int col)
        {
            return size == BoardSize && QometGraph.IsValidNode(row, col);
        }

        public static bool CanSelect(BoardState state, int row, int col)
        {
            return state != null
                && InBounds(state.Size, row, col)
                && state.Cells[row, col] == state.CurrentPlayer;
        }

        public static bool CanMove(BoardState state, int srcRow, int srcCol, int dstRow, int dstCol)
        {
            return state != null && QometGraph.AreConnected(srcRow, srcCol, dstRow, dstCol);
        }

        public static bool CanPlace(BoardState state, int row, int col, int reserve)
        {
            return state != null
                && reserve > 0
                && InBounds(state.Size, row, col)
                && state.Cells[row, col] == PlayerMark.None;
        }

        public static bool TryPlace(BoardState state, int row, int col, ref int reserve, out string message)
        {
            if (!CanPlace(state, row, col, reserve))
            {
                message = reserve <= 0 ? "Reserve vide: deplacez une etoile." : "Pose impossible sur cet emplacement.";
                return false;
            }

            state.Cells[row, col] = state.CurrentPlayer;
            reserve--;
            message = "Etoile posee.";
            return true;
        }

        public static bool TryMove(
            BoardState state,
            int srcRow,
            int srcCol,
            int dstRow,
            int dstCol,
            QometMoveRecord? lastMove,
            ref int player1Reserve,
            ref int player2Reserve,
            out QometMoveRecord moveRecord,
            out string message)
        {
            moveRecord = default;
            if (state == null)
            {
                message = "Partie Qomet non initialisee.";
                return false;
            }

            if (!CanSelect(state, srcRow, srcCol))
            {
                message = "Selectionnez une etoile de votre couleur.";
                return false;
            }

            if (!InBounds(state.Size, dstRow, dstCol))
            {
                message = "Emplacement Qomet invalide.";
                return false;
            }

            if (!QometGraph.AreConnected(srcRow, srcCol, dstRow, dstCol))
            {
                message = "Les deux emplacements ne sont pas relies.";
                return false;
            }

            var from = new Vector2Int(srcRow, srcCol);
            var to = new Vector2Int(dstRow, dstCol);
            if (IsImmediateReverse(lastMove, from, to))
            {
                message = "Coup inverse immediat interdit.";
                return false;
            }

            PlayerMark movingPlayer = state.CurrentPlayer;
            PlayerMark target = state.Cells[dstRow, dstCol];
            if (target == PlayerMark.None)
            {
                state.Cells[srcRow, srcCol] = PlayerMark.None;
                state.Cells[dstRow, dstCol] = movingPlayer;
                moveRecord = new QometMoveRecord(from, to, movingPlayer);
                message = "Etoile deplacee.";
                return true;
            }

            if (QometGraph.TryGetNextInDirection(from, to, out var pushedDestination))
            {
                if (state.Cells[pushedDestination.x, pushedDestination.y] != PlayerMark.None)
                {
                    message = "Poussee interdite: une deuxieme etoile bloque la ligne.";
                    return false;
                }

                state.Cells[pushedDestination.x, pushedDestination.y] = target;
                message = "Poussee effectuee.";
            }
            else
            {
                AddReserve(target, ref player1Reserve, ref player2Reserve);
                message = "Poussee hors plateau: l'etoile retourne en reserve.";
            }

            state.Cells[srcRow, srcCol] = PlayerMark.None;
            state.Cells[dstRow, dstCol] = movingPlayer;
            moveRecord = new QometMoveRecord(from, to, movingPlayer);
            return true;
        }

        public static PlayerMark CheckWinner(BoardState state, PlayerMark movedPlayer)
        {
            if (state == null || movedPlayer == PlayerMark.None)
            {
                return PlayerMark.None;
            }

            PlayerMark opponent = OtherPlayer(movedPlayer);
            if (TryFindWinningSquare(state, opponent, out var opponentSquare))
            {
                Debug.Log($"[Qomet] Victoire {opponent} : carre {FormatSquare(opponentSquare)}");
                return opponent;
            }

            if (TryFindWinningSquare(state, movedPlayer, out var playerSquare))
            {
                Debug.Log($"[Qomet] Victoire {movedPlayer} : carre {FormatSquare(playerSquare)}");
                return movedPlayer;
            }

            return PlayerMark.None;
        }

        public static bool HasPerfectSquare(BoardState state, PlayerMark player)
        {
            return TryFindWinningSquare(state, player, out _);
        }

        public static bool TryFindWinningSquare(BoardState state, PlayerMark player, out string[] squareIds)
        {
            squareIds = null;
            if (state == null || player == PlayerMark.None)
            {
                return false;
            }

            foreach (var square in WinningSquares)
            {
                bool allOccupied = true;
                for (int i = 0; i < square.Length; i++)
                {
                    if (!QometGraph.TryGetNodeById(square[i], out var node)
                        || state.Cells[node.Row, node.Col] != player)
                    {
                        allOccupied = false;
                        break;
                    }
                }

                if (allOccupied)
                {
                    squareIds = square;
                    return true;
                }
            }

            return false;
        }

        public static string FormatSquare(string[] squareIds)
        {
            return squareIds == null ? string.Empty : string.Join("-", squareIds);
        }

        private static bool IsImmediateReverse(QometMoveRecord? lastMove, Vector2Int from, Vector2Int to)
        {
            if (!lastMove.HasValue)
            {
                return false;
            }

            var previous = lastMove.Value;
            return previous.ToNode == from && previous.FromNode == to;
        }

        private static void AddReserve(PlayerMark player, ref int player1Reserve, ref int player2Reserve)
        {
            if (player == PlayerMark.Player1)
            {
                player1Reserve = Mathf.Min(StarReservePerPlayer, player1Reserve + 1);
            }
            else if (player == PlayerMark.Player2)
            {
                player2Reserve = Mathf.Min(StarReservePerPlayer, player2Reserve + 1);
            }
        }

        private static PlayerMark OtherPlayer(PlayerMark player)
        {
            return player == PlayerMark.Player1 ? PlayerMark.Player2 : PlayerMark.Player1;
        }
    }
}
