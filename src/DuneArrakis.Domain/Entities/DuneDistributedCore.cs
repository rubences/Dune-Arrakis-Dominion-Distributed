using System;
using System.Collections.Generic;
using System.Linq;

namespace Dune.Model;

/// <summary>
/// Define los tipos de súbditos disponibles en Arrakis.
/// </summary>
public enum TipoSubdito
{
    Guerrero,
    Explorador,
    Recolector,
    Sabio
}

/// <summary>
/// Clase base de cualquier súbdito de una casa noble.
/// </summary>
public abstract class Subdito
{
    protected Subdito(string nombre, int energia, int experiencia, decimal salarioMelange)
    {
        Id = Guid.NewGuid();
        Nombre = nombre;
        Energia = energia;
        Experiencia = experiencia;
        SalarioMelange = salarioMelange;
    }

    public Guid Id { get; }
    public string Nombre { get; }
    public int Energia { get; private set; }
    public int Experiencia { get; private set; }
    public decimal SalarioMelange { get; }
    public abstract TipoSubdito Tipo { get; }

    /// <summary>
    /// Ejecuta mantenimiento de ciclo del súbdito (+2 energía, +5 experiencia).
    /// </summary>
    public void AvanzarCiclo()
    {
        Energia += 2;
        Experiencia += 5;
    }

    /// <summary>
    /// Consume energía para un desplazamiento entre territorios.
    /// </summary>
    public bool IntentarTraslado(int costoEnergia)
    {
        if (Energia < costoEnergia)
        {
            return false;
        }

        Energia -= costoEnergia;
        return true;
    }
}

public sealed class Guerrero : Subdito
{
    public Guerrero(string nombre) : base(nombre, 100, 0, 12m) { }
    public override TipoSubdito Tipo => TipoSubdito.Guerrero;
}

public sealed class Explorador : Subdito
{
    public Explorador(string nombre) : base(nombre, 90, 0, 10m) { }
    public override TipoSubdito Tipo => TipoSubdito.Explorador;
}

public sealed class Recolector : Subdito
{
    public Recolector(string nombre) : base(nombre, 80, 0, 8m) { }
    public override TipoSubdito Tipo => TipoSubdito.Recolector;
}

public sealed class Sabio : Subdito
{
    public Sabio(string nombre) : base(nombre, 70, 0, 14m) { }
    public override TipoSubdito Tipo => TipoSubdito.Sabio;
}

/// <summary>
/// Clase base para representar territorios gobernables.
/// </summary>
public abstract class Territorio
{
    protected Territorio(string nombre, string tipologia, int extension, decimal melangeInicial, decimal energiaInicial)
    {
        Id = Guid.NewGuid();
        Nombre = nombre;
        Tipologia = tipologia;
        Extension = extension;
        Melange = melangeInicial;
        EnergiaInfraestructura = energiaInicial;
    }

    private readonly List<Subdito> _subditos = [];

    public Guid Id { get; }
    public string Nombre { get; }
    public string Tipologia { get; }
    public int Extension { get; }
    public decimal Melange { get; private set; }
    public decimal EnergiaInfraestructura { get; private set; }
    public int NivelConstruccion { get; private set; }
    public IReadOnlyCollection<Subdito> Subditos => _subditos.AsReadOnly();

    /// <summary>
    /// Registra la entrada de un súbdito al territorio.
    /// </summary>
    public void RecibirSubdito(Subdito subdito) => _subditos.Add(subdito);

    /// <summary>
    /// Retira un súbdito del territorio por identificador.
    /// </summary>
    public Subdito? ExtraerSubdito(Guid subditoId)
    {
        var subdito = _subditos.FirstOrDefault(s => s.Id == subditoId);
        if (subdito is null)
        {
            return null;
        }

        _subditos.Remove(subdito);
        return subdito;
    }

    /// <summary>
    /// Incrementa el nivel de construcción validando Melange disponible.
    /// </summary>
    public bool IntentarIncrementarConstruccion(decimal costo)
    {
        if (Melange < costo)
        {
            return false;
        }

        Melange -= costo;
        NivelConstruccion++;
        return true;
    }

    /// <summary>
    /// Aplica salarios de súbditos sobre el stock de Melange del territorio.
    /// </summary>
    public void PagarSalarios()
    {
        var costo = _subditos.Sum(s => s.SalarioMelange);
        Melange = Math.Max(0, Melange - costo);
    }

    /// <summary>
    /// Ejecuta la cosecha de Melange basada en recolectores y experiencia media.
    /// </summary>
    public decimal CosecharMelange()
    {
        var recolectores = _subditos.OfType<Recolector>().ToList();
        if (recolectores.Count == 0)
        {
            return 0;
        }

        var expMedia = recolectores.Average(r => r.Experiencia);
        var cosecha = (decimal)(recolectores.Count * (1.0 + expMedia / 100.0));
        Melange += cosecha;
        return cosecha;
    }

    public void AjustarEnergia(decimal delta) => EnergiaInfraestructura += delta;
}

public sealed class Caladan : Territorio
{
    public Caladan() : base("Caladan", "Oceánico", 1200, 500m, 350m) { }
}

public sealed class GiediPrime : Territorio
{
    public GiediPrime() : base("Giedi Prime", "Industrial", 980, 450m, 420m) { }
}

public sealed class SalusaSecundus : Territorio
{
    public SalusaSecundus() : base("Salusa Secundus", "Prisión Imperial", 1100, 480m, 300m) { }
}

/// <summary>
/// Representa una casa noble y sus territorios administrados.
/// </summary>
public abstract class Casa
{
    protected Casa(string nombre, IEnumerable<Territorio> territorios)
    {
        Nombre = nombre;
        Territorios = territorios.ToList().AsReadOnly();
    }

    public string Nombre { get; }
    public IReadOnlyCollection<Territorio> Territorios { get; }
}

public sealed class CasaAtreides : Casa
{
    public CasaAtreides() : base("Casa Atreides", [new Caladan()]) { }
}

public sealed class CasaHarkonnen : Casa
{
    public CasaHarkonnen() : base("Casa Harkonnen", [new GiediPrime()]) { }
}

public sealed class CasaCorrino : Casa
{
    public CasaCorrino() : base("Casa Corrino", [new SalusaSecundus()]) { }
}
