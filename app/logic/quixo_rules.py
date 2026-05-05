from app.models.quixo_model import QuixoModel


UP = "UP"
DOWN = "DOWN"
LEFT = "LEFT"
RIGHT = "RIGHT"


def is_border(size: int, row: int, col: int) -> bool:
    return row == 0 or col == 0 or row == size - 1 or col == size - 1


def can_select(model: QuixoModel, row: int, col: int) -> bool:
    value = model.board[row][col]
    return is_border(model.size, row, col) and (value == 0 or value == model.status.current_player)


def allowed_directions(model: QuixoModel, row: int, col: int) -> list[str]:
    if not can_select(model, row, col):
        return []

    size = model.size
    directions: list[str] = []
    if row < size - 1:
        directions.append(UP)
    if row > 0:
        directions.append(DOWN)
    if col < size - 1:
        directions.append(LEFT)
    if col > 0:
        directions.append(RIGHT)
    return directions


def apply_move(model: QuixoModel, row: int, col: int, direction: str) -> bool:
    if direction not in allowed_directions(model, row, col):
        return False

    p = model.status.current_player
    size = model.size
    board = model.board

    if direction == DOWN:
        for r in range(row, 0, -1):
            board[r][col] = board[r - 1][col]
        board[0][col] = p
        return True
    if direction == UP:
        for r in range(row, size - 1):
            board[r][col] = board[r + 1][col]
        board[size - 1][col] = p
        return True
    if direction == RIGHT:
        for c in range(col, 0, -1):
            board[row][c] = board[row][c - 1]
        board[row][0] = p
        return True
    if direction == LEFT:
        for c in range(col, size - 1):
            board[row][c] = board[row][c + 1]
        board[row][size - 1] = p
        return True

    return False


def check_line(values: list[int], player: int) -> bool:
    return all(v == player for v in values)


def check_winner(model: QuixoModel, player: int) -> int | None:
    opponent = 2 if player == 1 else 1
    size = model.size
    board = model.board

    player_has_line = False
    opponent_has_line = False

    for row in range(size):
        if check_line(board[row], player):
            player_has_line = True
        if check_line(board[row], opponent):
            opponent_has_line = True
    for col in range(size):
        column = [board[r][col] for r in range(size)]
        if check_line(column, player):
            player_has_line = True
        if check_line(column, opponent):
            opponent_has_line = True

    diag_lr = [board[i][i] for i in range(size)]
    diag_rl = [board[i][size - 1 - i] for i in range(size)]
    if check_line(diag_lr, player) or check_line(diag_rl, player):
        player_has_line = True
    if check_line(diag_lr, opponent) or check_line(diag_rl, opponent):
        opponent_has_line = True

    # Regle officielle Quixo: creer une ligne adverse fait perdre le joueur actif.
    if opponent_has_line:
        return opponent
    if player_has_line:
        return player
    return None
