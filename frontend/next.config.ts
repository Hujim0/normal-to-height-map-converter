import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  reactCompiler: true,
  output: 'export',
  distDir: 'out',
  async rewrites() {
    return [
      {
        source: '/api/:path*',
        destination: 'http://localhost:8080/api/:path*',
      },
            {
        source: '/uploads/:path*',
        destination: 'http://localhost/uploads/:path*',
      },
    ];
  },
};

export default nextConfig;
