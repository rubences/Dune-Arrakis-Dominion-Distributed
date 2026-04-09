'use client';

import { useState, useCallback } from 'react';
import { GameState, SimulationResult, SimulationEvent, Enclave, CREATURE_CATALOG, EVENT_ICONS, SCENARIO_NAMES } from '@/lib/types';
import * as api from '@/lib/api';

// ── Tipos de estado de agentes ─────────────────────────────────────────────────
type AgentStatus = 'idle' | 'processing' | 'done' | 'error';
interface AgentState { status: AgentStatus; message: string; }

// ── Componentes pequeños ───────────────────────────────────────────────────────

function AgentCard({ name, icon, state }: { name: string; icon: string; state: AgentState }) {
  const colors: Record<AgentStatus, string> = {
    idle:       'border-sand-600 text-sand-400',
    processing: 'border-holo text-holo animate-pulse-holo glow-holo',
    done:       'border-success/40 text-success',
    error:      'border-danger/40 text-danger',
  };
  const dots: Record<AgentStatus, string> = {
    idle: 'bg-sand-500', processing: 'bg-holo animate-ping', done: 'bg-success', error: 'bg-danger',
  };

  return (
    <div className={`card p-4 transition-all duration-500 ${colors[state.status]}`}>
      <div className="flex items-center gap-3 mb-2">
        <span className="text-2xl">{icon}</span>
        <div>
          <p className="font-cinzel text-sm tracking-wider">{name}</p>
          <div className="flex items-center gap-2 mt-1">
            <span className={`w-2 h-2 rounded-full ${dots[state.status]}`} />
            <span className="text-xs opacity-70">{state.message}</span>
          </div>
        </div>
      </div>
    </div>
  );
}

function HealthBar({ value, max = 100 }: { value: number; max?: number }) {
  const pct = Math.max(0, Math.min(100, (value / max) * 100));
  const color = pct >= 75 ? '#44ff88' : pct >= 40 ? '#ffcc44' : '#ff4455';
  return (
    <div className="w-full bg-sand-800 rounded-full h-1.5 overflow-hidden">
      <div className="h-full rounded-full transition-all duration-700" style={{ width: `${pct}%`, background: color }} />
    </div>
  );
}

function SolarisCounter({ value }: { value: number }) {
  return (
    <span className="font-cinzel text-gold font-bold">
      ◈ {value.toLocaleString('es-ES')}
    </span>
  );
}

function EventFeed({ events }: { events: SimulationEvent[] }) {
  return (
    <div className="space-y-2 max-h-72 overflow-y-auto pr-1">
      {events.length === 0 && (
        <p className="text-sand-500 text-sm italic text-center py-4">Sin eventos aún. Procesa el primer mes.</p>
      )}
      {[...events].reverse().slice(0, 30).map((ev, i) => (
        <div
          key={i}
          className="flex items-start gap-2 text-sm animate-fade-in-up bg-sand-900/50 rounded px-3 py-2"
          style={{ animationDelay: `${i * 30}ms` }}
        >
          <span className="shrink-0 mt-0.5">{EVENT_ICONS[ev.eventType] ?? '📋'}</span>
          <span className="text-sand-200 flex-1">{ev.description}</span>
          {ev.solarisChange !== 0 && (
            <span className={`shrink-0 font-bold text-xs ${ev.solarisChange > 0 ? 'text-success' : 'text-danger'}`}>
              {ev.solarisChange > 0 ? '+' : ''}{ev.solarisChange.toLocaleString('es-ES')} ◈
            </span>
          )}
        </div>
      ))}
    </div>
  );
}

function EnclaveCard({ enclave }: { enclave: Enclave }) {
  const alive = enclave.creatures.filter(c => c.isAlive);
  const avgHealth = alive.length ? Math.round(alive.reduce((s, c) => s + c.health, 0) / alive.length) : 0;
  const isExhibicion = enclave.type === 1;

  return (
    <div className={`card p-5 ${isExhibicion ? 'border-gold/30' : 'border-holo/20'}`}>
      <div className="flex justify-between items-start mb-3">
        <div>
          <h3 className="font-cinzel text-sm tracking-wider text-sand-200">{enclave.name}</h3>
          <span className={`badge mt-1 ${isExhibicion ? 'badge-gold' : 'badge-holo'}`}>
            {isExhibicion ? '🎭 Exhibición' : '🧪 Aclimatación'}
          </span>
        </div>
        <div className="text-right text-xs text-sand-400">
          <p className="font-bold text-sand-200">{alive.length}<span className="text-sand-500">/{enclave.maxCreatureCapacity}</span></p>
          <p>criaturas</p>
        </div>
      </div>

      {alive.length > 0 && (
        <div className="mb-3">
          <div className="flex justify-between text-xs text-sand-400 mb-1">
            <span>Salud media</span>
            <span className="font-bold" style={{ color: avgHealth >= 75 ? '#44ff88' : avgHealth >= 40 ? '#ffcc44' : '#ff4455' }}>
              {avgHealth}%
            </span>
          </div>
          <HealthBar value={avgHealth} />
        </div>
      )}

      {isExhibicion && (
        <div className="flex items-center gap-2 text-xs text-sand-400">
          <span>👥</span>
          <span>{enclave.totalVisitorsThisMonth.toLocaleString('es-ES')} visitantes este mes</span>
        </div>
      )}

      {/* Lista de criaturas */}
      <div className="mt-3 space-y-2">
        {alive.map(c => {
          const info = CREATURE_CATALOG.find(x => x.type === c.type);
          return (
            <div key={c.id} className="flex items-center gap-2 text-xs bg-sand-900/60 rounded px-2 py-1.5">
              <span>{info?.emoji ?? '🐾'}</span>
              <span className="flex-1 text-sand-200">{c.name}</span>
              <HealthBar value={c.health} />
              <span className="w-8 text-right" style={{ color: c.health >= 75 ? '#44ff88' : c.health >= 40 ? '#ffcc44' : '#ff4455' }}>
                {c.health}
              </span>
            </div>
          );
        })}
        {alive.length === 0 && (
          <p className="text-sand-600 text-xs italic text-center py-2">Sin criaturas vivas</p>
        )}
      </div>
    </div>
  );
}

// ── Pantalla Principal ─────────────────────────────────────────────────────────
export default function GamePage() {
  const [gameState, setGameState] = useState<GameState | null>(null);
  const [lastResult, setLastResult] = useState<SimulationResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [phase, setPhase] = useState<'menu' | 'planning' | 'resolving' | 'results'>('menu');

  const [strategicAgent, setStrategicAgent] = useState<AgentState>({ status: 'idle', message: 'En espera' });
  const [logisticsAgent, setLogisticsAgent] = useState<AgentState>({ status: 'idle', message: 'En espera' });

  const [saveName, setSaveName] = useState('Arrakeen-2026');
  const [scenarioType, setScenarioType] = useState(0);
  const [advice, setAdvice] = useState<string | null>(null);

  const clearError = () => setError(null);

  const [health, setHealth] = useState<{ status: string; version: string } | null>(null);
  const [isWakingUp, setIsWakingUp] = useState(false);

  // ── Handlers ────────────────────────────────────────────────────────────────

  // Efecto inicial para checkear salud
  useState(() => {
    const check = async () => {
      setIsWakingUp(true);
      const data = await api.checkHealth();
      setHealth(data);
      setIsWakingUp(false);
    };
    check();
  });

  const handleNewGame = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const gs = await api.newGame(scenarioType, saveName);
      setGameState(gs);
      setPhase('planning');
      setLastResult(null);
      setStrategicAgent({ status: 'idle', message: 'En espera' });
      setLogisticsAgent({ status: 'idle', message: 'En espera' });
    } catch (e: any) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }, [scenarioType, saveName]);

  const handleProcessMonth = useCallback(async () => {
    if (!gameState) return;
    setPhase('resolving');
    setLoading(true);
    setError(null);
    setStrategicAgent({ status: 'processing', message: '🧠 Analizando escenario...' });
    setLogisticsAgent({ status: 'processing', message: '⚙️ Evaluando logística...' });

    try {
      const result = await api.processMonth(gameState);
      setLastResult(result);

      // Actualizar Solaris en el estado local
      if (gameState.activeScenario) {
        gameState.activeScenario.currentSolaris = result.currentSolaris;
        gameState.activeScenario.currentMonth   = result.month + 1;
        gameState.activeScenario.eventLog.push(...result.events);
      }
      setGameState({ ...gameState });
      setStrategicAgent({ status: 'done', message: '✅ Análisis completado' });
      setLogisticsAgent({ status: 'done', message: '✅ Logística aplicada' });
      setPhase('results');
    } catch (e: any) {
      setError(e.message);
      setStrategicAgent({ status: 'error', message: '❌ Error de conexión' });
      setLogisticsAgent({ status: 'error', message: '❌ Error de conexión' });
      setPhase('planning');
    } finally {
      setLoading(false);
    }
  }, [gameState]);

  const handleGetAdvice = useCallback(async () => {
    if (!gameState) return;
    setStrategicAgent({ status: 'processing', message: '🧠 Consultando al asesor...' });
    try {
      const result = await api.getStrategicAdvice(
        gameState,
        'Analiza el estado del zoológico y proporciona recomendaciones estratégicas urgentes.'
      );
      setAdvice(result.advice || result.status || JSON.stringify(result));
      setStrategicAgent({ status: 'done', message: '✅ Consejo disponible' });
    } catch {
      setStrategicAgent({ status: 'error', message: '❌ CrewAI no disponible' });
      setAdvice('El Agente Estratégico no pudo conectarse a CrewAI. Verifica la configuración de BearerToken.');
    }
  }, [gameState]);

  const continuePlanning = () => {
    setPhase('planning');
    setStrategicAgent({ status: 'idle', message: 'En espera' });
    setLogisticsAgent({ status: 'idle', message: 'En espera' });
  };

  // ── RENDER ──────────────────────────────────────────────────────────────────

  const scenario = gameState?.activeScenario;

  return (
    <div className="min-h-screen scanlines relative overflow-hidden">
      {/* Desert background gradient */}
      <div className="fixed inset-0 bg-gradient-to-br from-sand-950 via-sand-900 to-[#0d0a06] -z-10" />
      <div className="fixed inset-0 bg-[radial-gradient(ellipse_at_50%_0%,rgba(196,150,58,0.08)_0%,transparent_60%)] -z-10" />

      {/* Sand Particle Effect Overlay */}
      <div className="fixed inset-0 pointer-events-none -z-5 opacity-30">
        {[...Array(20)].map((_, i) => (
          <div
            key={i}
            className="absolute rounded-full bg-gold/40"
            style={{
              width: Math.random() * 3 + 1 + 'px',
              height: Math.random() * 3 + 1 + 'px',
              left: Math.random() * 100 + '%',
              top: Math.random() * 100 + '%',
              filter: 'blur(1px)',
              animation: `shimmer ${Math.random() * 10 + 10}s linear infinite`,
              animationDelay: `-${Math.random() * 10}s`
            }}
          />
        ))}
      </div>

      {/* ── HEADER ──────────────────────────────────────────────── */}
      <header className="border-b border-sand-700/50 bg-sand-900/60 backdrop-blur-md sticky top-0 z-50">
        <div className="max-w-7xl mx-auto px-6 py-4 flex items-center justify-between">
          <div>
            <h1 className="font-cinzel text-xl tracking-[0.2em] shimmer-text">
              DUNE ARRAKIS DOMINION
            </h1>
            <p className="text-xs text-sand-500 tracking-widest mt-0.5">MULTI-AGENT ARCHITECTURE DEMO · 2026</p>
          </div>
          <div className="flex items-center gap-4">
            {/* Backend Status Indicator */}
            <div className="flex items-center gap-2 px-3 py-1 rounded bg-sand-950/50 border border-sand-800">
              <span className={`w-1.5 h-1.5 rounded-full ${isWakingUp ? 'bg-warn animate-pulse' : (health ? 'bg-success shadow-[0_0_8px_rgba(68,255,136,0.5)]' : 'bg-danger')}`} />
              <span className="text-[10px] tracking-widest text-sand-400 uppercase font-medium">
                {isWakingUp ? 'Waking API...' : (health ? 'System Ready' : 'API Offline')}
              </span>
            </div>
            {scenario && (
              <>
                <div className="text-right">
                  <p className="text-xs text-sand-500 tracking-wider">MES ACTUAL</p>
                  <p className="font-cinzel text-holo font-bold text-lg">{scenario.currentMonth}</p>
                </div>
                <div className="w-px h-8 bg-sand-700" />
                <div className="text-right">
                  <p className="text-xs text-sand-500 tracking-wider">SOLARIS</p>
                  <SolarisCounter value={scenario.currentSolaris} />
                </div>
                <div className="w-px h-8 bg-sand-700" />
                <div className="text-right">
                  <p className="text-xs text-sand-500 tracking-wider">ESCENARIO</p>
                  <p className="font-cinzel text-sand-200 text-sm">{scenario.name}</p>
                </div>
              </>
            )}
          </div>
        </div>
      </header>

      <main className="max-w-7xl mx-auto px-6 py-8">

        {/* ── ERROR BANNER ──────────────────────────────────────── */}
        {error && (
          <div className="mb-6 card border-danger/40 p-4 flex items-start gap-3 animate-fade-in-up">
            <span className="text-danger text-lg">⚠</span>
            <div className="flex-1">
              <p className="text-danger font-medium">Error</p>
              <p className="text-sand-300 text-sm mt-1">{error}</p>
            </div>
            <button onClick={clearError} className="text-sand-500 hover:text-sand-200 text-lg">×</button>
          </div>
        )}

        {/* ══════════════════════════════════════════════════════════
            MAIN MENU
        ══════════════════════════════════════════════════════════ */}
        {phase === 'menu' && (
          <div className="max-w-xl mx-auto mt-16 animate-fade-in-up">
            <div className="card p-8 text-center">
              <div className="text-6xl mb-4">🏜️</div>
              <h2 className="font-cinzel text-2xl tracking-widest text-sand-100 mb-2">
                NUEVA PARTIDA
              </h2>
              <p className="text-sand-400 text-sm mb-8 leading-relaxed">
                Gestiona tu zoológico en Arrakis.<br/>
                Los Agentes IA tomarán decisiones en paralelo cada mes.
              </p>

              <div className="space-y-4 text-left mb-8">
                <div>
                  <label className="text-xs text-sand-400 tracking-widest uppercase block mb-1">Nombre de Partida</label>
                  <input
                    value={saveName}
                    onChange={e => setSaveName(e.target.value)}
                    className="w-full bg-sand-800 border border-sand-600 rounded px-3 py-2 text-sand-100 font-rajdhani focus:outline-none focus:border-gold transition-colors"
                    placeholder="Arrakeen-2026"
                  />
                </div>
                <div>
                  <label className="text-xs text-sand-400 tracking-widest uppercase block mb-1">Escenario</label>
                  <select
                    value={scenarioType}
                    onChange={e => setScenarioType(Number(e.target.value))}
                    className="w-full bg-sand-800 border border-sand-600 rounded px-3 py-2 text-sand-100 font-rajdhani focus:outline-none focus:border-gold transition-colors"
                  >
                    <option value={0}>Arrakeen — Capital del Imperio (50,000 ◈)</option>
                    <option value={1}>Giedi Prime — Mundo Harkonnen (75,000 ◈)</option>
                    <option value={2}>Caladan — Mundo Oceánico Atreides (40,000 ◈)</option>
                  </select>
                </div>
              </div>

              <button
                onClick={handleNewGame}
                disabled={loading}
                className="btn-primary w-full py-3"
              >
                {loading ? '⏳ Iniciando...' : '🚀 Comenzar Partida'}
              </button>
            </div>

            {/* Info de la arquitectura */}
            <div className="mt-6 grid grid-cols-2 gap-4">
              <div className="card p-4">
                <div className="text-holo text-2xl mb-2">🧠</div>
                <p className="font-cinzel text-xs tracking-wider text-sand-300">StrategicAdvisorAgent</p>
                <p className="text-sand-500 text-xs mt-1">Analiza el estado y genera consejos vía CrewAI</p>
              </div>
              <div className="card p-4">
                <div className="text-gold text-2xl mb-2">⚙️</div>
                <p className="font-cinzel text-xs tracking-wider text-sand-300">LogisticsAutomationAgent</p>
                <p className="text-sand-500 text-xs mt-1">Gestiona traslados y compras autónomamente</p>
              </div>
            </div>
          </div>
        )}

        {/* ══════════════════════════════════════════════════════════
            GAME VIEW (Planning + Results)
        ══════════════════════════════════════════════════════════ */}
        {(phase === 'planning' || phase === 'resolving' || phase === 'results') && scenario && (
          <div className="grid grid-cols-12 gap-6">

            {/* ── LEFT: Enclaves ──────────────────────────────────── */}
            <div className="col-span-12 lg:col-span-7">
              <div className="flex items-center justify-between mb-4">
                <h2 className="font-cinzel text-sm tracking-widest text-sand-400 uppercase">
                  Enclaves del Zoológico
                </h2>
                <span className="text-xs text-sand-500">{scenario.enclaves.length} zonas activas</span>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {scenario.enclaves.map(enclave => (
                  <EnclaveCard key={enclave.id} enclave={enclave} />
                ))}
              </div>
            </div>

            {/* ── RIGHT: Control Panel ─────────────────────────────── */}
            <div className="col-span-12 lg:col-span-5 space-y-5">

              {/* Agentes Monitor */}
              <div>
                <h2 className="font-cinzel text-xs tracking-widest text-sand-400 uppercase mb-3">
                  Monitor de Agentes IA
                </h2>
                <div className="space-y-3">
                  <AgentCard name="StrategicAdvisorAgent"    icon="🧠" state={strategicAgent} />
                  <AgentCard name="LogisticsAutomationAgent" icon="⚙️" state={logisticsAgent} />
                </div>
              </div>

              {/* Acciones */}
              <div className="card p-5">
                <h2 className="font-cinzel text-xs tracking-widest text-sand-400 uppercase mb-4">Acciones</h2>
                <div className="space-y-3">
                  {phase === 'planning' && (
                    <>
                      <button
                        onClick={handleProcessMonth}
                        disabled={loading}
                        className="btn-primary w-full py-3"
                      >
                        ▶ Procesar Mes {scenario.currentMonth}
                      </button>
                      <button
                        onClick={handleGetAdvice}
                        disabled={loading}
                        className="btn-holo w-full"
                      >
                        🧠 Pedir Consejo Estratégico
                      </button>
                      <button
                        onClick={() => { setGameState(null); setPhase('menu'); }}
                        className="btn-danger w-full text-xs"
                      >
                        Abandonar Partida
                      </button>
                    </>
                  )}
                  {phase === 'resolving' && (
                    <div className="text-center py-4">
                      <div className="text-4xl mb-3 animate-pulse">⏳</div>
                      <p className="font-cinzel text-holo tracking-wider">Agentes procesando...</p>
                      <p className="text-sand-500 text-xs mt-2">Los dos agentes se ejecutan en paralelo</p>
                    </div>
                  )}
                  {phase === 'results' && (
                    <button onClick={continuePlanning} className="btn-primary w-full py-3">
                      ▶ Continuar — Mes {scenario.currentMonth}
                    </button>
                  )}
                </div>
              </div>

              {/* Consejo del Asesor */}
              {advice && (
                <div className="card-holo p-5 animate-fade-in-up">
                  <div className="flex items-center gap-2 mb-3">
                    <span className="text-xl">🧠</span>
                    <h3 className="font-cinzel text-xs tracking-widest text-holo uppercase">Consejo Estratégico</h3>
                  </div>
                  <p className="text-sand-200 text-sm leading-relaxed">{advice}</p>
                  <button onClick={() => setAdvice(null)} className="text-sand-500 text-xs mt-3 hover:text-sand-300 transition-colors">
                    Cerrar
                  </button>
                </div>
              )}

              {/* Estadísticas del mes */}
              {lastResult && phase === 'results' && (
                <div className="card p-5 animate-fade-in-up border-gold/20">
                  <h3 className="font-cinzel text-xs tracking-widest text-gold uppercase mb-3">
                    Resultado Mes {lastResult.month}
                  </h3>
                  <div className="flex items-center justify-between text-sm mb-2">
                    <span className="text-sand-400">Solaris actuales</span>
                    <SolarisCounter value={lastResult.currentSolaris} />
                  </div>
                  <div className="flex items-center justify-between text-sm">
                    <span className="text-sand-400">Eventos generados</span>
                    <span className="font-bold text-sand-200">{lastResult.events.length}</span>
                  </div>
                </div>
              )}
            </div>

            {/* ── BOTTOM: Event Log ─────────────────────────────────── */}
            <div className="col-span-12">
              <div className="card p-5">
                <h2 className="font-cinzel text-xs tracking-widest text-sand-400 uppercase mb-4">
                  Registro de Eventos
                </h2>
                <EventFeed events={scenario.eventLog} />
              </div>
            </div>
          </div>
        )}
      </main>

      {/* Footer */}
      <footer className="border-t border-sand-800 mt-12 py-6 text-center">
        <p className="text-sand-600 text-xs tracking-widest">
          DUNE ARRAKIS DOMINION · MULTI-AGENT ARCHITECTURE DEMO · 2026 ·{' '}
          <span className="text-sand-500">Railway + Vercel + MediatR + CrewAI</span>
        </p>
      </footer>
    </div>
  );
}
