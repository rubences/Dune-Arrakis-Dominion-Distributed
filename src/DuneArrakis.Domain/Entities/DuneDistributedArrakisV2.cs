using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dune.Models;

/// <summary>
/// Clase base POCO para súbditos de Arrakis, autocontenida y serializable.
/// </summary>
public abstract class Subdito
{
    protected Subdito(string nombre, int energiaInicial, int experienciaInicial)
    {
        Id = Guid.NewGuid();
        Nombre = nombre;
        Energia = energiaInicial;
        Experiencia = experienciaInicial;
    }

    public Guid Id { get; }
    public string Nombre { get; }
    public int Energia { get; private set; }
    public int Experiencia { get; private set; }
    public abstract string Rol { get; }

    /// <summary>
    /// Coste salarial por ciclo en melange según regla de supervivencia.
    /// </summary>
    public decimal SalarioMelange => Rol switch
    {
        "Guerrero" => 4m,
        "Recolector" => 3m,
        _ => 2m
    };

    /// <summary>
    /// Aplica recuperación de energía (+2) y experiencia (+5) del ciclo.
    /// </summary>
    public void AvanzarCiclo()
    {
        Energia += 2;
        Experiencia += 5;
    }

    /// <summary>
    /// Aplica coste de viaje entre territorios (-20 energía).
    /// </summary>
    public bool IntentarTraslado()
    {
        if (Energia < 20) return false;
        Energia -= 20;
        return true;
    }

    public void ReducirEnergia(int valor) => Energia = Math.Max(0, Energia - Math.Max(0, valor));
}

public sealed class Guerrero : Subdito { public Guerrero(string nombre) : base(nombre, 100, 0) { } public override string Rol => "Guerrero"; }
public sealed class Explorador : Subdito { public Explorador(string nombre) : base(nombre, 90, 0) { } public override string Rol => "Explorador"; }
public sealed class Recolector : Subdito { public Recolector(string nombre) : base(nombre, 80, 0) { } public override string Rol => "Recolector"; }
public sealed class Sabio : Subdito { public Sabio(string nombre) : base(nombre, 70, 0) { } public override string Rol => "Sabio"; }

/// <summary>
/// Clase base POCO para territorios de las casas.
/// </summary>
public abstract class Territorio
{
    protected Territorio(string nombre, string casa, decimal extension, decimal capacidadInicial)
    {
        Id = Guid.NewGuid();
        Nombre = nombre;
        Casa = casa;
        Extension = extension;
        CapacidadInicial = capacidadInicial;
        Melange = 500m;
        Energia = 400m;
    }

    private readonly List<Subdito> _subditos = [];

    public Guid Id { get; }
    public string Nombre { get; }
    public string Casa { get; }
    public decimal Extension { get; }
    public decimal CapacidadInicial { get; }
    public decimal Melange { get; private set; }
    public decimal Energia { get; private set; }
    public decimal SuperficieConstruida { get; private set; } = 1m;
    public IReadOnlyCollection<Subdito> Subditos => _subditos.AsReadOnly();

    /// <summary>
    /// Añade un súbdito al territorio.
    /// </summary>
    public void RecibirSubdito(Subdito subdito) => _subditos.Add(subdito);

    /// <summary>
    /// Retira súbdito por id.
    /// </summary>
    public Subdito? ExtraerSubdito(Guid subditoId)
    {
        var s = _subditos.FirstOrDefault(x => x.Id == subditoId);
        if (s is null) return null;
        _subditos.Remove(s);
        return s;
    }

    /// <summary>
    /// Paga salarios y elimina por inanición los súbditos que no se pueden sostener.
    /// </summary>
    public int PagarSalariosYSupervivencia()
    {
        var muertos = 0;
        foreach (var s in _subditos.ToList())
        {
            if (Melange >= s.SalarioMelange)
            {
                Melange -= s.SalarioMelange;
                continue;
            }

            _subditos.Remove(s);
            muertos++;
        }

        return muertos;
    }

    /// <summary>
    /// Cosecha de especia basada en recolectores y experiencia media.
    /// </summary>
    public decimal CosecharMelange(decimal alpha)
    {
        var rec = _subditos.OfType<Recolector>().ToList();
        if (rec.Count == 0) return 0;
        var expMedia = rec.Average(r => r.Experiencia);
        var cantidad = alpha * rec.Count * (1m + (decimal)expMedia / 100m);
        Melange += cantidad;
        return cantidad;
    }

    /// <summary>
    /// Calcula energía requerida por construcción: α*β*(extensión/superficie).
    /// </summary>
    public decimal CalcularEnergiaConstruccion(decimal alpha, decimal beta, decimal superficieAConstruir)
        => alpha * beta * (Extension / Math.Max(1m, superficieAConstruir));

    /// <summary>
    /// Intenta construir consumiendo energía según la fórmula del enunciado.
    /// </summary>
    public bool IntentarConstruccion(decimal alpha, decimal beta, decimal superficieAConstruir)
    {
        var requerida = CalcularEnergiaConstruccion(alpha, beta, superficieAConstruir);
        if (Energia < requerida) return false;
        Energia -= requerida;
        SuperficieConstruida += superficieAConstruir;
        return true;
    }

    public void AjustarEnergia(decimal delta) => Energia += delta;

    public string ToJson() => JsonSerializer.Serialize(this);
}

public sealed class Caladan : Territorio { public Caladan() : base("Caladan", "Atreides", 1200m, 300m) { } }
public sealed class GiediPrime : Territorio { public GiediPrime() : base("Giedi Prime", "Harkonnen", 980m, 260m) { } }
public sealed class SalusaSecundus : Territorio { public SalusaSecundus() : base("Salusa Secundus", "Corrino", 1100m, 280m) { } }

public sealed record CrewAiOrder(string Action, Guid FromTerritoryId, Guid ToTerritoryId, string UnitRole, int Amount, decimal? Alpha = null, decimal? Beta = null, decimal? Surface = null);
public sealed record CrewAiDecisionEnvelope(string DecisionId, IReadOnlyCollection<CrewAiOrder> Orders);

public sealed class ArrakisStateDto
{
    [JsonPropertyName("cycle")]
    public int Cycle { get; init; }

    [JsonPropertyName("territories")]
    public IReadOnlyCollection<TerritoryDto> Territories { get; init; } = [];

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public sealed class TerritoryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string House { get; init; } = string.Empty;
    public decimal Extension { get; init; }
    public decimal Melange { get; init; }
    public decimal Energy { get; init; }
    public decimal BuiltSurface { get; init; }
    public IReadOnlyCollection<UnitDto> Units { get; init; } = [];
}

public sealed class UnitDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public int Energy { get; init; }
    public int Experience { get; init; }
}
