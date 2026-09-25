FROM node:22.22-alpine AS build
WORKDIR /app
COPY src/boe-radar-ui/package*.json ./
RUN npm ci
COPY src/boe-radar-ui/ .
RUN npm run build

FROM nginx:1.29-alpine AS final
COPY src/boe-radar-ui/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/boe-radar-ui/browser /usr/share/nginx/html
EXPOSE 8080
