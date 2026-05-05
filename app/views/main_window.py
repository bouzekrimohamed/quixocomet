from PySide6.QtWidgets import QMainWindow, QStackedWidget

from app.views.game_view import GameView
from app.views.menu_view import MenuView


class MainWindow(QMainWindow):
    def __init__(
        self,
        on_play,
        on_quit,
        on_cell_click,
        on_direction_click,
        on_reset,
        on_back,
        on_theme_change,
        on_mat_change,
    ) -> None:
        super().__init__()
        self.setWindowTitle("Projet Tuteure - QOMET & QUIXO")
        self.setMinimumSize(760, 760)

        self.stack = QStackedWidget()
        self.setCentralWidget(self.stack)

        self.menu_view = MenuView(
            on_play=on_play,
            on_quit=on_quit,
            on_theme_change=on_theme_change,
            on_mat_change=on_mat_change,
        )
        self.game_view = GameView(
            on_cell_click=on_cell_click,
            on_direction_click=on_direction_click,
            on_reset=on_reset,
            on_back=on_back,
        )

        self.stack.addWidget(self.menu_view)
        self.stack.addWidget(self.game_view)

    def show_menu(self) -> None:
        self.stack.setCurrentWidget(self.menu_view)

    def show_game(self) -> None:
        self.stack.setCurrentWidget(self.game_view)
