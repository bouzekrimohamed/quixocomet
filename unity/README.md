# Quixo + Qomet Unity (C#)

Refonte du projet Python vers Unity.

## Prerequis

- Unity 2022.3 LTS ou plus recent
- Module Windows Build Support installe

## Structure

- `Assets/Scenes` : scenes du jeu
- `Assets/Scripts/Core` : regles pures C#
- `Assets/Scripts/Gameplay` : orchestration du gameplay
- `Assets/Scripts/UI` : HUD et navigation
- `Assets/Tests/EditMode` : tests unitaires des regles

## Ouvrir le projet

1. Ouvrir Unity Hub
2. Add project -> dossier `c:/quixo/unity`
3. Si `Assets/Scenes/MenuScene.unity` n'existe pas encore, suivre `UNITY_SETUP_STEPS.md`
4. Ouvrir la scene `Assets/Scenes/MenuScene.unity`

## Logique de jeu

La V1 Unity utilise les regles C# natives dans `Assets/Scripts/Core`.
Le code Python a ete conserve comme reference et ancienne application PySide6.

## V1

- Quixo local (2 joueurs)
- Qomet local (2 joueurs)
- Visuel anime via prefabs + tweening code

## Build Windows

Voir `Build/build_windows.ps1`.
