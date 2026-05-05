from PySide6.QtCore import Qt
from PySide6.QtWidgets import (
    QGridLayout,
    QHBoxLayout,
    QLabel,
    QMessageBox,
    QPushButton,
    QVBoxLayout,
    QWidget,
)

from app.views.style_system import get_mat_style


class GameView(QWidget):
    def __init__(self, on_cell_click, on_direction_click, on_reset, on_back) -> None:
        super().__init__()
        self.on_cell_click = on_cell_click
        self.on_direction_click = on_direction_click
        self.on_reset = on_reset
        self.on_back = on_back

        self.grid_buttons: list[list[QPushButton]] = []
        self.board_size = 0
        self.current_mat = "Bois"
        self.current_game = ""
        self._build_ui()

    def _build_ui(self) -> None:
        root = QVBoxLayout()
        root.setSpacing(16)
        root.setContentsMargins(24, 24, 24, 24)

        self.title_label = QLabel("Jeu")
        self.title_label.setStyleSheet("font-size: 24px; font-weight: bold;")
        self.turn_label = QLabel("Tour : Joueur 1")
        self.turn_label.setStyleSheet("font-size: 16px;")
        self.info_label = QLabel("")
        self.info_label.setStyleSheet("color: #666;")

        root.addWidget(self.title_label)
        root.addWidget(self.turn_label)
        root.addWidget(self.info_label)

        self.board_container = QWidget()
        self.grid_layout = QGridLayout()
        self.grid_layout.setSpacing(6)
        self.grid_layout.setContentsMargins(10, 10, 10, 10)
        self.board_container.setLayout(self.grid_layout)
        root.addWidget(self.board_container)

        dir_row = QHBoxLayout()
        self.direction_label = QLabel("Direction :")
        self.dir_up = QPushButton("Haut")
        self.dir_down = QPushButton("Bas")
        self.dir_left = QPushButton("Gauche")
        self.dir_right = QPushButton("Droite")
        for btn, key in [
            (self.dir_up, "UP"),
            (self.dir_down, "DOWN"),
            (self.dir_left, "LEFT"),
            (self.dir_right, "RIGHT"),
        ]:
            btn.clicked.connect(lambda _, k=key: self.on_direction_click(k))
            dir_row.addWidget(btn)
        dir_row.insertWidget(0, self.direction_label)
        root.addLayout(dir_row)

        actions = QHBoxLayout()
        self.reset_button = QPushButton("Reinitialiser")
        self.back_button = QPushButton("Retour au menu")
        self.reset_button.clicked.connect(self.on_reset)
        self.back_button.clicked.connect(self.on_back)
        actions.addWidget(self.reset_button)
        actions.addWidget(self.back_button)
        root.addLayout(actions)

        root.addStretch()
        self.setLayout(root)

    def build_board(self, size: int) -> None:
        self.board_size = size
        self.grid_buttons.clear()
        while self.grid_layout.count():
            child = self.grid_layout.takeAt(0)
            if child.widget():
                child.widget().deleteLater()

        for row in range(size):
            row_buttons: list[QPushButton] = []
            for col in range(size):
                btn = QPushButton("")
                btn.setFixedSize(72, 72)
                btn.clicked.connect(lambda _, r=row, c=col: self.on_cell_click(r, c))
                self.grid_layout.addWidget(btn, row, col, Qt.AlignmentFlag.AlignCenter)
                row_buttons.append(btn)
            self.grid_buttons.append(row_buttons)

    def set_game_title(self, game_name: str) -> None:
        self.title_label.setText(game_name)
        self.current_game = game_name.lower()

    def set_turn(self, current_player: int) -> None:
        self.turn_label.setText(f"Tour : Joueur {current_player}")

    def set_info(self, message: str) -> None:
        self.info_label.setText(message)

    def set_direction_visible(self, visible: bool) -> None:
        self.direction_label.setVisible(visible)
        self.dir_up.setVisible(visible)
        self.dir_down.setVisible(visible)
        self.dir_left.setVisible(visible)
        self.dir_right.setVisible(visible)

    def set_direction_enabled(self, allowed: list[str]) -> None:
        self.dir_up.setEnabled("UP" in allowed)
        self.dir_down.setEnabled("DOWN" in allowed)
        self.dir_left.setEnabled("LEFT" in allowed)
        self.dir_right.setEnabled("RIGHT" in allowed)

    def set_mat(self, mat_name: str) -> None:
        self.current_mat = mat_name
        mat = get_mat_style(mat_name)
        self.board_container.setStyleSheet(
            f"background: {mat.board_background}; border-radius: 14px; border: 1px solid {mat.border};"
        )

    def render_board(self, board: list[list[int]], selected: tuple[int, int] | None = None) -> None:
        mat = get_mat_style(self.current_mat)
        self.set_mat(self.current_mat)
        is_quixo = "quixo" in self.current_game
        for row in range(len(board)):
            for col in range(len(board[row])):
                value = board[row][col]
                btn = self.grid_buttons[row][col]
                if is_quixo:
                    mark = "" if value == 0 else ("X" if value == 1 else "O")
                else:
                    mark = "" if value == 0 else f"J{value}"
                btn.setText(mark)
                if value == 1:
                    color = mat.player1
                elif value == 2:
                    color = mat.player2
                else:
                    color = mat.empty_cell

                border = (
                    f"3px solid {mat.select_border}" if selected == (row, col) else f"1px solid {mat.border}"
                )
                btn.setStyleSheet(
                    f"background-color: {color}; color: {mat.text_color}; border: {border}; border-radius: 8px; font-weight: bold; font-size: 20px;"
                )

    def show_winner_dialog(self, winner: int) -> str:
        dialog = QMessageBox(self)
        dialog.setWindowTitle("Fin de partie")
        dialog.setText(f"Victoire Joueur {winner}")
        replay = dialog.addButton("Rejouer", QMessageBox.ButtonRole.AcceptRole)
        back = dialog.addButton("Retour au menu", QMessageBox.ButtonRole.RejectRole)
        dialog.exec()
        return "replay" if dialog.clickedButton() == replay else "menu"
