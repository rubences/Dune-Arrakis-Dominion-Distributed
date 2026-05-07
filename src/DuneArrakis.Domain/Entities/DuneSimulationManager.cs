using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Dune.Model;

namespace Dune.Network
{
    /// <summary>
    /// DTO de sincronización de estado para red.
    /// </summary>
    public sealed record GameStateSnapshot(
        int Ciclo,
        IReadOnlyCollection<TerritorioSnapshot> Territorios,
        IReadOnlyCollection<SimulationLogEntry> Historial);

    public sealed record TerritorioSnapshot(
        Guid Id,
        string Nombre,
        decimal Melange,
        decimal Energia,
        int NivelConstruccion,
        IReadOnlyCollection<SubditoSnapshot> Subditos);

    public sealed record SubditoSnapshot(Guid Id, string Nombre, string Tipo, int Energia, int Experiencia);

    public sealed record SimulationLogEntry(DateTimeOffset TimestampUtc, string Mensaje);
}

namespace Dune.Logic
{
    using Dune.Network;

    /// <summary>
    /// Contrato de estrategia para resolver conflictos según atacante/defensor.
    /// </summary>
    public interface IConflictStrategy
    {
        string Resolver(Subdito atacante, Territorio territorioObjetivo);
    }

    /// <summary>
    /// Estrategia para explorador contra territorio vacío con factor de estabilización.
    /// </summary>
    public sealed class ExploradorTerritorioVacioStrategy : IConflictStrategy
    {
        public string Resolver(Subdito atacante, Territorio territorioObjetivo)
        {
            var factorEstabilizacion = 1.0 + atacante.Experiencia / 200.0;
            territorioObjetivo.AjustarEnergia((decimal)factorEstabilizacion);
            return $"Explorador estabiliza {territorioObjetivo.Nombre} con factor {factorEstabilizacion:F2}.";
        }
    }

    /// <summary>
    /// Estrategia para combate Guerrero vs Guerrero con ataque/defensa derivados de energía y experiencia.
    /// </summary>
    public sealed class GuerreroVsGuerreroStrategy : IConflictStrategy
    {
        public string Resolver(Subdito atacante, Territorio territorioObjetivo)
        {
            var defensor = territorioObjetivo.Subditos.OfType<Guerrero>().OrderByDescending(g => g.Experiencia).FirstOrDefault();
            if (defensor is null)
            {
                return "No había defensor guerrero.";
            }

            var ataque = atacante.Energia * 0.7 + atacante.Experiencia * 1.3;
            var defensa = defensor.Energia * 0.8 + defensor.Experiencia * 1.2;
            return ataque > defensa
                ? $"Ataque exitoso ({ataque:F1} > {defensa:F1})."
                : $"Defensa exitosa ({defensa:F1} >= {ataque:F1}).";
        }
    }

    /// <summary>
    /// Motor principal del servidor autoritario basado en ciclos.
    /// </summary>
    public sealed class SimulationManager
    {
        private readonly Random _random;
        private readonly List<SimulationLogEntry> _historial = [];
        private readonly List<Casa> _casas = [];

        public SimulationManager(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _casas.Add(new CasaAtreides());
            _casas.Add(new CasaHarkonnen());
            _casas.Add(new CasaCorrino());
        }

        public int CicloActual { get; private set; }

        /// <summary>
        /// Ejecuta un tick del servidor aplicando salarios, experiencia, cosecha, eventos y nacimientos.
        /// </summary>
        public void Tick()
        {
            CicloActual++;
            foreach (var territorio in _casas.SelectMany(c => c.Territorios))
            {
                territorio.PagarSalarios();
                foreach (var subdito in territorio.Subditos)
                {
                    subdito.AvanzarCiclo();
                }

                var cosecha = territorio.CosecharMelange();
                if (cosecha > 0)
                {
                    _historial.Add(new SimulationLogEntry(DateTimeOffset.UtcNow, $"{territorio.Nombre} cosecha {cosecha:F2} de Melange."));
                }

                EvaluarPeligrosAmbientales(territorio, cosecha);
            }

            if (CicloActual % 5 == 0)
            {
                GenerarNacimientos();
            }
        }

        /// <summary>
        /// RPC servidor: traslada súbdito entre territorios si hay energía suficiente.
        /// </summary>
        public bool RpcTrasladarSubdito(Guid origenId, Guid destinoId, Guid subditoId)
        {
            var origen = ObtenerTerritorio(origenId);
            var destino = ObtenerTerritorio(destinoId);
            if (origen is null || destino is null) return false;

            var subdito = origen.ExtraerSubdito(subditoId);
            if (subdito is null) return false;

            if (!subdito.IntentarTraslado(20))
            {
                origen.RecibirSubdito(subdito);
                return false;
            }

            destino.RecibirSubdito(subdito);
            _historial.Add(new SimulationLogEntry(DateTimeOffset.UtcNow, $"Traslado confirmado de {subdito.Nombre} a {destino.Nombre}."));
            return true;
        }

        /// <summary>
        /// RPC servidor: incrementa la construcción del territorio validando Melange.
        /// </summary>
        public bool RpcIncrementarConstruccion(Guid territorioId, decimal costoMelange)
        {
            var territorio = ObtenerTerritorio(territorioId);
            if (territorio is null) return false;

            var ok = territorio.IntentarIncrementarConstruccion(costoMelange);
            if (ok)
            {
                _historial.Add(new SimulationLogEntry(DateTimeOffset.UtcNow, $"Construcción en {territorio.Nombre} aumentada."));
            }

            return ok;
        }

        /// <summary>
        /// Devuelve un snapshot ordenado por Melange para interfaz de Centro de Mandos.
        /// </summary>
        public GameStateSnapshot ObtenerSnapshot()
        {
            var territorios = _casas.SelectMany(c => c.Territorios)
                .OrderByDescending(t => t.Melange)
                .Select(t => new TerritorioSnapshot(
                    t.Id,
                    t.Nombre,
                    t.Melange,
                    t.EnergiaInfraestructura,
                    t.NivelConstruccion,
                    t.Subditos.Select(s => new SubditoSnapshot(s.Id, s.Nombre, s.Tipo.ToString(), s.Energia, s.Experiencia)).ToList()))
                .ToList();

            return new GameStateSnapshot(CicloActual, territorios, _historial.OrderBy(h => h.TimestampUtc).ToList());
        }

        /// <summary>
        /// Serializa el estado completo en JSON para persistencia distribuida.
        /// </summary>
        public string GuardarJson() => JsonSerializer.Serialize(ObtenerSnapshot(), new JsonSerializerOptions { WriteIndented = true });

        /// <summary>
        /// Reconstruye un snapshot de estado desde JSON para restauración de partida.
        /// </summary>
        public static GameStateSnapshot? CargarSnapshotDesdeJson(string json)
            => JsonSerializer.Deserialize<GameStateSnapshot>(json);

        private void GenerarNacimientos()
        {
            foreach (var territorio in _casas.SelectMany(c => c.Territorios))
            {
                var roll = _random.NextDouble();
                Subdito nuevo = roll switch
                {
                    < 0.30 => new Recolector($"Recolector-{Guid.NewGuid():N}"),
                    < 0.70 => new Guerrero($"Guerrero-{Guid.NewGuid():N}"),
                    < 0.90 => new Explorador($"Explorador-{Guid.NewGuid():N}"),
                    _ => new Sabio($"Sabio-{Guid.NewGuid():N}")
                };

                territorio.RecibirSubdito(nuevo);
                _historial.Add(new SimulationLogEntry(DateTimeOffset.UtcNow, $"Nacimiento: {nuevo.Tipo} en {territorio.Nombre}."));
            }
        }

        private void EvaluarPeligrosAmbientales(Territorio territorio, decimal cosecha)
        {
            var probGusanos = Math.Min(0.60, (double)cosecha / 100.0);
            if (_random.NextDouble() < probGusanos)
            {
                territorio.AjustarEnergia(-5);
                _historial.Add(new SimulationLogEntry(DateTimeOffset.UtcNow, $"Gusano de arena detectado en {territorio.Nombre}."));
            }

            if (_random.NextDouble() < 0.15)
            {
                territorio.AjustarEnergia(-8);
                _historial.Add(new SimulationLogEntry(DateTimeOffset.UtcNow, $"Tormenta de Coriolis impacta {territorio.Nombre}."));
            }
        }

        private Territorio? ObtenerTerritorio(Guid id) => _casas.SelectMany(c => c.Territorios).FirstOrDefault(t => t.Id == id);
    }
}
