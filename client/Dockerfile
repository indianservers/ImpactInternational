FROM node:24-alpine AS build
WORKDIR /src
COPY client/package*.json ./
RUN npm ci
COPY client/ ./
RUN npm run build
FROM nginx:stable-alpine
COPY client/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist /usr/share/nginx/html
EXPOSE 80
