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
- Valider le compte.
- Se connecter avec l'email ou le username.
- En cas d'oubli, utiliser `Mot de passe oublie` depuis l'ecran de connexion.

Le projet utilise Supabase Auth. Unity ne configure jamais de SMTP : pour eviter
les limites email du provider Supabase par defaut, un SMTP custom (ex. Brevo)
peut etre configure uniquement dans le tableau de bord Supabase. La confirmation
email peut alors etre activee ; les emails peuvent mettre quelques minutes a
arriver (verifier aussi Spam/Promotions).

Apres confirmation, Supabase doit rediriger vers
`https://bouzekrimohamed.github.io/quixocomet/email-confirmed/` (pas la racine
`/#access_token`). Verifier les Redirect URLs Supabase et reinscrire un compte
test si un ancien email avait ete genere sans `redirect_to`.

Pour la demo, la confirmation email peut rester desactivee dans Supabase afin
d'eviter les blocages pendant les tests sans SMTP custom.

## Jouer en local

Depuis le menu, choisir `Jouer`, puis le mode local et Quixo ou Qomet. Le mode
local fonctionne aussi sans compte, en mode invite.

## Jouer en ligne

Il faut etre connecte. Depuis le menu, choisir `Jouer`, puis le mode en ligne
et Quixo ou Qomet. Le matchmaking cherche un autre joueur en attente sur le
meme jeu.

L'autre solution est de passer par `Amis` et d'inviter directement un joueur
deja accepte.

## Quixo equipe 2v2

Le mode 2v2 est disponible pour Quixo en ligne via lobby. Un joueur cree un
lobby, partage le code, puis les trois autres joueurs rejoignent l'equipe 1
ou l'equipe 2. La partie ne demarre que quand il y a exactement deux joueurs
par equipe.

Les deux joueurs d'une meme equipe jouent la meme marque : equipe 1 = X,
equipe 2 = O. L'ordre des tours est fixe : equipe 1 joueur 1, equipe 2 joueur
1, equipe 1 joueur 2, equipe 2 joueur 2, puis on recommence. Le matchmaking
aleatoire 2v2 n'est pas encore ajoute dans cette V1.

## Amis

Le panneau `Amis` permet de rechercher un username, envoyer une demande,
accepter ou refuser une demande recue, voir les amis acceptes et leur statut
en ligne ou hors ligne. Depuis un ami, il est possible d'envoyer une invitation
de partie en Quixo ou en Qomet.

## Technologies utilisees

- Unity : moteur du jeu, gere les scenes, le rendu et l'interface.
- C# : langage utilise pour tout le code Unity, regles et reseau.
- Supabase Auth : gere les comptes, les sessions et le reset password.
- Supabase PostgreSQL : stocke les profils, les amis, les invitations et les coups en ligne.
- Supabase REST : API utilisee depuis Unity pour communiquer avec la base.
- GitHub Pages : heberge la page de telechargement, la page email confirme et la page de reset.

## Limites connues

- Les builds Windows et Linux ne sont pas signes.
- La version macOS n'est pas encore disponible.
- L'online V1 est principalement client-authoritative et utilise du polling REST plutot que Supabase Realtime.
- Pas encore de classement, de matchmaking aleatoire 2v2, ni de reconnexion automatique a une partie en cours.

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
