from dataclasses import dataclass, field

from app.models.base import GameStatus


@dataclass
class QometModel:
    size: int = 5
    board: list[list[int]] = field(default_factory=list)
    selected: tuple[int, int] | None = None
    status: GameStatus = field(default_factory=GameStatus)

    def __post_init__(self) -> None:
        self.reset()

    def reset(self) -> None:
        self.board = [[0 for _ in range(self.size)] for _ in range(self.size)]
        for col in range(self.size):
            self.board[0][col] = 1
            self.board[self.size - 1][col] = 2
        self.selected = None
        self.status = GameStatus(current_player=1, winner=None, message="")
