using DuneArrakis.Domain.Enums;

namespace DuneArrakis.Domain.Entities;

public class Creature
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public CreatureType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
    public HabitatType Habitat { get; set; }
    public DietType Diet { get; set; }
    public int Health { get; set; } = 100;
    public int Age { get; set; } = 0;
    public int AgeInMonths { get; set; } = 0;
    public decimal AcquisitionCost { get; set; }
    public decimal MonthlyFoodCost { get; set; }
    public int FoodRequiredPerMonth { get; set; }
    public int FoodConsumedThisMonth { get; set; }
    public bool IsAlive { get; set; } = true;
    public Guid EnclaveId { get; set; }

    public static readonly IReadOnlyDictionary<CreatureType, CreatureTemplate> Templates =
        new Dictionary<CreatureType, CreatureTemplate>
        {
            [CreatureType.GusanoDeArenaJuvenil] = new(
                "Gusano de Arena Juvenil", "Shai-Hulud joven",
                HabitatType.Desertico, DietType.Omnivoro,
                20_000m, 800m, 100),

            [CreatureType.TigreLaza] = new(
                "Tigre Laza", "Bestia cazadora harkonnen",
                HabitatType.Arido, DietType.Carnivoro,
                8_000m, 300m, 50),

            [CreatureType.MuadDib] = new(
                "Muad'Dib", "Ratón del desierto",
                HabitatType.Desertico, DietType.Granivoro,
                500m, 20m, 10),

            [CreatureType.HalconDelDesierto] = new(
                "Halcón del Desierto", "Ave rapaz de Arrakis",
                HabitatType.Desertico, DietType.Carnivoro,
                3_000m, 120m, 25),

            [CreatureType.TruchaDeArena] = new(
                "Trucha de Arena", "Precursor del gusano de arena",
                HabitatType.Desertico, DietType.Omnivoro,
                5_000m, 200m, 40)
        };

    public static Creature Create(CreatureType type)
    {
        var template = Templates[type];
        return new Creature
        {
            Type = type,
            Name = template.Name,
            CommonName = template.CommonName,
            Habitat = template.Habitat,
            Diet = template.Diet,
            AcquisitionCost = template.AcquisitionCost,
            MonthlyFoodCost = template.MonthlyFoodCost,
            FoodRequiredPerMonth = template.FoodRequiredPerMonth,
            Health = 100,
            Age = 0,
            AgeInMonths = 0,
            IsAlive = true
        };
    }
}

public record CreatureTemplate(
    string Name,
    string CommonName,
    HabitatType Habitat,
    DietType Diet,
    decimal AcquisitionCost,
    decimal MonthlyFoodCost,
    int FoodRequiredPerMonth);
