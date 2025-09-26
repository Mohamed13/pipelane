# Build stage
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
COPY angular.json tsconfig*.json ./
COPY tools ./tools
COPY src ./src
COPY cypress ./cypress
COPY jest.config.js setup-jest.ts ./
COPY .env.example ./.env
ARG API_BASE_URL=http://api:5000
RUN echo "API_BASE_URL=$API_BASE_URL" > .env
RUN npm ci || npm install
RUN npm run build

# Runtime stage
FROM nginx:1.25-alpine AS runtime
COPY --from=build /app/dist/pipelane-front /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx","-g","daemon off;"]

