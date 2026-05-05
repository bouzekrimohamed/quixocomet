import sys

from PySide6.QtWidgets import QApplication

from app.logic import qomet_rules, quixo_rules
from app.models.base import GameType
from app.models.qomet_model import QometModel
from app.models.quixo_model import QuixoModel
from app.views.main_window import MainWindow
from app.views.style_system import apply_theme


class GameController:
    def __init__(self) -> None:
        self.app = QApplication(sys.argv)
        self.active_game: GameType | None = None
        self.qomet = QometModel()
        self.quixo = QuixoModel()
        self.theme_name = "Clair"
        self.mat_name = "Bois"

        self.window = MainWindow(
            on_play=self.start_game,
            on_quit=self.app.quit,
            on_cell_click=self.handle_cell_click,
            on_direction_click=self.handle_direction_click,
            on_reset=self.reset_current_game,
            on_back=self.back_to_menu,
            on_theme_change=self.change_theme,
            on_mat_change=self.change_mat,
        )
        apply_theme(self.app, self.theme_name)
        self.window.game_view.set_mat(self.mat_name)
        self.window.show_menu()

    def run(self) -> None:
        self.window.show()
        self.app.exec()

    def start_game(self, game_name: str) -> None:
        self.active_game = GameType(game_name)
        self.reset_current_game()
        self.window.game_view.set_game_title(f"Jeu : {self.active_game.value}")
        self.window.show_game()

    def reset_current_game(self) -> None:
        if self.active_game == GameType.QOMET:
            self.qomet.reset()
            self.window.game_view.build_board(self.qomet.size)
            self.window.game_view.set_direction_visible(False)
            self._refresh_qomet(message="Cliquez une piece, puis une case voisine vide.")
        elif self.active_game == GameType.QUIXO:
            self.quixo.reset()
            self.window.game_view.build_board(self.quixo.size)
            self.window.game_view.set_direction_visible(True)
            self.window.game_view.set_direction_enabled([])
            self._refresh_quixo(message="Choisissez un cube du bord puis une direction.")

    def back_to_menu(self) -> None:
        self.active_game = None
        self.window.show_menu()

    def change_theme(self, theme_name: str) -> None:
        self.theme_name = theme_name
        apply_theme(self.app, self.theme_name)

    def change_mat(self, mat_name: str) -> None:
        self.mat_name = mat_name
        self.window.game_view.set_mat(self.mat_name)
        if self.active_game == GameType.QOMET:
            self._refresh_qomet("Tapis mis a jour.")
        elif self.active_game == GameType.QUIXO:
            self._refresh_quixo("Tapis mis a jour.")

    def handle_cell_click(self, row: int, col: int) -> None:
        if self.active_game == GameType.QOMET:
            self._handle_qomet_click(row, col)
        elif self.active_game == GameType.QUIXO:
            self._handle_quixo_click(row, col)

    def handle_direction_click(self, direction: str) -> None:
        if self.active_game != GameType.QUIXO:
            return
        if self.quixo.selected is None:
            self._refresh_quixo(message="Selectionnez d'abord un cube valide.")
            return

        row, col = self.quixo.selected
        moved = quixo_rules.apply_move(self.quixo, row, col, direction)
        if not moved:
            self.quixo.selected = None
            self.window.game_view.set_direction_enabled([])
            self._refresh_quixo(message="Mouvement invalide, selection annulee.")
            return

        player = self.quixo.status.current_player
        winner = quixo_rules.check_winner(self.quixo, player)
        self.quixo.selected = None
        self.window.game_view.set_direction_enabled([])
        if winner is not None:
            self._refresh_quixo(message=f"Victoire Joueur {winner}")
            self._on_game_over(winner)
            return

        self.quixo.status.current_player = 2 if player == 1 else 1
        self._refresh_quixo(message="Coup valide.")

    def _handle_qomet_click(self, row: int, col: int) -> None:
        model = self.qomet
        selected=model.selected

        if selected is None:
            if qomet_rules.can_select(model, row, col):
                model.selected = (row, col)
                self._refresh_qomet(message="Piece selectionnee.")
            else:
                self._refresh_qomet(message="Selection invalide.")
            return

        if selected == (row, col):
            model.selected = None
            self._refresh_qomet(message="Selection annulee.")
            return

        if not qomet_rules.can_move(model, selected, (row, col)):
            if qomet_rules.can_select(model, row, col):
                model.selected = (row, col)
                self._refresh_qomet(message="Nouvelle piece selectionnee.")
            else:
                model.selected = None
                self._refresh_qomet(message="Deplacement invalide.")
            return

        sr, sc = selected
        player = model.status.current_player
        model.board[sr][sc] = 0
        model.board[row][col] = player
        model.selected = None

        winner = qomet_rules.check_winner(model, player)
        if winner is not None:
            self._refresh_qomet(message=f"Victoire Joueur {winner}")
            self._on_game_over(winner)
            return

        model.status.current_player = 2 if player == 1 else 1
        self._refresh_qomet(message="Coup valide.")

    def _handle_quixo_click(self, row: int, col: int) -> None:
        model = self.quixo
        if not quixo_rules.can_select(model, row, col):
            model.selected = None
            self.window.game_view.set_direction_enabled([])
            self._refresh_quixo(message="Cube invalide: choisissez un cube de bord libre ou a vous.")
            return

        model.selected = (row, col)
        allowed = quixo_rules.allowed_directions(model, row, col)
        self.window.game_view.set_direction_enabled(allowed)
        self._refresh_quixo(message=f"Cube selectionne ({row}, {col}). Choisissez une direction.")

    def _refresh_qomet(self, message: str = "") -> None:
        model = self.qomet
        self.window.game_view.set_mat(self.mat_name)
        self.window.game_view.render_board(model.board, model.selected)
        self.window.game_view.set_turn(model.status.current_player)
        self.window.game_view.set_info(message)

    def _refresh_quixo(self, message: str = "") -> None:
        model = self.quixo
        self.window.game_view.set_mat(self.mat_name)
        self.window.game_view.render_board(model.board, model.selected)
        self.window.game_view.set_turn(model.status.current_player)
        self.window.game_view.set_info(message)

    def _on_game_over(self, winner: int) -> None:
        action = self.window.game_view.show_winner_dialog(winner)
        if action == "replay":
            self.reset_current_game()
        else:
            self.back_to_menu()
