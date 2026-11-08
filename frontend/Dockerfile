FROM node:22-alpine AS deps
WORKDIR /app
COPY package*.json ./
RUN npm install

FROM node:22-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .

# Argumentos públicos requeridos para incrustar en el JS del cliente
ARG PUBLIC_API_URL
ARG PUBLIC_ECOMMERCE_API_URL
ARG PUBLIC_BANK_TRANSFER_INFO
ENV PUBLIC_API_URL=$PUBLIC_API_URL
ENV PUBLIC_ECOMMERCE_API_URL=$PUBLIC_ECOMMERCE_API_URL
ENV PUBLIC_BANK_TRANSFER_INFO=$PUBLIC_BANK_TRANSFER_INFO
ENV OUTPUT_ADAPTER=node

RUN npm run build

FROM node:22-alpine AS runtime
WORKDIR /app
ENV HOST=0.0.0.0
ENV PORT=4321
ENV NODE_ENV=production

COPY --from=build /app/dist ./dist
COPY --from=deps /app/node_modules ./node_modules
COPY package.json ./

EXPOSE 4321
CMD ["node", "./dist/server/entry.mjs"]