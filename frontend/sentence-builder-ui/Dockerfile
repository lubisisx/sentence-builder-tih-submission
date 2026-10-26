FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
ARG API_BASE_URL=/api
RUN API_BASE_URL="$API_BASE_URL" npm run build

FROM nginx:stable-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/sentence-builder-ui/browser/ /usr/share/nginx/html/
EXPOSE 80
