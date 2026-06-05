# Page email confirme

URL GitHub Pages :
https://bouzekrimohamed.github.io/quixocomet/email-confirmed/

Page affichee apres confirmation email Supabase. Unity envoie cette URL dans
`redirect_to` lors de l'inscription (`EmailConfirmationRedirectUrl`).

Ajouter cette URL dans `Authentication > URL Configuration > Redirect URLs`.

Si Supabase redirige encore vers la racine `/#access_token=...&type=signup`,
verifier que l'URL est autorisee dans Supabase et reinscrire un compte test.
Une page de secours `docs/index.html` redirige aussi vers `/email-confirmed/`.

La page supprime le fragment `#access_token=...` de la barre d'adresse apres
affichage pour ne pas laisser le token visible.
