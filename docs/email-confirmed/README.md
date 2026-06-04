# Page email confirme

URL GitHub Pages :
https://bouzekrimohamed.github.io/quixocomet/email-confirmed/

Page de destination apres la confirmation d'une inscription Supabase. Elle ne
contient aucun secret et renvoie simplement vers la page de telechargement.

Dans Supabase, ajouter cette URL dans `Authentication > URL Configuration >
Redirect URLs`. La requete d'inscription Unity envoie aussi cette URL dans le
parametre `redirect_to`.
