# Unity Setup Steps

Ce projet contient la logique C# Unity, mais aucune scene `.unity` n'est versionnee dans `Assets/Scenes`.
Ces etapes creent la V1 jouable localement sans connecter Python a Unity.

## 1. Ouvrir le projet

1. Ouvrir Unity Hub.
2. Ajouter le projet avec le dossier `C:/quixo/unity`.
3. Utiliser Unity `2022.3.40f1` si possible, car `ProjectSettings/ProjectVersion.txt` cible cette version.
4. Si Unity propose une mise a niveau de version, accepter seulement apres avoir sauvegarde/commite le projet.

## 2. Creer les scenes

Creer deux scenes dans `Assets/Scenes` :

1. `MenuScene.unity`
2. `GameplayScene.unity`

Ajouter ensuite ces scenes dans `File > Build Settings... > Scenes In Build`, dans cet ordre :

1. `Assets/Scenes/MenuScene.unity`
2. `Assets/Scenes/GameplayScene.unity`

Si elles ne sont pas dans les Build Settings, les boutons `Jouer` et `Menu` afficheront une erreur de scene introuvable.

## 3. Scene MenuScene

Creer ou verifier les objets suivants :

- `EventSystem`
- `Canvas`
- `MenuRoot`

Sur `MenuRoot`, ajouter le script :

- `MenuController`

Dans le `Canvas`, creer quatre boutons :

- `QuixoButton`
- `QometButton`
- `ThemeButton`
- `QuitButton`

Brancher les boutons dans l'Inspector :

- `QuixoButton > On Click` : glisser `MenuRoot`, choisir `MenuController.StartQuixo`
- `QometButton > On Click` : glisser `MenuRoot`, choisir `MenuController.StartQomet`
- `MenuController.Theme Button` : glisser `ThemeButton` dans le champ
- `QuitButton > On Click` : glisser `MenuRoot`, choisir `MenuController.Quit`

UI conseillee :

- titre : `Quixo / Qomet`
- bouton theme : `Thème : MarineBlue`
- boutons larges, texte lisible, couleurs sobres
- `Canvas Scaler` : `Scale With Screen Size`, resolution `1920 x 1080`

## 4. Scene GameplayScene

Creer ou verifier les objets suivants :

- `Main Camera`
- `Directional Light`
- `EventSystem`
- `GameRoot`
- `BoardView`
- `BoardRoot`
- `HUD`

### Main Camera

Parametres conseilles :

- Position actuelle generee : `X 0`, `Y 7.2`, `Z -7.4`
- Rotation actuelle generee : `X 48`, `Y 0`, `Z 0`
- Orthographic Size : environ `4.9`
- Tag : `MainCamera`
- Ajouter `PhysicsRaycaster`

Le `PhysicsRaycaster` est necessaire pour cliquer sur les cases 3D.

### BoardView

Sur `BoardView`, ajouter :

- `BoardViewRenderer`

Dans l'Inspector :

- `Board Root` : glisser `BoardRoot`
- `Cell Prefab` : optionnel

Si `Cell Prefab` est vide, `BoardViewRenderer` genere des cases simples au lancement.
Pour un prefab personnalise, il doit contenir :

- un `MeshRenderer`
- un `Collider`
- un `BoardCellView`
- un enfant `TextMeshPro` pour afficher `X/O`
- un enfant `SelectionMarker` ou une `Image` de selection

### HUD

Sur `HUD`, ajouter :

- `Canvas`
- `Canvas Scaler`
- `Graphic Raycaster`
- `HudView`

Regler le `Canvas Scaler` :

- `UI Scale Mode` : `Scale With Screen Size`
- `Reference Resolution` : `1920 x 1080`

Creer les elements UI suivants dans `HUD` :

- `TurnLabel` : `TextMeshProUGUI`
- `InfoLabel` : `TextMeshProUGUI`
- `RestartButton` : `Button`
- `MenuButton` : `Button`
- `UpButton` : `Button`
- `DownButton` : `Button`
- `LeftButton` : `Button`
- `RightButton` : `Button`

Dans `HudView`, glisser les references :

- `Turn Label` -> `TurnLabel`
- `Info Label` -> `InfoLabel`
- `Restart Button` -> `RestartButton`
- `Menu Button` -> `MenuButton`
- `Up Button` -> `UpButton`
- `Down Button` -> `DownButton`
- `Left Button` -> `LeftButton`
- `Right Button` -> `RightButton`

Ne pas ajouter manuellement les callbacks des boutons HUD : `HudView.Bind` les branche au lancement.

UI conseillee :

- `TurnLabel` en haut a gauche
- `InfoLabel` sous le joueur courant
- directions en losange a droite ou sous le plateau
- `RestartButton` et `MenuButton` en haut a droite
- boutons de direction desactives par defaut

### GameRoot

Sur `GameRoot`, ajouter :

- `GameFlowController`
- optionnel : `VisualPolishController`

Dans `GameFlowController`, glisser les references :

- `Board View` -> objet `BoardView`
- `Hud View` -> objet `HUD`

Le script essaie de retrouver ces references automatiquement, mais les assigner dans l'Inspector reste plus clair.

Dans `VisualPolishController`, si utilise :

- `Main Camera` -> `Main Camera`
- `Key Light` -> `Directional Light`
- `UI Audio` -> un `AudioSource`, optionnel
- `Click Clip` et `Win Clip` peuvent rester vides

## 5. Tester avec Play

1. Ouvrir `MenuScene`.
2. Cliquer `Play`.
3. Cliquer `Jouer Quixo`.
4. Verifier :
   - le plateau 5x5 apparait
   - le HUD affiche `Joueur 1`
   - seuls les cubes de bord libres ou au joueur actif sont selectionnables
   - les boutons de direction s'activent apres selection
   - `Restart` remet le plateau a zero
   - `Menu` revient a `MenuScene`
5. Refaire avec `Jouer Qomet`.
6. Verifier :
   - Joueur 1 en haut, Joueur 2 en bas
   - clic sur une piece du joueur courant
   - clic sur une case voisine vide
   - changement de tour apres coup valide

## 6. Changer les couleurs / theme

Le theme peut etre change directement dans le jeu depuis `MenuScene` avec le bouton :

`Thème : MarineBlue`

Il cycle dans cet ordre :

1. `MarineBlue`
2. `EmeraldGreen`
3. `RoyalPurple`
4. `ClassicWood`
5. `PremiumDark`
6. `CleanModern`

Le choix est garde avec `PlayerPrefs` et s'applique au menu puis a `GameplayScene`.

La valeur par defaut de la scene generee se change aussi dans :

`Assets/Editor/QuixoSceneBuilder.cs`

Chercher :

```csharp
// ===============================
// CHANGE THEME HERE
// Options:
// ClassicWood, PremiumDark, CleanModern, MarineBlue, EmeraldGreen, RoyalPurple
// ===============================
private const GameplayTheme ActiveGameplayTheme = GameplayTheme.MarineBlue;
```

Remplacer `GameplayTheme.MarineBlue` par une des options :

- `GameplayTheme.ClassicWood`
- `GameplayTheme.PremiumDark`
- `GameplayTheme.CleanModern`
- `GameplayTheme.MarineBlue`
- `GameplayTheme.EmeraldGreen`
- `GameplayTheme.RoyalPurple`

Ensuite relancer `Tools > Quixo > Create/Repair Scenes` pour regenerer `MenuScene` et `GameplayScene` avec le theme choisi.

## 7. Si une reference est Missing

- Ouvrir la scene concernee.
- Selectionner l'objet qui porte le script.
- Dans l'Inspector, chercher les champs rouges ou vides.
- Glisser le bon objet depuis la Hierarchy.
- Sauvegarder la scene.
- Relancer Play.

References critiques :

- `GameFlowController.Board View`
- `GameFlowController.Hud View`
- `BoardViewRenderer.Board Root`
- tous les champs de `HudView`
- `Main Camera` avec `PhysicsRaycaster`
- scenes ajoutees dans `Build Settings`

## 8. Role du Python

Le code Python reste utile comme reference de regles et comme ancienne application PySide6.
Pour une V1 Unity etudiante, l'option la plus stable est de garder la logique native C# dans Unity.
Ne pas connecter Python a Unity pour cette V1 : cela ajouterait de la complexite de lancement, de packaging et de synchronisation sans gain important.
