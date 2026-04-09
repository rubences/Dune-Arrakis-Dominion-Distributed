// Capa de comunicación con el SimulationService en Railway
// La URL base viene de la variable de entorno NEXT_PUBLIC_API_URL

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';

export async function newGame(scenarioType: number = 0, saveName: string = 'Partida') {
  const res = await fetch(
    `${API_URL}/api/simulation/new-game?scenarioType=${scenarioType}&saveName=${encodeURIComponent(saveName)}`,
    { method: 'POST', headers: { 'Content-Type': 'application/json' } }
  );
  if (!res.ok) throw new Error(await res.text());
  return res.json();
}

export async function processMonth(gameState: object) {
  const res = await fetch(`${API_URL}/api/simulation/process-month`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(gameState),
  });
  if (!res.ok) throw new Error(await res.text());
  return res.json();
}

export async function purchaseCreature(gameState: object, enclaveId: string, creatureType: number) {
  const res = await fetch(`${API_URL}/api/simulation/purchase-creature`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ gameState, enclaveId, creatureType }),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }));
    throw new Error(err.error || 'Error al comprar criatura');
  }
  return res.json();
}

export async function feedCreature(gameState: object, creatureId: string, foodAmount: number) {
  const res = await fetch(`${API_URL}/api/simulation/feed-creature`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ gameState, creatureId, foodAmount }),
  });
  if (!res.ok) throw new Error(await res.text());
  return res.json();
}

export async function transferCreature(
  gameState: object,
  sourceEnclaveId: string,
  targetEnclaveId: string,
  creatureId: string
) {
  const res = await fetch(`${API_URL}/api/simulation/transfer-creature`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ gameState, sourceEnclaveId, targetEnclaveId, creatureId }),
  });
  if (!res.ok) throw new Error(await res.text());
  return res.json();
}

export async function getStrategicAdvice(gameState: object, prompt: string) {
  const res = await fetch(`${API_URL}/api/simulation/ai/strategic-advice`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ gameState, prompt, waitForCompletion: false }),
  });
  if (!res.ok) throw new Error(await res.text());
  return res.json();
}

export async function checkHealth() {
  try {
    const res = await fetch(`${API_URL}/api/simulation/health`, { cache: 'no-store' });
    return res.ok ? res.json() : null;
  } catch {
    return null;
  }
}
