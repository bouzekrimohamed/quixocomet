from dataclasses import dataclass

from PySide6.QtWidgets import QApplication


@dataclass(frozen=True)
class MatStyle:
    board_background: str
    empty_cell: str
    border: str
    select_border: str
    player1: str
    player2: str
    text_color: str


LIGHT_QSS = """
QMainWindow, QWidget {
    background: #f7f8fb;
    color: #1f2937;
    font-size: 14px;
}
QPushButton {
    background: #ffffff;
    border: 1px solid #c6cbd4;
    border-radius: 8px;
    padding: 8px 12px;
}
QPushButton:hover { background: #eef2ff; }
QPushButton:disabled { color: #888; background: #efefef; }
QComboBox {
    background: #ffffff;
    border: 1px solid #c6cbd4;
    border-radius: 8px;
    padding: 6px 10px;
}
"""


DARK_QSS = """
QMainWindow, QWidget {
    background: #12151d;
    color: #e5e7eb;
    font-size: 14px;
}
QPushButton {
    background: #1f2937;
    border: 1px solid #374151;
    border-radius: 8px;
    color: #e5e7eb;
    padding: 8px 12px;
}
QPushButton:hover { background: #273449; }
QPushButton:disabled { color: #808891; background: #2a2f3a; }
QComboBox {
    background: #1f2937;
    border: 1px solid #374151;
    border-radius: 8px;
    color: #e5e7eb;
    padding: 6px 10px;
}
"""


MATS: dict[str, MatStyle] = {
    "Bois": MatStyle(
        board_background="qlineargradient(x1:0,y1:0,x2:1,y2:1, stop:0 #8c6239, stop:1 #6b4423)",
        empty_cell="#f1d7b8",
        border="#5a3a1d",
        select_border="#2ecc71",
        player1="#2f80ed",
        player2="#eb5757",
        text_color="#111111",
    ),
    "Ardoise": MatStyle(
        board_background="qlineargradient(x1:0,y1:0,x2:1,y2:1, stop:0 #2f3642, stop:1 #1e222b)",
        empty_cell="#99a3b0",
        border="#131722",
        select_border="#f5c542",
        player1="#64b5f6",
        player2="#ff7675",
        text_color="#0f1115",
    ),
    "Ocean": MatStyle(
        board_background="qlineargradient(x1:0,y1:0,x2:1,y2:1, stop:0 #146c94, stop:1 #125b50)",
        empty_cell="#b9e3f5",
        border="#0f3d52",
        select_border="#ffd166",
        player1="#3a86ff",
        player2="#ef476f",
        text_color="#0c2230",
    ),
}


def apply_theme(app: QApplication, theme_name: str) -> None:
    app.setStyleSheet(DARK_QSS if theme_name == "Sombre" else LIGHT_QSS)


def get_mat_style(mat_name: str) -> MatStyle:
    return MATS.get(mat_name, MATS["Bois"])
