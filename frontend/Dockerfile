# Stage 1: Build Next.js app with Bun
FROM oven/bun:latest AS builder

WORKDIR /app

# Copy dependency manifests
COPY package.json bun.lock ./

# Install dependencies
RUN bun install

# Copy application code
COPY . .

# Build static files (requires `output: 'export'` in next.config.js)
RUN bun run build

# Stage 2: Nginx static server
FROM nginx:alpine

# Copy static files from builder stage
COPY --from=builder /app/out /usr/share/nginx/html

# Copy custom Nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

# Health check (optional but recommended)
HEALTHCHECK --interval=30s --timeout=3s \
  CMD wget --quiet --tries=1 --spider http://localhost/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
