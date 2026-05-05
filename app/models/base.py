from dataclasses import dataclass
from enum import Enum


class GameType(str, Enum):
    QOMET = "QOMET"
    QUIXO = "QUIXO"


@dataclass
class GameStatus:
    current_player: int = 1
    winner: int | None = None
    message: str = ""
