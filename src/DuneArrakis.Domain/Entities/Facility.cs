using DuneArrakis.Domain.Enums;

namespace DuneArrakis.Domain.Entities;

public class Facility
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public FacilityType Type { get; set; }
    public decimal ConstructionCost { get; set; }
    public decimal MaintenanceCostPerMonth { get; set; }
    public int Level { get; set; } = 1;
    public bool IsOperational { get; set; } = true;

    public static readonly IReadOnlyDictionary<FacilityType, (string Name, decimal Cost, decimal Maintenance)> Catalog =
        new Dictionary<FacilityType, (string, decimal, decimal)>
        {
            [FacilityType.ZonaDeHabitat]         = ("Zona de Hábitat",          5_000m, 200m),
            [FacilityType.CentroMedico]           = ("Centro Médico",            8_000m, 350m),
            [FacilityType.AlmacenDeAlimentos]     = ("Almacén de Alimentos",     3_000m, 100m),
            [FacilityType.LaboratorioDeClonacion] = ("Laboratorio de Clonación", 15_000m, 600m),
            [FacilityType.PuestoDeObservacion]    = ("Puesto de Observación",    2_000m,  80m)
        };

    public static Facility Create(FacilityType type)
    {
        var (name, cost, maintenance) = Catalog[type];
        return new Facility
        {
            Type = type,
            Name = name,
            ConstructionCost = cost,
            MaintenanceCostPerMonth = maintenance
        };
    }
}
