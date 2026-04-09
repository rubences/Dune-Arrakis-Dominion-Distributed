import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'Dune Arrakis Dominion — Multi-Agent Architecture Demo',
  description:
    'Demostrador de Arquitectura Multi-Agente basado en el universo Dune. ' +
    'Orquestación paralela de agentes IA con MediatR y CrewAI.',
  keywords: ['dune', 'multi-agent', 'AI', 'crewai', 'simulation', 'arrakis'],
  openGraph: {
    title: 'Dune Arrakis Dominion',
    description: 'Demostrador de Arquitectura Multi-Agente — 2026 Edition',
    type: 'website',
  },
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="es" className="dark">
      <head>
        <link
          href="https://fonts.googleapis.com/css2?family=Cinzel:wght@400;600;700&family=Rajdhani:wght@300;400;500;600&display=swap"
          rel="stylesheet"
        />
      </head>
      <body className="bg-sand-950 text-sand-100 font-rajdhani antialiased">
        {children}
      </body>
    </html>
  );
}
