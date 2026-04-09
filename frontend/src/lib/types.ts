// Tipos TypeScript espejo de los DTOs del SimulationService

export type ScenarioType = 0 | 1 | 2; // Arrakeen | GiediPrime | Caladan
export type EnclaveType  = 0 | 1;     // Aclimatacion | Exhibicion
export type CreatureType = 0 | 1 | 2 | 3 | 4;
export type FacilityType = 0 | 1 | 2 | 3 | 4;

export interface GameState {
  id: string;
  saveName: string;
  createdAt: string;
  activeScenario: Scenario;
}

export interface Scenario {
  id: string;
  name: string;
  description: string;
  type: ScenarioType;
  currentSolaris: number;
  storedFoodUnits: number;
  currentMonth: number;
  enclaves: Enclave[];
  eventLog: SimulationEvent[];
}

export interface Enclave {
  id: string;
  name: string;
  type: EnclaveType;
  hectareas: number;
  maxCreatureCapacity: number;
  nivelAdquisitivo: number;
  currentVisitors: number;
  totalVisitorsThisMonth: number;
  creatures: Creature[];
  facilities: Facility[];
}

export interface Creature {
  id: string;
  name: string;
  commonName: string;
  type: CreatureType;
  health: number;
  age: number;
  ageInMonths: number;
  isAlive: boolean;
  foodRequiredPerMonth: number;
  foodConsumedThisMonth: number;
  acquisitionCost: number;
  monthlyFoodCost: number;
  enclaveId: string;
}

export interface Facility {
  id: string;
  type: FacilityType;
  name: string;
  isOperational: boolean;
  maintenanceCostPerMonth: number;
}

export interface SimulationEvent {
  month: number;
  eventType: string;
  description: string;
  solarisChange: number;
  creatureId?: string;
  enclaveId?: string;
}

export interface SimulationResult {
  month: number;
  events: SimulationEvent[];
  currentSolaris: number;
}

// Catálogo local de criaturas (info complementaria)
export const CREATURE_CATALOG = [
  { type: 0, name: 'Gusano de Arena Juvenil', emoji: '🪱', cost: 20000, diet: 'Omnívoro' },
  { type: 1, name: 'Tigre Laza',              emoji: '🐯', cost: 8000,  diet: 'Carnívoro' },
  { type: 2, name: "Muad'Dib",               emoji: '🐭', cost: 500,   diet: 'Granívoro' },
  { type: 3, name: 'Halcón del Desierto',     emoji: '🦅', cost: 3000,  diet: 'Carnívoro' },
  { type: 4, name: 'Trucha de Arena',         emoji: '🐟', cost: 5000,  diet: 'Omnívoro' },
] as const;

export const SCENARIO_NAMES: Record<number, string> = {
  0: 'Arrakeen',
  1: 'Giedi Prime',
  2: 'Caladan',
};

export const EVENT_ICONS: Record<string, string> = {
  Compra:       '🛒',
  Muerte:       '💀',
  Reproduccion: '🥚',
  Visitantes:   '👥',
  Gastos:       '💸',
  Salud:        '❤️',
  Traslado:     '📦',
  Construccion: '🏗',
};
