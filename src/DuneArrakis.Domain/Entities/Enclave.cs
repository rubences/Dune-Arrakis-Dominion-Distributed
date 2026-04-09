using DuneArrakis.Domain.Enums;

namespace DuneArrakis.Domain.Entities;

public class Enclave
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public EnclaveType Type { get; set; }
    public double Hectareas { get; set; }
    public int StorageCapacity { get; set; }
    public int NivelAdquisitivo { get; set; }
    public int MaxCreatureCapacity { get; set; }
    public List<Creature> Creatures { get; set; } = [];
    public List<Facility> Facilities { get; set; } = [];
    public int CurrentVisitors { get; set; }
    public int TotalVisitorsThisMonth { get; set; }

    public static Enclave CreateAclimatacion(string name, double hectareas = 50) => new()
    {
        Name = name,
        Type = EnclaveType.Aclimatacion,
        Hectareas = hectareas,
        StorageCapacity = 1000,
        NivelAdquisitivo = 2,
        MaxCreatureCapacity = 5
    };

    public static Enclave CreateExhibicion(string name, double hectareas = 100) => new()
    {
        Name = name,
        Type = EnclaveType.Exhibicion,
        Hectareas = hectareas,
        StorageCapacity = 500,
        NivelAdquisitivo = 8,
        MaxCreatureCapacity = 20
    };
    public bool CanFitCreature()
    {
        return Creatures.Count(c => c.IsAlive) < MaxCreatureCapacity;
    }

    public void AddCreature(Creature creature)
    {
        if (!CanFitCreature())
            throw new Exceptions.InvalidEntityStateException($"El enclave '{Name}' está lleno.");
        
        creature.EnclaveId = Id;
        Creatures.Add(creature);
    }

    public void RemoveCreature(Creature creature)
    {
        Creatures.Remove(creature);
    }
}
