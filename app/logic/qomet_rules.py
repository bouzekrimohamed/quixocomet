from app.models.qomet_model import QometModel


def in_bounds(size: int, row: int, col: int) -> bool:
    return 0 <= row < size and 0 <= col < size


def neighbors(row: int, col: int) -> list[tuple[int, int]]:
    return [(row - 1, col), (row + 1, col), (row, col - 1), (row, col + 1)]


def can_select(model: QometModel, row: int, col: int) -> bool:
    return model.board[row][col] == model.status.current_player


def can_move(model: QometModel, src: tuple[int, int], dst: tuple[int, int]) -> bool:
    sr, sc = src
    dr, dc = dst
    if not in_bounds(model.size, dr, dc):
        return False
    if model.board[dr][dc] != 0:
        return False
    return abs(sr - dr) + abs(sc - dc) == 1


def has_legal_move(model: QometModel, player: int) -> bool:
    for row in range(model.size):
        for col in range(model.size):
            if model.board[row][col] != player:
                continue
            for nr, nc in neighbors(row, col):
                if in_bounds(model.size, nr, nc) and model.board[nr][nc] == 0:
                    return True
    return False


def check_winner(model: QometModel, moved_player: int) -> int | None:
    target_row = model.size - 1 if moved_player == 1 else 0
    if any(model.board[target_row][col] == moved_player for col in range(model.size)):
        return moved_player

    other = 2 if moved_player == 1 else 1
    if not has_legal_move(model, other):
        return moved_player
    return None
