# Quixo / Qomet

## Auteurs

- BOUZEKRI Mohamed
- MEHDI Kirouche
- ABEL Filiciaggi
- LIGH Douang

## Presentation

Ce projet est une application Unity qui regroupe deux jeux abstraits, Quixo et
Qomet. Nous avons ajoute un mode local, un mode en ligne, des comptes Supabase,
une liste d'amis et du matchmaking.

Le projet Unity utilise la version `2022.3.40f1`. Les builds proposes sont des
builds etudiants et ne sont pas signes.

## Telecharger le jeu

La page de telechargement se trouve ici :

https://bouzekrimohamed.github.io/quixocomet/download/

## Installation Windows

1. Telecharger `BuildWindows.zip`.
2. Extraire le dossier.
3. Lancer le fichier `.exe`.
4. Si Windows affiche un avertissement, c'est parce que le build etudiant n'est pas signe.

## Installation Linux

1. Telecharger `BuildLinux.zip`.
2. Extraire le dossier.
3. Ouvrir un terminal dans le dossier.
4. Lancer :

```bash
chmod +x QuixoQomet.x86_64
./QuixoQomet.x86_64
```

Si le nom du fichier varie :

```bash
chmod +x *.x86_64
./*.x86_64
```

## macOS

La version macOS n'est pas encore disponible.

## Configuration du compte

- Creer un compte avec un email, un username et un mot de passe.
- Confirmer l'email recu avant la premiere connexion.
- Se connecter avec l'email ou le username.
- Utiliser `Mot de passe oublie` si necessaire.

## Jouer en local

Depuis le menu, choisir `Jouer`, puis le mode local et Quixo ou Qomet. Le mode
local reste disponible sans compte.

## Jouer en ligne

Il faut d'abord se connecter. Depuis le menu, choisir le mode en ligne puis
Quixo ou Qomet. Il est aussi possible d'inviter directement un joueur depuis
le panneau `Amis`.

## Amis

Le panneau `Amis` permet de rechercher un username, envoyer une demande,
accepter ou refuser une demande et inviter un ami a jouer.

## Technologies

- Unity
- C#
- Supabase Auth
- Supabase PostgreSQL
- Supabase REST
- GitHub Pages

## Limites connues

- Le build Windows n'est pas signe.
- La version macOS n'est pas encore disponible.
- Le online V1 est principalement client-authoritative.
- La synchronisation online utilise du polling et pas encore Supabase Realtime.

## Ouvrir le projet Unity

1. Ouvrir le dossier `unity` avec Unity `2022.3.40f1`.
2. Stop Play si necessaire.
3. Faire `Assets > Refresh`.
4. Faire `Tools > Quixo > Create/Repair Scenes`.
5. Ouvrir `Assets/Scenes/IntroVideoScene`.
6. Lancer Play.

La configuration Supabase est expliquee dans `unity/SUPABASE_SETUP.md`.

## Contact

En cas de probleme urgent avec l'installation ou le lancement :
[lm_bouzekri@gmail.com](mailto:lm_bouzekri@gmail.com)
