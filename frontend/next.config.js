/** @type {import('next').NextConfig} */
const nextConfig = {
  // La URL del backend se inyecta por variable de entorno en Vercel
  env: {
    NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000',
  },
  // Permite imágenes externas si se usan
  images: {
    remotePatterns: [],
  },
};

module.exports = nextConfig;
