# Scene Setup

Creer deux scenes Unity:

1. `MenuScene`
2. `GameplayScene`

## MenuScene

- Canvas avec 3 boutons:
  - Jouer Quixo -> `MenuController.StartQuixo`
  - Jouer Qomet -> `MenuController.StartQomet`
  - Quitter -> `MenuController.Quit`
- Ajouter `MenuController` sur un objet `MenuRoot`.

## GameplayScene

- Objet `GameRoot` avec:
  - `GameFlowController`
  - `VisualPolishController`
- Objet `BoardRoot` vide (transform) assigne dans `BoardViewRenderer`.
- Objet `BoardView` avec `BoardViewRenderer`.
- Objet `HUD` (Canvas) avec `HudView`.
- Prefab `BoardCellPrefab`:
  - `BoardCellView`
  - `MeshRenderer` pour couleur de tuile
  - `TextMeshPro` pour marque `X/O`
  - `Image` pour anneau de selection
  - `EventTrigger` ou `PhysicsRaycaster` pour clic.

## Build Settings

- Ajouter `MenuScene` puis `GameplayScene` dans cet ordre.
