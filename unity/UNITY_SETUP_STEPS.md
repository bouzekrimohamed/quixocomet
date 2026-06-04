# Unity Setup Steps

Ce projet contient le jeu Unity natif Quixo/Qomet, avec intro video, splash fallback, authentification Supabase, profil, amis, menu premium, jeu local et V1 online par polling Supabase.
Le Python reste separe et ne doit pas etre connecte a Unity pour cette version.

## 1. Ouvrir le projet

1. Ouvrir Unity Hub.
2. Ajouter le projet avec le dossier `C:/Users/lm_bo/Documents/PROJET/quixocomet/unity`.
3. Utiliser Unity `2022.3.40f1` si possible.
4. Si Unity propose une mise a niveau, sauvegarder le projet avant d'accepter.

## 2. Generer les scenes

Dans Unity :

1. Stop Play si necessaire.
2. `Assets > Refresh`.
3. `Tools > Quixo > Create/Repair Scenes`.

Le Scene Builder cree/repare ces scenes dans `Assets/Scenes` :

1. `IntroVideoScene.unity`
2. `SplashScene.unity`
3. `AuthScene.unity`
4. `MenuScene.unity`
5. `GameplayScene.unity`

Il les ajoute aussi aux Build Settings dans cet ordre.

## 3. Configurer Supabase

Lire `unity/SUPABASE_SETUP.md`.

Verifier dans :

`Assets/Scripts/Auth/SupabaseSettings.cs`

```csharp
public const string ProjectUrl = "https://wcwufabumabolxhmpexc.supabase.co";
public const string AnonKey = "sb_publishable_PwbgvZXpUn07HsvFRghnPg_R_9T5W3H";
public const string PasswordResetRedirectUrl = "https://bouzekrimohamed.github.io/quixocomet/reset-password/";
```

Important :

- utiliser seulement la cle `anon public` / `publishable`;
- ne jamais mettre de cle `service_role` dans Unity;
- les mots de passe sont geres par Supabase Auth, pas par une table custom.

## 4. Lancer le jeu

1. Ouvrir `Assets/Scenes/IntroVideoScene`.
2. Cliquer `Play`.
3. Verifier la video intro si le clip existe.
4. Si le fichier video est absent, la scene passe automatiquement a `AuthScene` ou `MenuScene`.
5. Tester `Continuer hors ligne` si besoin.
6. Tester `Inscription`, `Connexion`, login par username et `Mot de passe oublie`.
7. Dans `MenuScene`, tester :
   - `Jouer Quixo local`;
   - `Jouer Qomet local`;
   - `Jouer Quixo en ligne` avec un compte connecte;
   - `Jouer Qomet en ligne` avec un compte connecte;
   - `Amis`;
   - `Theme`;
   - `Deconnexion`;
   - `Quitter`.

## 5. Intro video

Le Scene Builder cherche d'abord :

`Assets/Videos/powered_by_mohamed_bouzekri.mp4`

Puis fallback si le fichier garde son double suffixe :

`Assets/Videos/powered_by_mohamed_bouzekri.mp4.mp4`

Skip possible :

- espace;
- entree;
- clic souris.

## 6. Themes visuels

Le theme se change dans le jeu depuis `MenuScene` avec le bouton :

`Theme : MarineBlue`

Ordre du cycle :

1. `MarineBlue`
2. `EmeraldGreen`
3. `RoyalPurple`
4. `ClassicWood`
5. `PremiumDark`
6. `CleanModern`

## 7. Build Windows

1. Stop Play.
2. `Assets > Refresh`.
3. `Tools > Quixo > Create/Repair Scenes`.
4. `File > Build Settings`.
5. Verifier l'ordre :
   - `Assets/Scenes/IntroVideoScene.unity`
   - `Assets/Scenes/SplashScene.unity`
   - `Assets/Scenes/AuthScene.unity`
   - `Assets/Scenes/MenuScene.unity`
   - `Assets/Scenes/GameplayScene.unity`
6. Build `Windows x86_64`.
7. Lancer le `.exe`.

Verification attendue :

- pas de carre magenta;
- video ou fallback OK;
- AuthScene visible;
- mode hors ligne utilisable;
- inscription/connexion fonctionnent apres configuration;
- login par username fonctionne apres migration SQL email;
- reset password ouvre le flux email Supabase;
- menu et amis ne crashent pas;
- Quixo/Qomet local restent jouables.
- avec deux comptes, presence/invitations/matchmaking online fonctionnent apres migration SQL.

## 8. Online V1

La V1 online utilise REST/polling Supabase :

- presence toutes les 10 secondes;
- invitations entre amis depuis le panneau `Amis`;
- matchmaking public via `Jouer Quixo en ligne` / `Jouer Qomet en ligne`;
- polling des coups toutes les 1 seconde pendant la partie;
- bouton `Recommencer` desactive en online, bouton `Menu` pour quitter.

Pour tester online, utiliser deux comptes Supabase differents avec deux instances : Unity + build Windows, ou deux PC.

## 9. Future V2 online

Ajouter ensuite Supabase Realtime, validation serveur par RPC/Edge Function, reconnexion, abandon, timer et classement.
# Scenes et builds

Apres modification des scripts ou du Scene Builder :

1. Stop Play.
2. `Assets > Refresh`.
3. `Tools > Quixo > Create/Repair Scenes`.
4. Ouvrir `IntroVideoScene`, puis lancer Play.

Pour distribuer le jeu, creer separement les builds Windows x86_64, Linux x86_64
et macOS. Les dossiers `unity/BuildWindows`, `unity/BuildLinux` et
`unity/BuildMac` sont ignores par Git.
