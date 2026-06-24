# Image multi-étapes du frontend Angular : build de la SPA puis service via Nginx.

# --- Étape 1 : build de la SPA Angular ---
FROM node:20-alpine AS build
WORKDIR /app
# Évite le téléchargement du binaire Cypress (~200 Mo) inutile pour le build.
ENV CYPRESS_INSTALL_BINARY=0
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

# --- Étape 2 : service Nginx (sert la SPA + proxifie /api vers l'API) ---
FROM nginx:alpine
COPY nginx.conf /etc/nginx/nginx.conf
COPY --from=build /app/dist/map-frontend/browser /usr/share/nginx/html
EXPOSE 80
