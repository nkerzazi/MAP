# Assets statiques du frontend

Déposez ici les images servies par l'application (copiées telles quelles dans le build Angular).

## Logo MAP

L'en-tête des trois espaces (public, éditeur, admin) référence :

```
src/assets/map-logo.png
```

➡️ **Placez le logo officiel de la MAP à cet emplacement** (nom exact : `map-logo.png`).

- Format recommandé : **PNG** (fond transparent ou blanc) ou **SVG** (dans ce cas, mettre à jour
  l'extension `assets/map-logo.svg` dans les coquilles `*-shell.component.ts`).
- Hauteur d'affichage : ~32–36 px (la largeur s'ajuste automatiquement, `w-auto`).

Tant que le fichier n'est pas présent, l'en-tête affiche le texte alternatif « MAP » à côté du
libellé de l'espace.
