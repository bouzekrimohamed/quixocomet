# Quixo / Qomet Unity

Projet Unity 2022.3.40f1 avec :

- Quixo local jouable;
- Qomet local jouable;
- themes visuels;
- intro video `powered_by_mohamed_bouzekri.mp4`;
- splash fallback `POWERED BY MOHAMED BOUZEKRI`;
- authentification Supabase email/password;
- reset password via GitHub Pages;
- login par email ou username;
- profil utilisateur simple;
- ajout d'amis;
- presence online/offline;
- invitations ami pour Quixo/Qomet;
- matchmaking public Quixo/Qomet;
- synchronisation des coups par polling Supabase.

## Telechargement et lancement rapide

La page de telechargement Windows, Linux et macOS est disponible ici :

https://bouzekrimohamed.github.io/quixocomet/download/

Telechargez l'archive adaptee, extrayez-la, puis lancez l'executable. Les builds
etudiants ne sont pas signes : Windows et macOS peuvent afficher un
avertissement au premier lancement. Sous Linux, rendez le fichier executable si
necessaire.

Apres une inscription, l'adresse email doit etre confirmee avant la premiere
connexion.

## Lancer dans Unity

1. Ouvrir `C:/Users/lm_bo/Documents/PROJET/quixocomet/unity`.
2. Stop Play si necessaire.
3. `Assets > Refresh`.
4. `Tools > Quixo > Create/Repair Scenes`.
5. Ouvrir `Assets/Scenes/IntroVideoScene`.
6. Play.

## Configurer Supabase

Voir `unity/SUPABASE_SETUP.md`.

Page GitHub Pages de reset password :

`docs/reset-password/`

Unity utilise seulement :

- Project URL;
- anon/publishable key.

Ne jamais mettre de cle `service_role` dans Unity.

## Build Windows

1. Regenerer les scenes avec le Scene Builder.
2. Verifier les Build Settings :
   - `IntroVideoScene`
   - `SplashScene`
   - `AuthScene`
   - `MenuScene`
   - `GameplayScene`
3. Build Windows x86_64.
4. Lancer le `.exe`.

## Online V1

Apres la migration SQL de `unity/SUPABASE_SETUP.md`, deux comptes connectes peuvent :

- voir la presence online/offline des amis;
- inviter un ami en Quixo ou Qomet;
- chercher un adversaire aleatoire;
- jouer une partie online avec synchronisation des coups.

La V1 online est client-authoritative. Une V2 devrait ajouter Supabase Realtime, RPC/Edge Functions pour valider les coups cote serveur, reconnexion, abandon, timer et classement.
