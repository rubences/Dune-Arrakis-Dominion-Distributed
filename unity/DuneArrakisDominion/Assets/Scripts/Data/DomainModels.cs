// ============================================================
// DuneArrakis Dominion - Domain Models (Unity Mirror of Backend DTOs)
// Reflejo exacto de las entidades del dominio .NET para deserialización JSON.
// ============================================================

using System;
using System.Collections.Generic;

namespace DuneArrakis.Unity.Data
{
    // ── Enums ──────────────────────────────────────────────────────────────────
    public enum ScenarioType { Arrakeen, GiediPrime, Caladan }
    public enum EnclaveType  { Aclimatacion, Exhibicion }
    public enum CreatureType { GusanoDeArenaJuvenil, TigreLaza, MuadDib, HalconDelDesierto, TruchaDeArena }
    public enum FacilityType { ZonaDeHabitat, PuestoMedico, TorreDeMando, LaboratorioDeClonacion, CentroDeEntrenamiento }
    public enum HabitatType  { Desertico, Arido, Templado, Oceanico }
    public enum DietType     { Carnivoro, Granivoro, Omnivoro }

    // ── Core Entities ──────────────────────────────────────────────────────────
    [Serializable]
    public class GameState
    {
        public string id;
        public string saveName;
        public Scenario activeScenario;
        public string createdAt;
    }

    [Serializable]
    public class Scenario
    {
        public string id;
        public string name;
        public string description;
        public int type;                   // mapped to ScenarioType
        public decimal currentSolaris;
        public int storedFoodUnits;
        public int currentMonth;
        public List<Enclave> enclaves = new();
        public List<SimulationEvent> eventLog = new();
    }

    [Serializable]
    public class Enclave
    {
        public string id;
        public string name;
        public int type;                   // mapped to EnclaveType
        public double hectareas;
        public int maxCreatureCapacity;
        public int nivelAdquisitivo;
        public int currentVisitors;
        public int totalVisitorsThisMonth;
        public List<Creature> creatures  = new();
        public List<Facility> facilities = new();
    }

    [Serializable]
    public class Creature
    {
        public string id;
        public string name;
        public string commonName;
        public int type;                   // mapped to CreatureType
        public int health;
        public int age;
        public int ageInMonths;
        public bool isAlive;
        public int foodRequiredPerMonth;
        public int foodConsumedThisMonth;
        public decimal acquisitionCost;
        public decimal monthlyFoodCost;
        public int habitat;
        public int diet;
        public string enclaveId;
    }

    [Serializable]
    public class Facility
    {
        public string id;
        public int type;                   // mapped to FacilityType
        public string name;
        public bool isOperational;
        public decimal maintenanceCostPerMonth;
    }

    [Serializable]
    public class SimulationEvent
    {
        public int month;
        public string eventType;
        public string description;
        public decimal solarisChange;
        public string creatureId;
        public string enclaveId;
    }

    // ── API Payloads ───────────────────────────────────────────────────────────
    [Serializable]
    public class SimulationResult
    {
        public int month;
        public List<SimulationEvent> events = new();
        public decimal currentSolaris;
    }

    [Serializable]
    public class PurchaseCreatureRequest
    {
        public string enclaveId;
        public int creatureType;           // matches backend enum int value
    }

    [Serializable]
    public class FeedCreatureRequest
    {
        public string creatureId;
        public int foodAmount;
    }

    [Serializable]
    public class TransferCreatureRequest
    {
        public string sourceEnclaveId;
        public string targetEnclaveId;
        public string creatureId;
    }

    [Serializable]
    public class BuildFacilityRequest
    {
        public string enclaveId;
        public int facilityType;
    }

    [Serializable]
    public class ApiError
    {
        public string title;
        public string detail;
        public int status;
    }

    // ── Catalog (local, for UI display) ───────────────────────────────────────
    public static class CreatureCatalog
    {
        public static readonly CreatureInfo[] All = new[]
        {
            new CreatureInfo(CreatureType.GusanoDeArenaJuvenil, "Gusano de Arena Juvenil", "Shai-Hulud joven", 20_000m, 800m, "🪱"),
            new CreatureInfo(CreatureType.TigreLaza,            "Tigre Laza",              "Bestia cazadora",  8_000m,  300m, "🐯"),
            new CreatureInfo(CreatureType.MuadDib,              "Muad'Dib",                "Ratón del desierto",500m,  20m,  "🐭"),
            new CreatureInfo(CreatureType.HalconDelDesierto,    "Halcón del Desierto",     "Ave rapaz",        3_000m,  120m, "🦅"),
            new CreatureInfo(CreatureType.TruchaDeArena,        "Trucha de Arena",         "Precursor del gusano", 5_000m, 200m, "🐟"),
        };
    }

    [Serializable]
    public class CreatureInfo
    {
        public CreatureType Type;
        public string Name;
        public string CommonName;
        public decimal AcquisitionCost;
        public decimal MonthlyFoodCost;
        public string Emoji;

        public CreatureInfo(CreatureType t, string n, string cn, decimal ac, decimal mfc, string emoji)
        {
            Type = t; Name = n; CommonName = cn;
            AcquisitionCost = ac; MonthlyFoodCost = mfc; Emoji = emoji;
        }
    }
}
