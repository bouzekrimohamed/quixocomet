# Page email confirme

URL GitHub Pages prevue :
https://bouzekrimohamed.github.io/quixocomet/email-confirmed/

Cette page est la destination utilisee apres la confirmation d'une inscription.
Elle ne contient aucun secret et renvoie vers la page de telechargement.

Dans Supabase, ajouter cette URL dans `Authentication > URL Configuration >
Redirect URLs`. La requete d'inscription Unity envoie aussi cette URL dans le
parametre `redirect_to`.
