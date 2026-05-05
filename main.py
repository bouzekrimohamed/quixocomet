from app.controllers.game_controller import GameController


def main() -> None:
    controller = GameController()
    controller.run()


if __name__ == "__main__":
    main()
