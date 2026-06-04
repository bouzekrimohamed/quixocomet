# Rapport du projet Quixo / Qomet

**Auteurs :** Mohamed Bouzekri, Mehdi Kerrouche, Abel Feliciagui, Hoang Linh Doan.

## Mode d'emploi et installation

Cette section explique comment recuperer le jeu, l'installer et le lancer. Elle
sert aussi de petit guide d'utilisation pour quelqu'un qui veut tester
rapidement le projet.

### Telecharger le jeu

La page de telechargement officielle se trouve ici :

https://bouzekrimohamed.github.io/quixocomet/download/

Elle propose les builds Windows et Linux dans la release GitHub `v1.0.0`. La
version macOS est indiquee comme "bientot disponible".

### Installation Windows

1. Telecharger `BuildWindows.zip`.
2. Extraire le ZIP dans un dossier.
3. Ouvrir le dossier et double-cliquer sur le fichier `.exe`.

Windows peut afficher un avertissement SmartScreen ou Smart App Control. C'est
le comportement normal d'un build etudiant non signe : il faut choisir
`Informations complementaires` puis `Executer quand meme`. Aucune
desactivation de l'antivirus ou de la securite Windows n'est demandee.

### Installation Linux

1. Telecharger `BuildLinux.zip`.
2. Extraire le ZIP.
3. Ouvrir un terminal dans le dossier extrait.
4. Lancer :

```bash
chmod +x QuixoQomet.x86_64
./QuixoQomet.x86_64
```

Si le nom de l'executable varie :

```bash
chmod +x *.x86_64
./*.x86_64
```

Il faut garder le fichier `.x86_64`, le dossier `_Data` et `UnityPlayer.so`
dans le meme dossier. Si l'un de ces elements manque, le jeu ne demarre pas.

### macOS

La version macOS est en preparation. Sur macOS, un build non signe peut demander
un clic droit > `Ouvrir` plutot qu'un double-clic au premier lancement.

### Creation de compte et confirmation email

Pour creer un compte :

1. Ouvrir l'onglet `Inscription` dans l'ecran d'authentification.
2. Saisir un email, un username et un mot de passe.
3. Valider l'inscription.
4. Ouvrir le mail de confirmation envoye par Supabase.
5. Cliquer sur le lien. Supabase redirige vers la page
   https://bouzekrimohamed.github.io/quixocomet/email-confirmed/.

Tant que l'email n'est pas confirme, la connexion echoue avec un message clair
dans l'ecran d'authentification. Une fois l'email confirme, la connexion est
possible avec l'email ou avec le username.

### Mot de passe oublie

Depuis l'ecran de connexion, saisir l'adresse email puis cliquer sur
`Mot de passe oublie`. Supabase envoie un mail avec un lien qui ouvre la page
https://bouzekrimohamed.github.io/quixocomet/reset-password/. La page permet
de choisir un nouveau mot de passe puis de revenir se connecter dans le jeu.

### Jouer en local

Depuis le menu principal, choisir `Jouer`, puis le mode local et Quixo ou
Qomet. Le mode local fonctionne aussi en mode invite, sans compte connecte.

### Jouer en ligne

Il faut etre connecte. Depuis le menu principal, choisir `Jouer`, puis le mode
en ligne et Quixo ou Qomet. Le matchmaking cherche un autre joueur en attente
sur le meme jeu.

Il est aussi possible d'inviter directement un ami depuis le panneau `Amis`.

### Amis

Dans le panneau `Amis`, saisir le username recherche puis envoyer une demande.
Le destinataire peut accepter ou refuser. Une fois la relation acceptee, les
deux comptes voient le statut en ligne ou hors ligne, et peuvent s'envoyer une
invitation de partie. Une invitation acceptee ne lance jamais la partie toute
seule : le joueur clique sur `Rejoindre` quand il veut entrer.

### Contact

En cas de probleme urgent avec l'installation ou le lancement :
[lm_bouzekri@gmail.com](mailto:lm_bouzekri@gmail.com).

## 1. Introduction

Ce projet est une application de jeu realisee avec Unity. Elle regroupe deux
jeux de plateau : Quixo et Qomet. L'objectif etait d'obtenir une version
jouable et presentable, avec un parcours utilisateur complet, et une premiere
couche de jeu en ligne.

Le projet permet de jouer en local a Quixo et a Qomet. Il ajoute une partie
connectee avec Supabase : creation de compte, connexion, profils, amis,
presence en ligne, invitations et matchmaking. Unity communique directement
avec Supabase en REST, sans serveur dedie.

Le resultat est une base de jeu coherente : les deux jeux ont chacun leurs
regles, l'utilisateur peut se connecter ou jouer hors ligne, et deux comptes
peuvent jouer une partie en ligne ensemble.

## 2. Objectifs du projet

Le premier objectif etait de proposer un vrai parcours utilisateur dans Unity,
pas seulement les regles des jeux. Le projet contient donc une scene d'intro,
une scene d'authentification, un menu, une scene de jeu, un HUD et plusieurs
boutons adaptes aux modes local et en ligne.

Le deuxieme objectif etait d'integrer Quixo et Qomet dans une meme base Unity.
Les deux jeux utilisent un plateau et deux joueurs, mais leurs regles sont
differentes. Il fallait donc partager le rendu et le deroulement de partie,
tout en separant clairement la logique de chaque jeu.

Le troisieme objectif etait d'ajouter une couche compte et social. Un joueur
peut creer un compte, se connecter avec son email ou son username, ajouter des
amis et accepter ou refuser une demande recue.

Le quatrieme objectif etait de permettre le jeu en ligne. Deux chemins ont ete
prevus : l'invitation entre amis et le matchmaking aleatoire. Dans les deux
cas, le match et les coups joues sont stockes dans Supabase, puis relus par
polling depuis Unity.

Enfin, Qomet devait rester un vrai jeu different de Quixo. Qomet n'est pas une
simple variante visuelle de Quixo : il repose sur un graphe de 25 noeuds, des
connexions, des reserves d'etoiles, des deplacements, des poussees et une
condition de victoire par carre.

## 3. Technologies utilisees

Unity est utilise comme moteur du jeu. Il gere les scenes, l'interface, les
interactions avec le plateau, les boutons, l'affichage des pions et les builds
Windows et Linux.

C# est utilise pour tout le code cote Unity : les regles de Quixo et de Qomet,
les controleurs de menu, d'authentification et de partie, et les services qui
parlent a Supabase.

Supabase Auth sert a gerer l'inscription, la connexion, les sessions et la
reinitialisation du mot de passe. Les mots de passe ne sont pas stockes dans
une table du projet : ils restent geres par Supabase Auth.

Supabase PostgreSQL sert a stocker les donnees liees au jeu : profils,
relations d'amitie, presence, invitations, file de matchmaking, matchs et
coups joues.

Supabase REST permet a Unity de communiquer avec la base sans serveur dedie.
Les scripts utilisent `UnityWebRequest` pour envoyer des requetes HTTP vers
l'API Supabase.

GitHub Pages heberge trois pages statiques : la page de telechargement, la
page de confirmation email et la page de reinitialisation du mot de passe. La
page de reset recoit les tokens envoyes par Supabase et permet a l'utilisateur
de choisir un nouveau mot de passe.

Git et GitHub servent au versionnement et au partage du projet. Le depot
contient aussi les fichiers de configuration et les guides pour ouvrir Unity,
configurer Supabase et faire les builds.

## 4. Organisation generale du projet

Le code Unity est range dans des dossiers simples.

Le dossier `Auth` contient la connexion, l'inscription, la session utilisateur,
le stockage des tokens et les requetes authentifiees vers Supabase. On y trouve
notamment `AuthService`, `SessionManager` et `SupabaseRequestHelper`.

Le dossier `Social` contient la gestion des amis. Il permet de chercher un
profil par username, d'envoyer une demande, de l'accepter ou de la refuser,
puis de charger la liste des amis acceptes.

Le dossier `Online` contient la presence, les invitations de match, le
matchmaking et la synchronisation des coups. Cette partie repose sur des
tables Supabase et sur du polling depuis Unity.

Le dossier `Gameplay` contient le deroulement d'une partie. `GameFlowController`
choisit les regles selon le jeu, gere les clics sur le plateau, applique les
coups, met a jour le HUD et bloque les actions quand le joueur n'a pas la main
en ligne.

Le dossier `Core` contient les regles et les structures de base : etat du
plateau, joueur courant, directions de mouvement, logique Quixo, graphe Qomet
et regles Qomet.

Le dossier `UI` contient les vues : authentification, menu, amis, HUD, rendu
du plateau, themes visuels, splash et intro video.

Enfin, `Editor / QuixoSceneBuilder` sert a creer ou reparer les scenes Unity.
Il prepare notamment `IntroVideoScene`, `SplashScene`, `AuthScene`,
`MenuScene` et `GameplayScene`, puis les ajoute aux Build Settings.

Cette organisation reste volontairement simple. Elle separe les
responsabilites principales sans chercher a imiter une grosse architecture
serveur.

## 5. Authentification et gestion des comptes

L'authentification utilise Supabase Auth. L'utilisateur peut s'inscrire avec
un email, un mot de passe et un username. Apres l'inscription, le projet cree
ou met a jour un profil dans la table `profiles`, avec l'identifiant Supabase
de l'utilisateur, son username, son display name et son email.

La connexion accepte deux formats. Si l'identifiant contient un `@`, il est
traite comme un email et envoye directement a Supabase Auth. Sinon, Unity
cherche d'abord dans `profiles` l'email correspondant au username, puis utilise
cet email pour la connexion Supabase.

Le mot de passe oublie passe par l'endpoint Supabase `recover`. Unity envoie
l'email et une URL de redirection vers la page GitHub Pages du projet. Cette
page statique utilise Supabase JS pour appliquer le nouveau mot de passe avec
les tokens fournis dans le lien.

La confirmation email est obligatoire. La requete d'inscription indique la
page `email-confirmed` comme redirection et Unity refuse la connexion tant
que Supabase ne renvoie pas un email confirme.

Les sessions sont stockees cote Unity avec `PlayerPrefs`. Le projet conserve
l'access token, le refresh token, l'id utilisateur, l'email et le username.
Quand une requete authentifiee echoue a cause d'un JWT expire ou invalide,
`SupabaseRequestHelper` tente de rafraichir la session avec le refresh token,
puis relance la requete.

Le projet n'utilise jamais la cle `service_role` dans Unity. Unity utilise
uniquement l'URL du projet Supabase et la cle anon / publishable. Les droits
d'acces sont geres par les policies RLS cote Supabase.

Un mode hors ligne existe aussi. Si Supabase n'est pas configure, ou si
l'utilisateur ne veut pas se connecter, il peut continuer comme invite. Dans
ce cas, les fonctions sociales et en ligne ne sont pas disponibles, mais les
modes locaux restent utilisables.

## 6. Systeme d'amis

Le systeme d'amis utilise la table `friends`. Pour ajouter un joueur,
l'utilisateur saisit un username. Unity normalise ce username en minuscules,
avec uniquement des lettres, des chiffres et des underscores, puis cherche le
profil correspondant dans `profiles`.

Avant d'envoyer une demande, le service verifie qu'il ne s'agit pas de
l'utilisateur courant et qu'une relation n'existe pas deja entre les deux
joueurs. Si tout est correct, une ligne est creee dans `friends` avec le
statut `pending`.

Le joueur qui recoit la demande peut l'accepter ou la refuser. L'acceptation
met le statut a `accepted`, le refus le met a `rejected`. La liste des amis
affichee dans l'interface ne reprend que les relations acceptees.

Le panneau d'amis affiche aussi les demandes recues, les amis acceptes et les
invitations de partie. Depuis la ligne d'un ami, il est possible d'envoyer une
invitation pour Quixo ou pour Qomet.

Les erreurs Supabase sont converties en messages plus lisibles cote UI. Une
cle dupliquee devient par exemple un message du type "Vous etes deja amis ou
une demande existe deja", et une erreur RLS est affichee comme un probleme
d'autorisation ou de policies.

## 7. Presence en ligne et hors ligne

La presence en ligne est geree avec la table `user_presence`. Quand un
utilisateur connecte est actif, Unity envoie un heartbeat regulier qui met a
jour le statut, le username et surtout `last_seen_at`.

L'affichage des amis utilise ensuite cette information pour determiner si un
ami est en ligne. Un utilisateur est considere online si son statut est
`online` et si son `last_seen_at` est assez recent. L'interface peut alors
afficher un statut visible.

L'approche est simple, adaptee a une V1. Limite normale : si l'application se
ferme brutalement, Unity ne peut pas forcement envoyer un statut `offline`.
Le joueur peut donc rester affiche en ligne quelques secondes, jusqu'a ce que
son dernier heartbeat soit considere trop ancien.

## 8. Multijoueur en ligne

Le multijoueur en ligne propose deux facons de demarrer une partie.

La premiere est l'invitation entre amis. Un joueur envoie une invitation a un
ami pour Quixo ou Qomet. L'invitation est stockee dans `match_invites` avec le
statut `pending`. Quand le destinataire accepte, Unity cree un match dans
`online_matches`, met l'invitation a `accepted` et associe le `match_id`.

La deuxieme est le matchmaking aleatoire. Quand un joueur cherche une partie,
Unity cree ou remet a zero sa ligne dans `matchmaking_queue`. Le client cherche
ensuite un autre joueur en attente pour le meme jeu. Si un adversaire est
trouve, un match est cree et les deux lignes de queue passent en `matched`.

Les matchs sont stockes dans `online_matches`. La table contient le type de
jeu, `player1_id`, `player2_id`, `current_turn_id`, le statut du match et
eventuellement le gagnant. Le joueur `player1` commence toujours, car
`current_turn_id` est initialise avec `player1_id`.

Les coups joues sont stockes dans `online_moves`. Chaque coup contient un
numero, le joueur qui l'a envoye et un payload JSON. Pour Quixo, le payload
contient la case selectionnee et la direction. Pour Qomet, il distingue la
pose d'une etoile et le deplacement d'une etoile entre deux noeuds.

La synchronisation se fait par polling. Pendant une partie en ligne, Unity
recupere regulierement les nouveaux coups et l'etat du match. Le client local
applique les coups adverses lorsqu'ils apparaissent dans `online_moves`.

Le mode en ligne bloque les actions si ce n'est pas le tour du joueur. Le HUD
indique si le joueur doit jouer ou attendre l'adversaire. Apres un coup
valide, Unity envoie le coup, met a jour le tour suivant et termine le match
si un gagnant est detecte.

Le matchmaking contient quelques protections utiles. Les lignes de queue trop
anciennes sont ignorees pour eviter de matcher un joueur avec une ancienne
session fermee. Le projet annule aussi les anciennes queues du joueur avant
une nouvelle recherche, et un tie-break deterministe evite que deux joueurs
creent chacun un match separe.

### Limite de la V1 online

La version actuelle est principalement client-authoritative. Unity valide les
coups cote client, puis envoie le resultat a Supabase.

Supabase controle surtout l'identite, les droits d'acces RLS, le tour courant
et l'etat du match. Cette base est suffisante pour une premiere version, mais
elle n'empeche pas completement un client modifie d'envoyer un coup incorrect.

Une V2 plus solide pourrait utiliser des RPC SQL ou des Edge Functions pour
valider les coups cote serveur. Elle pourrait aussi remplacer une partie du
polling par Supabase Realtime.

## 9. Mode Quixo

Quixo se joue sur un plateau 5x5. Le joueur selectionne un cube du bord qui
est neutre ou qui lui appartient, puis choisit une direction d'insertion. Le
deplacement pousse la ligne ou la colonne correspondante et place le symbole
du joueur a l'autre extremite.

Le projet affiche les marques des deux joueurs avec X et O. La victoire est
detectee quand un joueur obtient une ligne, une colonne ou une diagonale
complete. Comme dans les regles classiques, si le coup cree aussi une ligne
gagnante pour l'adversaire, la verification de l'adversaire est prioritaire.

Techniquement, Quixo repose sur une logique de grille. Les cases sont indexees
par ligne et colonne, et les directions possibles sont calculees a partir de
la position du cube selectionne.

Quixo est jouable en local et en ligne. En ligne, les informations
necessaires au coup sont envoyees dans le payload, puis rejouees sur le client
adverse.

## 10. Mode Qomet

Qomet est une partie importante du projet, car ce n'est pas une variante
visuelle de Quixo. Le plateau ne fonctionne pas comme une grille classique de
25 cases : il est represente par un graphe de 25 noeuds disposes dans une
grille logique 7x7, avec seulement certaines positions valides.

La disposition visuelle correspond a une forme 3-3-3-7-3-3-3 : trois noeuds en
haut, trois sur les lignes suivantes, sept au centre, puis trois noeuds par
ligne vers le bas. Les noeuds sont identifies de A a Y et relies par des
connexions definies dans `QometGraph`.

Chaque joueur possede une reserve de 7 etoiles. Le plateau est vide au depart.
Tant qu'un joueur a des etoiles en reserve, il peut poser une etoile sur un
noeud vide. Une fois la reserve vide, ou selon la situation de jeu, il peut
selectionner une etoile de sa couleur et la deplacer vers un noeud relie.

Le deplacement se fait uniquement le long des connexions du graphe. Si le
noeud d'arrivee est vide, l'etoile s'y deplace simplement. Si le noeud
d'arrivee contient une etoile, une poussee peut etre tentee dans la meme
direction.

La poussee ne deplace qu'une seule etoile. Si le noeud suivant dans la
direction existe et est vide, l'etoile poussee y est deplacee. Si aucun noeud
suivant n'existe, l'etoile sort du plateau et retourne dans la reserve de son
proprietaire. Si une deuxieme etoile bloque la ligne, la poussee est interdite.

Le code interdit aussi le coup inverse immediat. Cela evite qu'un joueur
annule directement le deplacement precedent en revenant exactement de `to`
vers `from`.

La victoire se fait par formation d'un carre. Le projet utilise une liste de
carres gagnants pre-calcules, avec des quadruplets de noeuds. Cette solution
evite de detecter de faux carres a cause des positions visuelles ou des
calculs flottants.

Apres un coup, Qomet verifie d'abord si l'adversaire possede un carre. Si
c'est le cas, l'adversaire gagne. Sinon, le jeu verifie si le joueur qui vient
de jouer a cree son propre carre. Cette priorite est importante, car une
poussee peut produire une situation favorable a l'autre joueur.

La difference technique avec Quixo est donc nette. Quixo utilise une grille
5x5 et des directions de poussee sur lignes ou colonnes. Qomet utilise un
graphe de noeuds et de connexions, avec une reserve, des mouvements le long
des aretes et une victoire par carres predefinis.

## 11. Interface utilisateur

L'interface est organisee autour de plusieurs scenes.

`AuthScene` sert a la connexion, l'inscription, le mot de passe oublie et le
mode invite. Elle permet de continuer hors ligne si Supabase n'est pas
configure ou si l'utilisateur ne veut pas se connecter.

`MenuScene` donne acces aux modes locaux, aux modes en ligne, au panneau
d'amis, aux themes, a la deconnexion et a la sortie du jeu. Les boutons online
et amis dependent de l'etat de session de l'utilisateur.

`FriendsView` affiche les demandes d'amis, les amis acceptes, leur presence et
les invitations de partie. C'est aussi depuis cette vue qu'un joueur peut
inviter un ami en Quixo ou en Qomet. Une invitation acceptee n'entraine pas
le lancement automatique d'une partie : le joueur choisit quand rejoindre.

`GameplayScene` contient le plateau, le HUD, les messages de tour, la popup de
fin de partie et les controles de jeu. Pour Quixo, le HUD affiche les
directions possibles. Pour Qomet, l'interaction se fait davantage par
selection de noeuds relies.

Le projet contient aussi une intro video et un splash de secours. Si la video
est disponible, elle est jouee au demarrage. Sinon, le projet passe au flux
suivant avec un affichage fallback.

## 12. Base de donnees Supabase

La table `profiles` contient les informations publiques du joueur : id,
username, display name, email et date de creation. Elle sert au login par
username et a l'affichage des amis.

La table `friends` contient les relations entre deux profils. Elle stocke le
demandeur, le receveur et le statut : `pending`, `accepted`, `rejected` ou
`blocked`.

La table `user_presence` contient la presence en ligne. Elle stocke
l'utilisateur, son username, son statut et `last_seen_at`, utilise pour savoir
si le joueur est encore considere connecte.

La table `match_invites` contient les invitations entre amis. Elle indique
l'expediteur, le destinataire, le jeu choisi, le statut de l'invitation et
eventuellement le match cree apres acceptation.

La table `matchmaking_queue` sert a trouver un adversaire aleatoire. Une ligne
indique qu'un joueur cherche une partie pour Quixo ou Qomet. Le code utilise
`created_at` et `updated_at` pour departager les joueurs et ignorer les
anciennes lignes.

La table `online_matches` represente une partie en ligne. Elle contient les
deux joueurs, le joueur qui doit jouer, le statut du match et le gagnant si
la partie est terminee.

La table `online_moves` contient les coups joues. Chaque coup est lie a un
match, a un joueur, a un numero de coup et a un payload JSON qui permet de
rejouer l'action cote adverse.

## 13. Securite et limites

Le projet n'utilise pas de cle `service_role` dans Unity. C'est un point
important, car une application cliente ne doit pas contenir une cle capable
de contourner les regles d'acces de la base.

Unity utilise la cle anon / publishable. Les acces sont donc limites par les
policies RLS de Supabase. Les policies permettent par exemple a un utilisateur
de gerer son propre profil, de voir ses relations d'amitie, de mettre a jour
sa presence et d'inserer un coup seulement si Supabase voit qu'il est le
joueur courant du match.

Les sessions utilisent un access token et un refresh token. Quand l'access
token expire, le projet tente de rafraichir la session automatiquement. Si le
refresh echoue, la session est nettoyee et l'utilisateur doit se reconnecter.

Les limites actuelles sont normales pour une premiere version. La validation
serveur des coups n'est pas complete : la logique des regles reste dans Unity.
Le polling est utilise a la place du realtime. Il n'y a pas encore de
classement, de timer, d'historique complet de parties, de reconnexion avancee
ni de systeme d'abandon dedie.

Ces limites ne bloquent pas le rendu du projet, mais elles indiquent les
prochaines etapes si le jeu devait evoluer vers une version plus robuste.

## 14. Tests manuels

Les tests manuels prevus pour valider le projet :

- creation de compte avec email, mot de passe et username ;
- connexion avec email ;
- connexion avec username ;
- refus propre d'une connexion avant confirmation email ;
- page GitHub Pages `email-confirmed` apres validation ;
- mot de passe oublie et page GitHub Pages de reset ;
- chargement du profil apres connexion ;
- ajout d'un ami par username ;
- acceptation et refus d'une demande d'ami ;
- affichage des amis acceptes ;
- presence online / offline avec deux comptes ;
- invitation d'un ami en Quixo ;
- invitation d'un ami en Qomet ;
- matchmaking aleatoire Quixo ;
- matchmaking aleatoire Qomet ;
- partie Quixo locale ;
- partie Qomet locale ;
- partie en ligne avec deux comptes ;
- verification dans l'editeur Unity ;
- verification d'un build Windows.

Ces tests doivent etre realises avec au moins deux comptes Supabase pour
valider correctement les amis, la presence, les invitations et le matchmaking.
Le projet contient aussi des tests EditMode pour les regles de Quixo et de
Qomet, mais le rapport ne suppose pas que toute la partie Unity et online a
ete validee automatiquement.

## 15. Difficultes rencontrees

Une premiere difficulte a ete l'adaptation de Qomet. Contrairement a Quixo,
Qomet ne peut pas etre traite comme une grille carree. Il faut afficher un
plateau qui ressemble visuellement a un reseau de points, mais garder une
logique de graphe pour savoir quels noeuds sont relies.

Cette separation entre rendu visuel et logique de graphe demande de la
rigueur. Une position peut exister dans la grille 7x7 utilisee par le code
sans etre un vrai noeud Qomet. Les regles doivent donc toujours verifier que
le noeud existe dans `QometGraph`.

La gestion des sessions Supabase a aussi demande du soin. Un access token
peut expirer pendant que le joueur utilise le menu, les amis ou le online. Le
projet a donc un helper capable de detecter l'expiration, de rafraichir la
session et de relancer la requete.

La synchronisation en ligne est une autre difficulte. Comme le projet utilise
le polling, il faut gerer les delais, les coups deja recus, le tour courant,
les fins de partie et les cas ou un match existe deja.

Le matchmaking a aussi un piege classique : si deux joueurs cherchent en meme
temps, ils peuvent chacun croire devoir creer le match. Le projet utilise un
tie-break base sur `created_at` et l'id utilisateur pour eviter que deux
matchs separes soient crees. Il ignore aussi les lignes trop anciennes pour
eviter de matcher avec un joueur qui a ferme Unity sans annuler.

L'interface amis demande egalement plusieurs etats : demande en attente, ami
accepte, ami online / offline, invitation recue, invitation envoyee puis
acceptee. Cela rend la vue plus complexe qu'une simple liste de profils.

Enfin, il fallait maintenir les modes locaux pendant l'ajout du online. Le
mode hors ligne et les parties locales doivent continuer a fonctionner meme
si Supabase n'est pas configure ou si l'utilisateur n'est pas connecte.

## 16. Ameliorations possibles

Une premiere amelioration serait d'utiliser Supabase Realtime pour recevoir
les coups et les invitations sans polling constant.

Une autre amelioration importante serait la validation serveur des coups. Des
RPC SQL ou des Edge Functions pourraient verifier qu'un coup est legal avant
de l'inserer dans `online_moves` et de modifier `online_matches`.

Le matchmaking pourrait aussi etre deplace cote serveur avec une fonction
atomique. Cela reduirait encore les risques de concurrence quand plusieurs
joueurs cherchent une partie en meme temps.

Une reconnexion a une partie en cours serait utile. Si un joueur ferme le jeu
puis revient, le client pourrait retrouver son match actif, rejouer les coups
stockes et reprendre la partie.

Un abandon propre permettrait de quitter une partie en donnant la victoire a
l'adversaire ou en marquant le match comme annule selon le contexte.

Le projet pourrait aussi ajouter un historique des parties, un classement, un
timer de tour, un tutoriel integre et de meilleures animations pour les pions,
les etoiles et les poussees.

Pour Qomet, des indications visuelles plus poussees pourraient aider le
joueur : noeuds accessibles apres selection, previsualisation d'une poussee,
mise en evidence du carre gagnant et animation du retour en reserve.

## 17. Conclusion

Le projet Quixo / Qomet propose une base jouable et coherente. Il regroupe
deux jeux dans une meme application Unity, avec une interface complete, des
modes locaux et une premiere version de jeu en ligne.

Supabase a permis d'ajouter un backend sans developper un serveur dedie. Les
comptes, les profils, les amis, la presence, les invitations, le matchmaking
et les coups en ligne passent par des tables PostgreSQL accessibles depuis
Unity en REST.

Le projet reste ameliorable, surtout sur la validation serveur, le realtime,
la reconnexion et les fonctionnalites competitives. Malgre cela, il propose
deja une base utilisable pour un rendu : Quixo est jouable, Qomet a sa
propre logique en graphe, et la couche en ligne permet de relier deux joueurs
dans des conditions simples.
