# Quixo / Qomet

## Auteurs

- Mohamed Bouzekri
- Mehdi Kerrouche
- Abel Feliciagui
- Hoang Linh Doan

## Presentation

Application Unity qui regroupe deux jeux de plateau, Quixo et Qomet. On peut
jouer en local a deux sur le meme ecran, ou en ligne apres avoir cree un compte.
Le projet ajoute aussi une liste d'amis, des invitations de partie et du
matchmaking simple.

La version Unity utilisee est `2022.3.40f1`. Les builds fournis sont des builds
etudiants et ne sont pas signes.

## Telecharger le jeu

La page de telechargement est en ligne ici :

https://bouzekrimohamed.github.io/quixocomet/download/

Elle propose les builds Windows et Linux. La version macOS n'est pas encore
disponible.

## Installation Windows

1. Telecharger `BuildWindows.zip` depuis la page de telechargement.
2. Extraire le ZIP dans un dossier.
3. Ouvrir le dossier extrait.
4. Double-cliquer sur le fichier `.exe`.

Windows peut afficher SmartScreen ou Smart App Control au premier lancement.
C'est normal pour un build etudiant non signe. Sur ce message, choisir
`Informations complementaires` puis `Executer quand meme`. Il n'est pas
necessaire de desactiver l'antivirus ou la securite Windows.

## Installation Linux

1. Telecharger `BuildLinux.zip` depuis la page de telechargement.
2. Extraire le ZIP.
3. Ouvrir un terminal dans le dossier extrait.
4. Lancer :

```bash
chmod +x QuixoQomet.x86_64
./QuixoQomet.x86_64
```

Si le nom du fichier varie un peu :

```bash
chmod +x *.x86_64
./*.x86_64
```

Garder ensemble dans le meme dossier : le fichier `.x86_64`, le dossier `_Data`
et `UnityPlayer.so`. Si l'un de ces fichiers manque, le jeu ne se lance pas.

## macOS

La version macOS n'est pas encore disponible. Sur Mac, un build non signe peut
demander un clic droit > `Ouvrir` plutot qu'un double-clic, mais le build est en
preparation.

## Creation de compte

- Cliquer sur `Inscription` dans l'ecran d'authentification.
- Saisir un email, un username et un mot de passe.
- Valider et ouvrir le mail recu pour confirmer l'adresse.
- Revenir dans le jeu et se connecter avec l'email ou le username.
- En cas d'oubli, utiliser `Mot de passe oublie` depuis l'ecran de connexion.

Tant que l'email n'est pas confirme, la connexion est refusee proprement avec
un message explicite.

## Jouer en local

Depuis le menu, choisir `Jouer`, puis le mode local et Quixo ou Qomet. Le mode
local fonctionne aussi sans compte, en mode invite.

## Jouer en ligne

Il faut etre connecte. Depuis le menu, choisir `Jouer`, puis le mode en ligne
et Quixo ou Qomet. Le matchmaking cherche un autre joueur en attente sur le
meme jeu.

L'autre solution est de passer par `Amis` et d'inviter directement un joueur
deja accepte.

## Amis

Le panneau `Amis` permet de rechercher un username, envoyer une demande,
accepter ou refuser une demande recue, voir les amis acceptes et leur statut
en ligne ou hors ligne. Depuis un ami, il est possible d'envoyer une invitation
de partie en Quixo ou en Qomet.

## Technologies utilisees

- Unity : moteur du jeu, gere les scenes, le rendu et l'interface.
- C# : langage utilise pour tout le code Unity, regles et reseau.
- Supabase Auth : gere les comptes, les sessions et la confirmation email.
- Supabase PostgreSQL : stocke les profils, les amis, les invitations et les coups en ligne.
- Supabase REST : API utilisee depuis Unity pour communiquer avec la base.
- GitHub Pages : heberge la page de telechargement, la page email confirme et la page de reset.

## Limites connues

- Les builds Windows et Linux ne sont pas signes.
- La version macOS n'est pas encore disponible.
- L'online V1 est principalement client-authoritative et utilise du polling REST plutot que Supabase Realtime.
- Pas encore de classement, de timer de tour, ni de reconnexion automatique a une partie en cours.

## Ouvrir le projet Unity

1. Ouvrir le dossier `unity` avec Unity `2022.3.40f1`.
2. Arreter le mode Play si necessaire.
3. Faire `Assets > Refresh`.
4. Faire `Tools > Quixo > Create/Repair Scenes`.
5. Ouvrir `Assets/Scenes/IntroVideoScene`.
6. Lancer Play.

La configuration Supabase est expliquee dans `unity/SUPABASE_SETUP.md`.

## Contact

En cas de probleme urgent avec l'installation ou le lancement :
[lm_bouzekri@gmail.com](mailto:lm_bouzekri@gmail.com)
