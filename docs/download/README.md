# Page de telechargement

Page publiee par GitHub Pages a l'adresse :
https://bouzekrimohamed.github.io/quixocomet/download/

## Liens des builds

Les trois liens pointent vers les archives de la release GitHub `v1.0.0`.

- Windows : https://github.com/bouzekrimohamed/quixocomet/releases/download/v1.0.0/BuildWindows.zip
- Linux : https://github.com/bouzekrimohamed/quixocomet/releases/download/v1.0.0/BuildLinux.zip
- macOS : https://github.com/bouzekrimohamed/quixocomet/releases/download/v1.0.0/BuildMacOS.zip

## Procedures affichees sur la page

Windows : telecharger le ZIP, extraire, double-cliquer sur le `.exe`. Le
message SmartScreen est explique sur la page.

Linux : apres extraction, dans un terminal :

```bash
chmod +x QuixoQomet.x86_64
./QuixoQomet.x86_64
```

Si le nom varie :

```bash
chmod +x *.x86_64
./*.x86_64
```

macOS : apres extraction, si Gatekeeper bloque l'application :

```bash
# Retirer l'attribut de quarantaine
xattr -dr com.apple.quarantine QuixoQomet.app
chmod +x QuixoQomet.app/Contents/MacOS/*
```

Ou simplement : clic droit sur l'application → **Ouvrir** au premier lancement.

## Contact

Probleme urgent d'installation ou de lancement :
[lm_bouzekri@gmail.com](mailto:lm_bouzekri@gmail.com)

## Publier une nouvelle version

Modifier uniquement les valeurs de `DOWNLOADS` dans `app.js`. Les archives
restent dans GitHub Releases et ne doivent pas etre ajoutees au depot Git.
