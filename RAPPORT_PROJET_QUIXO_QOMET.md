# Rapport du projet Quixo / Qomet

## Mode d'emploi et telechargement

**Auteurs :** BOUZEKRI Mohamed, MEHDI Kirouche, ABEL Filiciaggi et LIGH Douang.

Le jeu peut etre telecharge depuis la page
https://bouzekrimohamed.github.io/quixocomet/download/. Elle propose les builds
Windows et Linux. La version macOS est indiquee comme bientot disponible.

Sous Windows, il faut telecharger le ZIP, l'extraire puis lancer le fichier
`.exe`. Windows peut afficher un avertissement, car ce build etudiant n'est pas
signe. Sous Linux, il faut extraire le telechargement puis lancer :

```bash
chmod +x QuixoQomet.x86_64
./QuixoQomet.x86_64
```

Si le nom varie, les commandes `chmod +x *.x86_64` puis `./*.x86_64`
permettent de lancer le build.

Pour creer un compte, ouvrir l'onglet d'inscription, saisir un email, un
username et un mot de passe, puis confirmer l'adresse avec le lien recu par
email. La connexion est refusee tant que l'email n'est pas confirme. Une fois
le lien valide, Supabase redirige vers
https://bouzekrimohamed.github.io/quixocomet/email-confirmed/. La connexion est
ensuite possible avec l'email ou le username.

Pour une partie locale, suivre `Jouer > Jouer en local`, puis choisir Quixo ou
Qomet. Ce parcours fonctionne aussi en mode invite. Pour une partie en ligne,
il faut etre connecte, suivre `Jouer > Jouer en ligne`, puis choisir le
matchmaking Quixo ou Qomet. Il est aussi possible d'ouvrir `Amis` et d'inviter
directement un joueur.

Dans le menu Amis, saisir le username recherche puis envoyer une demande. Le
destinataire peut l'accepter ou la refuser. Une invitation de partie acceptee
ne lance jamais le jeu toute seule : le joueur choisit quand la rejoindre.

En cas d'oubli du mot de passe, saisir l'adresse email sur l'ecran de connexion
et utiliser `Mot de passe oublie`. Le lien recu ouvre la page de
reinitialisation du projet.

Quand une partie Quixo ou Qomet se termine, une fenetre affiche le gagnant et
propose un retour au menu. Le bouton Rejouer est disponible en local et masque
en ligne pour ne pas casser la synchronisation.

En cas de probleme urgent avec l'installation ou le lancement, le contact
developpeur est [lm_bouzekri@gmail.com](mailto:lm_bouzekri@gmail.com).

## 1. Introduction

Ce projet est une application de jeu réalisée avec Unity. Elle regroupe deux jeux abstraits : Quixo et Qomet. L'objectif principal était d'obtenir une version jouable, présentable et suffisamment complète pour montrer à la fois la partie gameplay, l'interface et une première couche de jeu en ligne.

Le projet permet de jouer localement à Quixo et à Qomet. Il ajoute aussi une partie connectée avec Supabase : création de compte, connexion, profils, amis, présence en ligne, invitations et matchmaking. L'ensemble reste volontairement simple côté serveur, car Unity communique directement avec Supabase via des requêtes REST.

Le résultat est une base de jeu complète pour une première version : les deux jeux sont séparés dans leur logique, l'utilisateur peut se connecter ou continuer hors ligne, et les parties en ligne peuvent être synchronisées entre deux comptes.

## 2. Objectifs du projet

Le premier objectif était de proposer une interface jouable dans Unity. Le projet ne devait pas seulement contenir les règles des jeux, mais aussi un vrai parcours utilisateur : une scène d'introduction, une scène d'authentification, un menu, un affichage du plateau, un HUD et des boutons adaptés aux différents modes.

Le deuxième objectif était d'intégrer deux jeux dans une même base Unity. Quixo et Qomet utilisent tous les deux un plateau et deux joueurs, mais leurs règles sont différentes. Il fallait donc garder une structure commune pour l'affichage et le déroulement de partie, tout en séparant clairement la logique de chaque jeu.

Le troisième objectif était d'ajouter une couche compte et social. Un joueur peut créer un compte avec un email, un mot de passe et un username. Il peut ensuite se connecter avec son email ou son username, ajouter des amis, répondre à des demandes et inviter un ami à jouer.

Le quatrième objectif était de permettre le jeu en ligne. Deux chemins ont été prévus : l'invitation entre amis et le matchmaking aléatoire. Dans les deux cas, le match et les coups joués sont stockés dans Supabase, puis relus par polling depuis Unity.

Enfin, le projet devait rendre Qomet vraiment différent de Quixo. Qomet n'est pas seulement une variante graphique : il repose sur un graphe de 25 nœuds, des connexions, des réserves d'étoiles, des déplacements, des poussées et une condition de victoire par carré.

## 3. Technologies utilisées

Unity est utilisé comme moteur principal du jeu. Il gère les scènes, l'interface, les interactions avec le plateau, les boutons, l'affichage des pions et le build Windows.

C# est utilisé pour les scripts de gameplay, les services, l'interface et la communication avec Supabase. Les règles de Quixo et de Qomet sont codées côté Unity, de même que les contrôleurs de menu, d'authentification et de partie.

Supabase Auth sert à gérer l'inscription, la connexion, les sessions utilisateur et la réinitialisation du mot de passe. Les mots de passe ne sont pas stockés dans une table du projet : ils restent gérés par Supabase Auth.

Supabase PostgreSQL sert à stocker les données publiques ou liées au jeu : profils, relations d'amitié, présence, invitations, file de matchmaking, matchs et coups joués.

Supabase REST permet à Unity de communiquer avec la base sans serveur dédié. Les scripts utilisent `UnityWebRequest` pour envoyer des requêtes HTTP vers l'API Supabase.

GitHub Pages est utilisé pour héberger la page de réinitialisation du mot de passe, la page de confirmation email et la page de téléchargement. La page de reset reçoit les tokens envoyés par Supabase et permet à l'utilisateur de choisir un nouveau mot de passe.

Git et GitHub servent au versionnement et au partage du projet. Le projet contient aussi des fichiers de configuration et des guides pour ouvrir Unity, configurer Supabase et préparer un build Windows.

## 4. Organisation générale du projet

Le code Unity est organisé en dossiers simples, chacun avec un rôle assez clair.

Le dossier `Auth` contient la connexion, l'inscription, la session utilisateur, le stockage des tokens et les requêtes authentifiées vers Supabase. On y trouve notamment `AuthService`, `SessionManager` et `SupabaseRequestHelper`.

Le dossier `Social` contient la gestion des amis. Il permet de chercher un profil par username, d'envoyer une demande, de l'accepter ou de la refuser, puis de charger la liste des amis acceptés.

Le dossier `Online` contient la présence, les invitations de match, le matchmaking et la synchronisation des coups. Cette partie repose sur des tables Supabase et sur du polling depuis Unity.

Le dossier `Gameplay` contient le déroulement d'une partie. `GameFlowController` choisit les règles selon le jeu, gère les clics sur le plateau, applique les coups, met à jour le HUD et bloque les actions quand le joueur n'a pas la main en ligne.

Le dossier `Core` contient les règles et les structures de base : état du plateau, joueur courant, directions de mouvement, logique Quixo, graphe Qomet et règles Qomet.

Le dossier `UI` contient les vues : authentification, menu, amis, HUD, rendu du plateau, thèmes visuels, splash et intro vidéo.

Enfin, `Editor / QuixoSceneBuilder` sert à créer ou réparer les scènes Unity. Il prépare notamment les scènes `IntroVideoScene`, `SplashScene`, `AuthScene`, `MenuScene` et `GameplayScene`, puis les ajoute aux Build Settings.

Cette organisation reste simple. Elle ne cherche pas à imiter une grosse architecture serveur, mais elle permet de séparer les responsabilités principales du projet.

## 5. Authentification et gestion des comptes

L'authentification repose sur Supabase Auth. L'utilisateur peut s'inscrire avec un email, un mot de passe et un username. Après l'inscription, le projet crée ou met à jour un profil dans la table `profiles`, avec l'identifiant Supabase de l'utilisateur, son username, son display name et son email.

La connexion accepte deux formats. Si l'identifiant contient un `@`, il est traité comme un email et envoyé directement à Supabase Auth. Sinon, Unity cherche d'abord dans `profiles` l'email correspondant au username, puis utilise cet email pour la connexion Supabase.

Le mot de passe oublié passe par l'endpoint Supabase `recover`. Unity envoie l'email et une URL de redirection vers la page GitHub Pages du projet. Cette page statique utilise Supabase JS pour appliquer le nouveau mot de passe avec les tokens fournis dans le lien.

La confirmation email est obligatoire. La requête d'inscription indique la page `email-confirmed` comme redirection et Unity refuse proprement la connexion tant que Supabase ne renvoie pas un email confirmé.

Les sessions sont stockées côté Unity avec `PlayerPrefs`. Le projet conserve l'access token, le refresh token, l'id utilisateur, l'email et le username. Quand une requête authentifiée échoue à cause d'un JWT expiré ou invalide, `SupabaseRequestHelper` tente de rafraîchir la session avec le refresh token, puis relance la requête.

Le projet fait attention à ne pas utiliser de clé `service_role` dans Unity. Unity utilise uniquement l'URL du projet Supabase et la clé anon/publishable. Les droits d'accès sont donc gérés par les policies RLS côté Supabase.

Un mode hors ligne existe aussi. Si Supabase n'est pas configuré, ou si l'utilisateur ne veut pas se connecter, il peut continuer comme invité. Dans ce cas, les fonctions sociales et en ligne ne sont pas disponibles, mais les modes locaux restent utilisables.

## 6. Système d'amis

Le système d'amis utilise la table `friends`. Pour ajouter un joueur, l'utilisateur saisit un username. Unity normalise ce username en minuscules, avec uniquement des lettres, des chiffres et des underscores, puis cherche le profil correspondant dans `profiles`.

Avant d'envoyer une demande, le service vérifie qu'il ne s'agit pas de l'utilisateur courant et qu'une relation n'existe pas déjà entre les deux joueurs. Si tout est correct, une ligne est créée dans `friends` avec le statut `pending`.

Le joueur qui reçoit la demande peut l'accepter ou la refuser. L'acceptation met le statut à `accepted`, le refus met le statut à `rejected`. La liste des amis affichée dans l'interface ne reprend que les relations acceptées.

Le panneau d'amis affiche aussi les demandes reçues, les amis acceptés et les invitations de partie. Depuis la ligne d'un ami, il est possible d'envoyer une invitation pour Quixo ou pour Qomet.

Les erreurs Supabase sont converties en messages plus lisibles côté UI. Par exemple, une clé dupliquée peut devenir un message du type "Vous êtes déjà amis ou une demande existe déjà", et une erreur RLS est affichée comme un problème d'autorisation ou de policies.

## 7. Présence online/offline

La présence en ligne est gérée avec la table `user_presence`. Quand un utilisateur connecté est actif, Unity envoie régulièrement un heartbeat. Ce heartbeat met à jour le statut, le username et surtout `last_seen_at`.

L'affichage des amis utilise ensuite cette information pour déterminer si un ami est en ligne. Dans le code, un utilisateur est considéré online si son statut est `online` et si son `last_seen_at` est assez récent. L'interface peut alors afficher une pastille verte ou rouge.

Cette approche est simple et adaptée à une V1. Elle a toutefois une limite normale : si l'application se ferme brutalement, Unity ne peut pas forcément envoyer un statut `offline`. Le joueur peut donc rester affiché en ligne quelques secondes, jusqu'à ce que son dernier heartbeat soit considéré trop ancien.

## 8. Multijoueur en ligne

Le multijoueur en ligne propose deux façons de démarrer une partie.

La première est l'invitation entre amis. Un joueur envoie une invitation à un ami pour Quixo ou Qomet. L'invitation est stockée dans `match_invites` avec le statut `pending`. Quand le destinataire accepte, Unity crée un match dans `online_matches`, met l'invitation à `accepted` et associe le `match_id`.

La deuxième est le matchmaking aléatoire. Quand un joueur cherche une partie, Unity crée ou remet à zéro sa ligne dans `matchmaking_queue`. Le client cherche ensuite un autre joueur en attente pour le même jeu. Si un adversaire est trouvé, un match est créé et les deux lignes de queue sont marquées comme `matched`.

Les matchs sont stockés dans `online_matches`. Cette table contient le type de jeu, `player1_id`, `player2_id`, `current_turn_id`, le statut du match et éventuellement le gagnant. Le joueur `player1` commence toujours, car `current_turn_id` est initialisé avec `player1_id`.

Les coups joués sont stockés dans `online_moves`. Chaque coup contient un numéro, le joueur qui l'a envoyé et un payload JSON. Pour Quixo, le payload contient notamment la case sélectionnée et la direction. Pour Qomet, il distingue la pose d'une étoile et le déplacement d'une étoile entre deux nœuds.

La synchronisation se fait par polling. Pendant une partie en ligne, Unity récupère régulièrement les nouveaux coups et l'état du match. Le client local applique les coups adverses lorsqu'ils apparaissent dans `online_moves`.

Le mode online bloque les actions si ce n'est pas le tour du joueur. Le HUD indique si le joueur doit jouer ou attendre l'adversaire. Après un coup valide, Unity envoie le coup, met à jour le tour suivant et termine le match si un gagnant est détecté.

Le matchmaking contient quelques protections utiles. Les lignes de queue trop anciennes sont ignorées pour éviter de matcher un joueur avec une ancienne session fermée. Le projet annule aussi les anciennes queues du joueur avant une nouvelle recherche, et un tie-break déterministe évite que deux joueurs créent chacun un match séparé.

### Limite de la V1 online

La version actuelle est principalement client-authoritative. Cela signifie que Unity valide les coups côté client, puis envoie le résultat à Supabase.

Supabase contrôle surtout l'identité de l'utilisateur, les droits d'accès avec RLS, le tour courant et l'état du match. Cette base est suffisante pour une première version étudiante, mais elle n'empêche pas complètement un client modifié d'envoyer un coup incorrect.

Une V2 plus solide pourrait utiliser des RPC SQL ou des Edge Functions pour valider les coups côté serveur. Elle pourrait aussi remplacer une partie du polling par Supabase Realtime.

## 9. Mode Quixo

Quixo est joué sur un plateau 5x5. Le joueur sélectionne un cube du bord qui est neutre ou qui lui appartient, puis choisit une direction d'insertion. Le déplacement pousse la ligne ou la colonne correspondante et place le symbole du joueur à l'autre extrémité.

Le projet affiche les marques des deux joueurs avec X et O. La victoire est détectée quand un joueur obtient une ligne, une colonne ou une diagonale complète. Comme dans les règles classiques, si le coup crée aussi une ligne gagnante pour l'adversaire, la vérification de l'adversaire est prioritaire.

Techniquement, Quixo repose sur une logique de grille. Les cases sont indexées par ligne et colonne, et les directions possibles sont calculées à partir de la position du cube sélectionné.

Quixo est jouable en local et en ligne. En ligne, les informations nécessaires au coup sont envoyées dans le payload, puis rejouées sur le client adverse.

## 10. Mode Qomet

Qomet est une partie importante du projet, car ce n'est pas une simple variante visuelle de Quixo. Le plateau ne fonctionne pas comme une grille classique de 25 cases. Il est représenté par un graphe de 25 nœuds disposés dans une grille logique 7x7, avec seulement certaines positions valides.

La disposition visuelle correspond à une forme 3-3-3-7-3-3-3 : trois nœuds en haut, trois sur les lignes suivantes, sept au centre, puis à nouveau trois nœuds par ligne vers le bas. Les nœuds sont identifiés de A à Y et reliés par des connexions définies dans `QometGraph`.

Chaque joueur possède une réserve de 7 étoiles. Le plateau est vide au départ. Tant qu'un joueur a des étoiles en réserve, il peut poser une étoile sur un nœud vide. Une fois la réserve vide, ou selon la situation de jeu, il peut sélectionner une étoile de sa couleur et la déplacer vers un nœud relié.

Le déplacement se fait uniquement le long des connexions du graphe. Si le nœud d'arrivée est vide, l'étoile se déplace simplement. Si le nœud d'arrivée contient une étoile, une poussée peut être tentée dans la même direction.

La poussée ne déplace qu'une seule étoile. Si le nœud suivant dans la direction existe et est vide, l'étoile poussée y est déplacée. Si aucun nœud suivant n'existe, l'étoile sort du plateau et retourne dans la réserve de son propriétaire. Si une deuxième étoile bloque la ligne, la poussée est interdite.

Le code interdit aussi le coup inverse immédiat. Cela évite qu'un joueur annule directement le déplacement précédent en revenant exactement de `to` vers `from`.

La victoire se fait par formation d'un carré. Le projet utilise une liste de carrés gagnants pré-calculés, avec des quadruplets de nœuds. Cette solution évite de détecter de faux carrés à cause des positions visuelles ou des calculs flottants.

Après un coup, Qomet vérifie d'abord si l'adversaire possède un carré. Si c'est le cas, l'adversaire gagne. Sinon, le jeu vérifie si le joueur qui vient de jouer a créé son propre carré. Cette priorité est importante, car une poussée peut produire une situation favorable à l'autre joueur.

La différence technique avec Quixo est donc nette. Quixo utilise une grille 5x5 et des directions de poussée sur lignes ou colonnes. Qomet utilise un graphe de nœuds et de connexions, avec une réserve, des mouvements le long des arêtes et une victoire par carrés prédéfinis.

## 11. Interface utilisateur

L'interface est organisée autour de plusieurs scènes.

`AuthScene` sert à la connexion, l'inscription, le mot de passe oublié et le mode invité. Elle permet de continuer hors ligne si Supabase n'est pas configuré ou si l'utilisateur ne veut pas se connecter.

`MenuScene` donne accès aux modes locaux, aux modes en ligne, au panneau d'amis, aux thèmes, à la déconnexion et à la sortie du jeu. Les boutons online et amis dépendent de l'état de session de l'utilisateur.

`FriendsView` affiche les demandes d'amis, les amis acceptés, leur présence et les invitations de match. C'est aussi depuis cette vue qu'un joueur peut inviter un ami en Quixo ou en Qomet.

`GameplayScene` contient le plateau, le HUD, les messages de tour, les messages de victoire et les contrôles de jeu. Pour Quixo, le HUD affiche les directions possibles. Pour Qomet, l'interaction se fait davantage par sélection de nœuds reliés.

Le projet contient aussi une intro vidéo et un splash de secours. Si la vidéo est disponible, elle est jouée au démarrage ; sinon, le projet peut passer au flux suivant avec un affichage fallback.

## 12. Base de données Supabase

La table `profiles` contient les informations publiques du joueur : id, username, display name, email et date de création. Elle sert au login par username et à l'affichage des amis.

La table `friends` contient les relations entre deux profils. Elle stocke le demandeur, le receveur et le statut de la relation : `pending`, `accepted`, `rejected` ou `blocked`.

La table `user_presence` contient la présence en ligne. Elle stocke l'utilisateur, son username, son statut et `last_seen_at`, utilisé pour savoir si le joueur est encore considéré connecté.

La table `match_invites` contient les invitations entre amis. Elle indique l'expéditeur, le destinataire, le jeu choisi, le statut de l'invitation et éventuellement le match créé après acceptation.

La table `matchmaking_queue` sert à trouver un adversaire aléatoire. Une ligne indique qu'un joueur cherche une partie pour Quixo ou Qomet. Le code utilise `created_at` et `updated_at` pour départager les joueurs et ignorer les anciennes lignes.

La table `online_matches` représente une partie en ligne. Elle contient les deux joueurs, le joueur qui doit jouer, le statut du match et le gagnant si la partie est terminée.

La table `online_moves` contient les coups joués. Chaque coup est lié à un match, à un joueur, à un numéro de coup et à un payload JSON qui permet de rejouer l'action côté adverse.

## 13. Sécurité et limites

Le projet évite de mettre une clé `service_role` dans Unity. C'est un point important, car une application cliente ne doit pas contenir une clé capable de contourner les règles d'accès de la base.

Unity utilise la clé anon/publishable. Les accès sont donc limités par les policies RLS de Supabase. Les policies permettent par exemple à un utilisateur de gérer son propre profil, de voir ses relations d'amitié, de mettre à jour sa présence et d'insérer un coup seulement si Supabase voit qu'il est le joueur courant du match.

Les sessions utilisent un access token et un refresh token. Quand l'access token expire, le projet tente de rafraîchir la session automatiquement. Si le refresh échoue, la session est nettoyée et l'utilisateur doit se reconnecter.

Les limites actuelles sont normales pour une première version. La validation serveur des coups n'est pas complète, car la logique de règles reste dans Unity. Le polling est utilisé à la place du realtime. Il n'y a pas encore de classement, de timer, d'historique complet de parties, de reconnexion avancée ou de système d'abandon propre.

Ces limites ne bloquent pas le rendu du projet, mais elles indiquent clairement les prochaines étapes si le jeu devait évoluer vers une version plus robuste.

## 14. Tests manuels

Les tests manuels prévus pour valider le projet sont les suivants :

- création de compte avec email, mot de passe et username ;
- connexion avec email ;
- connexion avec username ;
- refus propre d'une connexion avant confirmation email ;
- page GitHub Pages `email-confirmed` après validation ;
- mot de passe oublié et page GitHub Pages de reset ;
- chargement du profil après connexion ;
- ajout d'un ami par username ;
- acceptation et refus d'une demande d'ami ;
- affichage des amis acceptés ;
- présence online/offline avec deux comptes ;
- invitation d'un ami en Quixo ;
- invitation d'un ami en Qomet ;
- matchmaking aléatoire Quixo ;
- matchmaking aléatoire Qomet ;
- partie Quixo locale ;
- partie Qomet locale ;
- partie online avec deux comptes ;
- vérification dans Unity Editor ;
- vérification d'un build Windows.

Ces tests doivent être réalisés avec au moins deux comptes Supabase pour valider correctement les amis, la présence, les invitations et le matchmaking. Le projet contient aussi des tests EditMode pour les règles de Quixo et de Qomet, mais le rapport ne suppose pas que toute la partie Unity et online a été validée automatiquement.

## 15. Difficultés rencontrées

Une première difficulté a été l'adaptation de Qomet. Contrairement à Quixo, Qomet ne peut pas être traité comme une simple grille carrée. Il faut afficher un plateau qui ressemble visuellement à un réseau de points, mais garder une logique de graphe pour savoir quels nœuds sont reliés.

Cette séparation entre rendu visuel et logique de graphe demande de faire attention. Une position peut exister dans la grille 7x7 utilisée par le code sans être un vrai nœud Qomet. Les règles doivent donc toujours vérifier que le nœud existe dans `QometGraph`.

La gestion des sessions Supabase a aussi demandé du soin. Un access token peut expirer pendant que le joueur utilise le menu, les amis ou le online. Le projet a donc besoin d'un helper capable de détecter l'expiration, de rafraîchir la session et de relancer la requête.

La synchronisation online est une autre difficulté. Comme le projet utilise le polling, il faut gérer les délais, les coups déjà reçus, le tour courant, les fins de partie et les cas où un match existe déjà.

Le matchmaking a aussi un piège classique : si deux joueurs cherchent en même temps, ils peuvent chacun croire devoir créer le match. Le projet utilise un tie-break basé sur `created_at` et l'id utilisateur pour éviter que deux matchs séparés soient créés. Il ignore aussi les lignes trop anciennes pour éviter de matcher avec un joueur qui a fermé Unity sans annuler.

L'interface amis demande également plusieurs états : demande en attente, ami accepté, ami online/offline, invitation reçue, invitation envoyée puis acceptée. Cela rend la vue plus complexe qu'une simple liste de profils.

Enfin, il fallait maintenir les modes locaux pendant l'ajout du online. Le mode hors ligne et les parties locales doivent continuer à fonctionner même si Supabase n'est pas configuré ou si l'utilisateur n'est pas connecté.

## 16. Améliorations possibles

La première amélioration serait d'utiliser Supabase Realtime pour recevoir les coups et les invitations plus rapidement, sans polling constant.

Une autre amélioration importante serait la validation serveur des coups. Des RPC SQL ou des Edge Functions pourraient vérifier qu'un coup est légal avant de l'insérer dans `online_moves` et de modifier `online_matches`.

Le matchmaking pourrait aussi être déplacé côté serveur avec une fonction atomique. Cela réduirait encore les risques de concurrence quand plusieurs joueurs cherchent une partie en même temps.

Une reconnexion à une partie en cours serait utile. Si un joueur ferme le jeu puis revient, le client pourrait retrouver son match actif, rejouer les coups stockés et reprendre la partie.

Un abandon propre permettrait de quitter une partie en donnant la victoire à l'adversaire ou en marquant le match comme annulé selon le contexte.

Le projet pourrait aussi ajouter un historique des parties, un classement, un timer de tour, un tutoriel intégré et de meilleures animations pour les pions, les étoiles et les poussées.

Pour Qomet, des indications visuelles plus poussées pourraient aider le joueur : nœuds accessibles après sélection, prévisualisation d'une poussée, mise en évidence du carré gagnant et animation du retour en réserve.

## 17. Conclusion

Le projet Quixo / Qomet propose une base jouable et cohérente. Il regroupe deux jeux abstraits dans une même application Unity, avec une interface complète, des modes locaux et une première version de jeu en ligne.

Supabase a permis d'ajouter un backend sans développer un serveur complet. Les comptes, les profils, les amis, la présence, les invitations, le matchmaking et les coups en ligne passent par des tables PostgreSQL accessibles depuis Unity en REST.

Le projet reste améliorable, surtout sur la validation serveur, le realtime, la reconnexion et les fonctionnalités compétitives. Malgré cela, il présente déjà une base solide pour un rendu : Quixo est jouable, Qomet possède une logique propre en graphe, et la couche online permet de relier deux joueurs dans des conditions simples.
