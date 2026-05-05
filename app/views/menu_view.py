from PySide6.QtWidgets import (
    QComboBox,
    QHBoxLayout,
    QLabel,
    QPushButton,
    QVBoxLayout,
    QWidget,
)


class MenuView(QWidget):
    def __init__(self, on_play, on_quit, on_theme_change, on_mat_change) -> None:
        super().__init__()
        self.on_play = on_play
        self.on_quit = on_quit
        self.on_theme_change = on_theme_change
        self.on_mat_change = on_mat_change
        self._build_ui()

    def _build_ui(self) -> None:
        root = QVBoxLayout()
        root.setSpacing(20)
        root.setContentsMargins(40, 40, 40, 40)

        title = QLabel("QOMET / QUIXO")
        title.setStyleSheet("font-size: 28px; font-weight: bold;")

        game_row = QHBoxLayout()
        game_label = QLabel("Choisir un jeu :")
        self.game_selector = QComboBox()
        self.game_selector.addItems(["QOMET", "QUIXO"])
        self.game_selector.setMinimumWidth(220)
        game_row.addWidget(game_label)
        game_row.addWidget(self.game_selector)

        theme_row = QHBoxLayout()
        theme_label = QLabel("Theme :")
        self.theme_selector = QComboBox()
        self.theme_selector.addItems(["Clair", "Sombre"])
        self.theme_selector.setMinimumWidth(220)
        theme_row.addWidget(theme_label)
        theme_row.addWidget(self.theme_selector)

        mat_row = QHBoxLayout()
        mat_label = QLabel("Tapis :")
        self.mat_selector = QComboBox()
        self.mat_selector.addItems(["Bois", "Ardoise", "Ocean"])
        self.mat_selector.setMinimumWidth(220)
        mat_row.addWidget(mat_label)
        mat_row.addWidget(self.mat_selector)

        self.play_button = QPushButton("Jouer")
        self.quit_button = QPushButton("Quitter")
        self.play_button.setMinimumHeight(42)
        self.quit_button.setMinimumHeight(42)

        root.addWidget(title)
        root.addLayout(game_row)
        root.addLayout(theme_row)
        root.addLayout(mat_row)
        root.addWidget(self.play_button)
        root.addWidget(self.quit_button)
        root.addStretch()

        self.setLayout(root)

        self.play_button.clicked.connect(self._play_clicked)
        self.quit_button.clicked.connect(self.on_quit)
        self.theme_selector.currentTextChanged.connect(self.on_theme_change)
        self.mat_selector.currentTextChanged.connect(self.on_mat_change)

    def _play_clicked(self) -> None:
        self.on_play(self.selected_game())

    def selected_game(self) -> str:
        return self.game_selector.currentText().strip().upper()
