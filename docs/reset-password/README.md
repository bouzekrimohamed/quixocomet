# Quixo Qomet Reset Password Page

Page statique compatible GitHub Pages pour le reset password Supabase.

## GitHub Pages

Publier le dossier `docs/` avec GitHub Pages.

URL attendue :

`https://bouzekrimohamed.github.io/quixocomet/reset-password/`

## Supabase Auth URL Configuration

Dans `Authentication > Providers > Email`, activer `Confirm email` si vous souhaitez valider les emails avant connexion.

Dans `Authentication > URL Configuration` :

- Site URL : `https://bouzekrimohamed.github.io/quixocomet`
- Redirect URLs : `https://bouzekrimohamed.github.io/quixocomet/reset-password/`

## Template reset password

Dans `Authentication > Email Templates > Reset Password`, verifier que le lien utilise l'URL de redirection autorisee.

La page utilise :

- `@supabase/supabase-js@2` via CDN;
- Project URL publique;
- anon/publishable key uniquement;
- aucun stockage de mot de passe;
- aucune cle `service_role`.
