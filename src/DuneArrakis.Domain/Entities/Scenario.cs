using DuneArrakis.Domain.Enums;

namespace DuneArrakis.Domain.Entities;

public class Scenario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ScenarioType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal InitialSolaris { get; set; }
    public decimal CurrentSolaris { get; set; }
    public int StoredFoodUnits { get; set; }
    public List<Enclave> Enclaves { get; set; } = [];
    public List<SimulationEvent> EventLog { get; set; } = [];
    public int CurrentMonth { get; set; } = 1;

    public static Scenario CreateArrakeen() => new()
    {
        Type = ScenarioType.Arrakeen,
        Name = "Arrakeen",
        Description = "La capital de Arrakis, corazón del Imperio en el planeta de la especia.",
        InitialSolaris = 50_000m,
        CurrentSolaris = 50_000m,
        StoredFoodUnits = 200
    };

    public static Scenario CreateGiediPrime() => new()
    {
        Type = ScenarioType.GiediPrime,
        Name = "Giedi Prime",
        Description = "Mundo industrial de la Casa Harkonnen, dominado por fábricas y contaminación.",
        InitialSolaris = 75_000m,
        CurrentSolaris = 75_000m,
        StoredFoodUnits = 250
    };

    public static Scenario CreateCaladan() => new()
    {
        Type = ScenarioType.Caladan,
        Name = "Caladan",
        Description = "Mundo oceánico de la Casa Atreides, con vastos océanos y naturaleza exuberante.",
        InitialSolaris = 40_000m,
        CurrentSolaris = 40_000m,
        StoredFoodUnits = 180
    };
    public void DeductSolaris(decimal amount)
    {
        if (CurrentSolaris < amount)
            throw new Exceptions.InsufficientFundsException(amount, CurrentSolaris);
        CurrentSolaris -= amount;
    }

    public void AddEvent(SimulationEvent simulationEvent)
    {
        EventLog.Add(simulationEvent);
    }

    public Creature PurchaseCreature(Guid enclaveId, CreatureType creatureType)
    {
        var enclave = Enclaves.FirstOrDefault(e => e.Id == enclaveId) 
            ?? throw new Exceptions.EntityNotFoundException("enclave", enclaveId);

        if (!Creature.Templates.TryGetValue(creatureType, out var template))
            throw new Exceptions.InvalidEntityStateException($"Tipo de criatura no válido: {creatureType}.");

        DeductSolaris(template.AcquisitionCost);
        
        var creature = Creature.Create(creatureType);
        enclave.AddCreature(creature);

        AddEvent(new SimulationEvent
        {
            Month = CurrentMonth,
            EventType = "Compra",
            Description = $"Adquirido {creature.Name} para {enclave.Name}. Coste: {template.AcquisitionCost:N0} Solaris.",
            SolarisChange = -template.AcquisitionCost,
            CreatureId = creature.Id,
            EnclaveId = enclave.Id
        });

        return creature;
    }

    public Facility BuildFacility(Guid enclaveId, FacilityType facilityType)
    {
        var enclave = Enclaves.FirstOrDefault(e => e.Id == enclaveId) 
            ?? throw new Exceptions.EntityNotFoundException("enclave", enclaveId);

        if (!Facility.Catalog.TryGetValue(facilityType, out var catalogEntry))
            throw new Exceptions.InvalidEntityStateException($"Tipo de instalación no válido: {facilityType}.");

        var (name, cost, _) = catalogEntry;
        DeductSolaris(cost);

        var facility = Facility.Create(facilityType);
        enclave.Facilities.Add(facility);

        AddEvent(new SimulationEvent
        {
            Month = CurrentMonth,
            EventType = "Construccion",
            Description = $"Construida instalación '{name}' en {enclave.Name}. Coste: {cost:N0} Solaris.",
            SolarisChange = -cost,
            EnclaveId = enclave.Id
        });

        return facility;
    }

    public void FeedCreature(Guid creatureId, int foodAmount)
    {
        var enclave = Enclaves.FirstOrDefault(e => e.Creatures.Any(c => c.Id == creatureId))
            ?? throw new Exceptions.EntityNotFoundException("criatura", creatureId);

        var creature = enclave.Creatures.First(c => c.Id == creatureId);

        if (!creature.IsAlive)
            throw new Exceptions.InvalidCreatureStateException("No se puede alimentar una criatura que no está viva.");

        var foodCost = creature.MonthlyFoodCost * (decimal)foodAmount / creature.FoodRequiredPerMonth;
        DeductSolaris(foodCost);

        creature.FoodConsumedThisMonth = Math.Min(
            creature.FoodConsumedThisMonth + foodAmount,
            creature.FoodRequiredPerMonth);
    }

    public void TransferCreature(Guid sourceEnclaveId, Guid targetEnclaveId, Guid creatureId)
    {
        var sourceEnclave = Enclaves.FirstOrDefault(e => e.Id == sourceEnclaveId)
            ?? throw new Exceptions.EntityNotFoundException("enclave origen", sourceEnclaveId);
            
        var targetEnclave = Enclaves.FirstOrDefault(e => e.Id == targetEnclaveId)
            ?? throw new Exceptions.EntityNotFoundException("enclave destino", targetEnclaveId);

        var creature = sourceEnclave.Creatures.FirstOrDefault(c => c.Id == creatureId)
            ?? throw new Exceptions.EntityNotFoundException("criatura", creatureId);

        if (!creature.IsAlive)
            throw new Exceptions.InvalidCreatureStateException("No se puede trasladar una criatura que no está viva.");

        if (creature.Health < 75)
            throw new Exceptions.InvalidCreatureStateException($"No se puede trasladar '{creature.Name}'. La criatura necesita al menos 75 de salud (actual: {creature.Health}).");

        sourceEnclave.RemoveCreature(creature);
        targetEnclave.AddCreature(creature);

        AddEvent(new SimulationEvent
        {
            Month = CurrentMonth,
            EventType = "Traslado",
            Description = $"{creature.Name} trasladado de '{sourceEnclave.Name}' a '{targetEnclave.Name}'.",
            CreatureId = creature.Id,
            EnclaveId = targetEnclave.Id
        });
    }
}
