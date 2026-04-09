using DuneArrakis.AdminClient.Services;
using DuneArrakis.Domain.Entities;
using DuneArrakis.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Setup DI
var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddHttpClient<PersistenceServiceClient>(c =>
{
    c.BaseAddress = new Uri(args.Length > 0 ? args[0] : "http://localhost:5100");
    c.Timeout = TimeSpan.FromSeconds(10);
});
services.AddHttpClient<SimulationServiceClient>(c =>
{
    c.BaseAddress = new Uri(args.Length > 1 ? args[1] : "http://localhost:5200");
    c.Timeout = TimeSpan.FromSeconds(10);
});

var provider = services.BuildServiceProvider();
var persistenceClient = provider.GetRequiredService<PersistenceServiceClient>();
var simulationClient = provider.GetRequiredService<SimulationServiceClient>();

// Application state
GameState? currentGame = null;

await RunMainMenuAsync();

// ─────────────────────────────────────────────
// MAIN MENU
// ─────────────────────────────────────────────
async Task RunMainMenuAsync()
{
    PrintHeader("DUNE: ARRAKIS DOMINION DISTRIBUTED - CENTRO DE MANDO");

    while (true)
    {
        Console.WriteLine();
        PrintSeparator();
        Console.WriteLine("  MENÚ PRINCIPAL");
        PrintSeparator();
        if (currentGame != null)
        {
            var s = currentGame.ActiveScenario;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  Partida activa: [{s.Name}] Mes {s.CurrentMonth} | Solaris: {s.CurrentSolaris:N0}");
            Console.ResetColor();
        }
        Console.WriteLine("  [1] Nueva partida");
        Console.WriteLine("  [2] Cargar partida");
        Console.WriteLine("  [3] Guardar partida");
        Console.WriteLine("  [4] Centro de mando (estado global)");
        Console.WriteLine("  [5] Gestionar enclaves y criaturas");
        Console.WriteLine("  [6] Avanzar mes (simulación)");
        Console.WriteLine("  [7] Ver registro de eventos");
        Console.WriteLine("  [8] Consultar asesor estratégico CrewAI");
        Console.WriteLine("  [0] Salir");
        PrintSeparator();
        Console.Write("  Selección: ");

        var key = Console.ReadLine()?.Trim();
        switch (key)
        {
            case "1": await NewGameMenuAsync(); break;
            case "2": await LoadGameMenuAsync(); break;
            case "3": await SaveGameMenuAsync(); break;
            case "4": ShowCommandCenter(); break;
            case "5": await ManageEnclavesMenuAsync(); break;
            case "6": await AdvanceMonthAsync(); break;
            case "7": ShowEventLog(); break;
            case "8": await ConsultCrewAiAdvisorAsync(); break;
            case "0":
                PrintInfo("¡Hasta pronto, Administrator!");
                return;
            default:
                PrintError("Opción no válida.");
                break;
        }
    }
}

// ─────────────────────────────────────────────
// NEW GAME
// ─────────────────────────────────────────────
async Task NewGameMenuAsync()
{
    PrintHeader("NUEVA PARTIDA");
    Console.WriteLine("  Selecciona el escenario:");
    Console.WriteLine("  [1] Arrakeen       - 50,000 Solaris (planeta desértico)");
    Console.WriteLine("  [2] Giedi Prime    - 75,000 Solaris (planeta industrial)");
    Console.WriteLine("  [3] Caladan        - 40,000 Solaris (planeta oceánico)");
    Console.Write("  Opción: ");

    var choice = Console.ReadLine()?.Trim();
    Scenario scenario = choice switch
    {
        "1" => Scenario.CreateArrakeen(),
        "2" => Scenario.CreateGiediPrime(),
        "3" => Scenario.CreateCaladan(),
        _ => null!
    };

    if (scenario is null) { PrintError("Opción no válida."); return; }

    Console.Write("  Nombre de la partida: ");
    var saveName = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(saveName)) saveName = $"{scenario.Name}_{DateTime.Now:yyyyMMdd_HHmm}";

    // Add default enclaves
    var aclimatacion = Enclave.CreateAclimatacion("Zona de Aclimatación");
    var exhibicion = Enclave.CreateExhibicion("Zona de Exhibición");
    scenario.Enclaves.Add(aclimatacion);
    scenario.Enclaves.Add(exhibicion);

    currentGame = GameState.NewGame(scenario, saveName);

    PrintSuccess($"Partida '{saveName}' creada en {scenario.Name} con {scenario.CurrentSolaris:N0} Solaris.");
    PrintInfo($"Descripción: {scenario.Description}");

    await Task.CompletedTask;
}

// ─────────────────────────────────────────────
// LOAD GAME
// ─────────────────────────────────────────────
async Task LoadGameMenuAsync()
{
    PrintHeader("CARGAR PARTIDA");

    var saves = (await persistenceClient.ListSavesAsync()).ToList();
    if (!saves.Any())
    {
        var available = await persistenceClient.IsAvailableAsync();
        if (!available)
            PrintError("Servicio de persistencia no disponible. Inicie DuneArrakis.PersistenceService.");
        else
            PrintInfo("No hay partidas guardadas.");
        return;
    }

    Console.WriteLine("  Partidas disponibles:");
    for (int i = 0; i < saves.Count; i++)
        Console.WriteLine($"  [{i + 1}] {saves[i]}");

    Console.Write("  Número de partida (o nombre): ");
    var input = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(input)) return;

    string saveName;
    if (int.TryParse(input, out var idx) && idx >= 1 && idx <= saves.Count)
        saveName = saves[idx - 1];
    else
        saveName = input;

    var gameState = await persistenceClient.LoadGameAsync(saveName);
    if (gameState is null)
    {
        PrintError($"No se pudo cargar la partida '{saveName}'.");
        return;
    }

    currentGame = gameState;
    PrintSuccess($"Partida '{saveName}' cargada. Mes {currentGame.ActiveScenario.CurrentMonth} | Solaris: {currentGame.ActiveScenario.CurrentSolaris:N0}");
}

// ─────────────────────────────────────────────
// SAVE GAME
// ─────────────────────────────────────────────
async Task SaveGameMenuAsync()
{
    if (currentGame is null) { PrintError("No hay partida activa."); return; }

    var available = await persistenceClient.IsAvailableAsync();
    if (!available)
    {
        PrintError("Servicio de persistencia no disponible. Inicie DuneArrakis.PersistenceService.");
        return;
    }

    var success = await persistenceClient.SaveGameAsync(currentGame);
    if (success)
        PrintSuccess($"Partida '{currentGame.SaveName}' guardada correctamente.");
    else
        PrintError("Error al guardar la partida. Compruebe el Servicio de Persistencia.");
}

// ─────────────────────────────────────────────
// COMMAND CENTER
// ─────────────────────────────────────────────
void ShowCommandCenter()
{
    if (currentGame is null) { PrintError("No hay partida activa."); return; }

    var s = currentGame.ActiveScenario;
    PrintHeader($"CENTRO DE MANDO - {s.Name.ToUpper()} (MES {s.CurrentMonth})");

    // Funds
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"  💰 Fondos: {s.CurrentSolaris:N0} Solaris");
    Console.ResetColor();

    // Enclaves summary
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"  📊 ENCLAVES ({s.Enclaves.Count})");
    Console.ResetColor();

    foreach (var enclave in s.Enclaves)
    {
        var aliveCreatures = enclave.Creatures.Where(c => c.IsAlive).ToList();
        Console.WriteLine($"  ┌─ [{enclave.Type}] {enclave.Name}");
        Console.WriteLine($"  │   Hectáreas: {enclave.Hectareas:N0} | Capacidad: {aliveCreatures.Count}/{enclave.MaxCreatureCapacity}");
        Console.WriteLine($"  │   Instalaciones: {enclave.Facilities.Count} | Visitantes (último mes): {enclave.TotalVisitorsThisMonth:N0}");
        Console.WriteLine($"  │   Nivel adquisitivo: {enclave.NivelAdquisitivo}");

        if (aliveCreatures.Count > 0)
        {
            Console.WriteLine("  │   Criaturas (ordenadas por salud ↓):");
            // SORTED DESCENDING BY HEALTH
            foreach (var c in aliveCreatures.OrderByDescending(c => c.Health))
            {
                var healthColor = c.Health >= 75 ? ConsoleColor.Green
                                : c.Health >= 40 ? ConsoleColor.Yellow
                                : ConsoleColor.Red;
                Console.Write($"  │     • {c.Name,-30} Salud: ");
                Console.ForegroundColor = healthColor;
                Console.Write($"{c.Health,3}");
                Console.ResetColor();
                Console.WriteLine($" | Edad: {c.AgeInMonths} meses | Dieta: {c.Diet} | Hábitat: {c.Habitat}");
            }
        }
        else
        {
            Console.WriteLine("  │   (Sin criaturas)");
        }

        if (enclave.Facilities.Count > 0)
        {
            Console.Write("  │   Instalaciones: ");
            Console.WriteLine(string.Join(", ", enclave.Facilities.Select(f => f.Name)));
        }
        Console.WriteLine("  └─");
    }

    // Total visitors
    var totalVisitors = s.Enclaves.Sum(e => e.TotalVisitorsThisMonth);
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"  👥 Total visitantes último mes: {totalVisitors:N0}");
    Console.WriteLine($"  🐾 Total criaturas vivas: {s.Enclaves.Sum(e => e.Creatures.Count(c => c.IsAlive))}");
    Console.ResetColor();
}

// ─────────────────────────────────────────────
// MANAGE ENCLAVES / CREATURES
// ─────────────────────────────────────────────
async Task ManageEnclavesMenuAsync()
{
    if (currentGame is null) { PrintError("No hay partida activa."); return; }

    while (true)
    {
        PrintHeader("GESTIÓN DE ENCLAVES Y CRIATURAS");
        var s = currentGame.ActiveScenario;
        Console.WriteLine($"  Solaris disponibles: {s.CurrentSolaris:N0}");
        Console.WriteLine();
        Console.WriteLine("  [1] Comprar criatura");
        Console.WriteLine("  [2] Alimentar criatura");
        Console.WriteLine("  [3] Trasladar criatura");
        Console.WriteLine("  [4] Construir instalación");
        Console.WriteLine("  [0] Volver");
        Console.Write("  Opción: ");

        var key = Console.ReadLine()?.Trim();
        switch (key)
        {
            case "1": await BuyCreatureMenuAsync(); break;
            case "2": await FeedCreatureMenuAsync(); break;
            case "3": await TransferCreatureMenuAsync(); break;
            case "4": await BuildFacilityMenuAsync(); break;
            case "0": return;
            default: PrintError("Opción no válida."); break;
        }
    }
}

async Task BuyCreatureMenuAsync()
{
    var available = await simulationClient.IsAvailableAsync();
    if (!available) { PrintError("Servicio de simulación no disponible."); return; }

    var s = currentGame!.ActiveScenario;
    PrintHeader("COMPRAR CRIATURA");
    Console.WriteLine($"  Solaris: {s.CurrentSolaris:N0}");
    Console.WriteLine();
    Console.WriteLine("  Criaturas disponibles:");

    var types = Enum.GetValues<CreatureType>().ToList();
    for (int i = 0; i < types.Count; i++)
    {
        var t = types[i];
        var tmpl = Creature.Templates[t];
        Console.WriteLine($"  [{i + 1}] {tmpl.Name,-35} Coste: {tmpl.AcquisitionCost,8:N0} Solaris | Dieta: {tmpl.Diet} | Hábitat: {tmpl.Habitat}");
    }
    Console.Write("  Tipo (número): ");
    if (!int.TryParse(Console.ReadLine(), out var tIdx) || tIdx < 1 || tIdx > types.Count)
    { PrintError("Selección inválida."); return; }
    var creatureType = types[tIdx - 1];

    Console.WriteLine();
    Console.WriteLine("  Enclaves disponibles:");
    for (int i = 0; i < s.Enclaves.Count; i++)
    {
        var e = s.Enclaves[i];
        Console.WriteLine($"  [{i + 1}] {e.Name} ({e.Type}) - {e.Creatures.Count(c => c.IsAlive)}/{e.MaxCreatureCapacity} criaturas");
    }
    Console.Write("  Enclave (número): ");
    if (!int.TryParse(Console.ReadLine(), out var eIdx) || eIdx < 1 || eIdx > s.Enclaves.Count)
    { PrintError("Selección inválida."); return; }
    var enclaveId = s.Enclaves[eIdx - 1].Id;

    try
    {
        var updatedState = await simulationClient.BuyCreatureAsync(currentGame, enclaveId, creatureType);
        if (updatedState != null)
        {
            currentGame.ActiveScenario = updatedState.ActiveScenario;
            PrintSuccess($"Criatura adquirida correctamente. Solaris restantes: {s.CurrentSolaris:N0}");
        }
    }
    catch (InvalidOperationException ex)
    {
        PrintError(ex.Message);
    }
}

async Task FeedCreatureMenuAsync()
{
    var available = await simulationClient.IsAvailableAsync();
    if (!available) { PrintError("Servicio de simulación no disponible."); return; }

    var s = currentGame!.ActiveScenario;
    PrintHeader("ALIMENTAR CRIATURA");

    var allCreatures = s.Enclaves
        .SelectMany(e => e.Creatures.Where(c => c.IsAlive).Select(c => new { c, e }))
        .OrderByDescending(x => x.c.Health)
        .ToList();

    if (!allCreatures.Any()) { PrintInfo("No hay criaturas vivas."); return; }

    Console.WriteLine("  Criaturas (ordenadas por salud ↓):");
    for (int i = 0; i < allCreatures.Count; i++)
    {
        var x = allCreatures[i];
        Console.WriteLine($"  [{i + 1}] {x.c.Name,-30} Salud: {x.c.Health,3} | Alimentación: {x.c.FoodConsumedThisMonth}/{x.c.FoodRequiredPerMonth} | {x.e.Name}");
    }
    Console.Write("  Criatura (número): ");
    if (!int.TryParse(Console.ReadLine(), out var cIdx) || cIdx < 1 || cIdx > allCreatures.Count)
    { PrintError("Selección inválida."); return; }

    var selected = allCreatures[cIdx - 1];
    var remaining = selected.c.FoodRequiredPerMonth - selected.c.FoodConsumedThisMonth;
    Console.Write($"  Cantidad de alimento (máx. {remaining}): ");
    if (!int.TryParse(Console.ReadLine(), out var foodAmt) || foodAmt <= 0)
    { PrintError("Cantidad inválida."); return; }
    foodAmt = Math.Min(foodAmt, remaining);

    try
    {
        var updatedState = await simulationClient.FeedCreatureAsync(currentGame, selected.c.Id, foodAmt);
        if (updatedState != null)
        {
            currentGame.ActiveScenario = updatedState.ActiveScenario;
            PrintSuccess($"Criatura alimentada con {foodAmt} unidades.");
        }
    }
    catch (InvalidOperationException ex)
    {
        PrintError(ex.Message);
    }
}

async Task TransferCreatureMenuAsync()
{
    var available = await simulationClient.IsAvailableAsync();
    if (!available) { PrintError("Servicio de simulación no disponible."); return; }

    var s = currentGame!.ActiveScenario;
    PrintHeader("TRASLADAR CRIATURA");

    var allCreatures = s.Enclaves
        .SelectMany(e => e.Creatures.Where(c => c.IsAlive).Select(c => new { c, e }))
        .OrderByDescending(x => x.c.Health)
        .ToList();

    if (allCreatures.Count < 1) { PrintInfo("No hay criaturas para trasladar."); return; }

    Console.WriteLine("  Selecciona la criatura a trasladar (ordenadas por salud ↓):");
    for (int i = 0; i < allCreatures.Count; i++)
    {
        var x = allCreatures[i];
        var healthStatus = x.c.Health < 75 ? " ⚠ (salud < 75, NO trasladable)" : "";
        Console.WriteLine($"  [{i + 1}] {x.c.Name,-30} Salud: {x.c.Health,3} | {x.e.Name}{healthStatus}");
    }
    Console.Write("  Criatura (número): ");
    if (!int.TryParse(Console.ReadLine(), out var cIdx) || cIdx < 1 || cIdx > allCreatures.Count)
    { PrintError("Selección inválida."); return; }
    var selected = allCreatures[cIdx - 1];

    Console.WriteLine();
    Console.WriteLine("  Selecciona el enclave destino:");
    for (int i = 0; i < s.Enclaves.Count; i++)
    {
        var e = s.Enclaves[i];
        var marker = e.Id == selected.e.Id ? " (actual)" : "";
        Console.WriteLine($"  [{i + 1}] {e.Name} ({e.Type}) - {e.Creatures.Count(c => c.IsAlive)}/{e.MaxCreatureCapacity}{marker}");
    }
    Console.Write("  Destino (número): ");
    if (!int.TryParse(Console.ReadLine(), out var eIdx) || eIdx < 1 || eIdx > s.Enclaves.Count)
    { PrintError("Selección inválida."); return; }
    var targetEnclave = s.Enclaves[eIdx - 1];

    try
    {
        var updatedState = await simulationClient.TransferCreatureAsync(
            currentGame, selected.e.Id, targetEnclave.Id, selected.c.Id);
        if (updatedState != null)
        {
            currentGame.ActiveScenario = updatedState.ActiveScenario;
            PrintSuccess($"Criatura trasladada a '{targetEnclave.Name}' correctamente.");
        }
    }
    catch (InvalidOperationException ex)
    {
        PrintError(ex.Message);
    }
}

async Task BuildFacilityMenuAsync()
{
    var available = await simulationClient.IsAvailableAsync();
    if (!available) { PrintError("Servicio de simulación no disponible."); return; }

    var s = currentGame!.ActiveScenario;
    PrintHeader("CONSTRUIR INSTALACIÓN");
    Console.WriteLine($"  Solaris: {s.CurrentSolaris:N0}");
    Console.WriteLine();
    Console.WriteLine("  Instalaciones disponibles:");
    var types = Enum.GetValues<FacilityType>().ToList();
    for (int i = 0; i < types.Count; i++)
    {
        var (name, cost, maint) = Facility.Catalog[types[i]];
        Console.WriteLine($"  [{i + 1}] {name,-30} Coste: {cost,8:N0} | Mantenimiento: {maint,5:N0}/mes");
    }
    Console.Write("  Instalación (número): ");
    if (!int.TryParse(Console.ReadLine(), out var fIdx) || fIdx < 1 || fIdx > types.Count)
    { PrintError("Selección inválida."); return; }
    var facilityType = types[fIdx - 1];

    Console.WriteLine();
    Console.WriteLine("  Enclaves disponibles:");
    for (int i = 0; i < s.Enclaves.Count; i++)
        Console.WriteLine($"  [{i + 1}] {s.Enclaves[i].Name} ({s.Enclaves[i].Type})");
    Console.Write("  Enclave (número): ");
    if (!int.TryParse(Console.ReadLine(), out var eIdx) || eIdx < 1 || eIdx > s.Enclaves.Count)
    { PrintError("Selección inválida."); return; }
    var enclaveId = s.Enclaves[eIdx - 1].Id;

    try
    {
        var updatedState = await simulationClient.BuildFacilityAsync(currentGame, enclaveId, facilityType);
        if (updatedState != null)
        {
            currentGame.ActiveScenario = updatedState.ActiveScenario;
            PrintSuccess($"Instalación construida. Solaris restantes: {s.CurrentSolaris:N0}");
        }
    }
    catch (InvalidOperationException ex)
    {
        PrintError(ex.Message);
    }
}

// ─────────────────────────────────────────────
// ADVANCE MONTH
// ─────────────────────────────────────────────
async Task AdvanceMonthAsync()
{
    if (currentGame is null) { PrintError("No hay partida activa."); return; }

    var available = await simulationClient.IsAvailableAsync();
    if (!available)
    {
        PrintError("Servicio de simulación no disponible. Inicie DuneArrakis.SimulationService.");
        return;
    }

    var s = currentGame.ActiveScenario;
    PrintHeader($"AVANZAR MES {s.CurrentMonth}");

    var result = await simulationClient.ProcessMonthAsync(currentGame);
    if (result is null)
    {
        PrintError("Error al procesar el mes. Compruebe el Servicio de Simulación.");
        return;
    }

    // The simulation service modifies the game state and returns events
    // We need to sync the state - in this distributed setup, we re-process locally for display
    // The game state was sent to the service and modified in place, we print the events
    s.CurrentMonth = result.Month + 1;

    PrintSuccess($"Mes {result.Month} procesado. Eventos:");
    Console.WriteLine();
    foreach (var evt in result.Events)
    {
        var color = evt.EventType switch
        {
            "Muerte" => ConsoleColor.Red,
            "Reproduccion" => ConsoleColor.Green,
            "Visitantes" => ConsoleColor.Cyan,
            "Gastos" => ConsoleColor.Yellow,
            _ => ConsoleColor.White
        };
        Console.ForegroundColor = color;
        Console.WriteLine($"  [{evt.EventType}] {evt.Description}");
        Console.ResetColor();
    }

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"  Solaris actuales: {result.CurrentSolaris:N0}");
    Console.ResetColor();
}

// ─────────────────────────────────────────────
// EVENT LOG
// ─────────────────────────────────────────────
void ShowEventLog()
{
    if (currentGame is null) { PrintError("No hay partida activa."); return; }

    PrintHeader("REGISTRO CRONOLÓGICO DE EVENTOS");
    var events = currentGame.ActiveScenario.EventLog
        .OrderByDescending(e => e.Timestamp)
        .Take(50)
        .ToList();

    if (!events.Any()) { PrintInfo("No hay eventos registrados."); return; }

    foreach (var evt in events)
    {
        Console.Write($"  [{evt.Timestamp:yyyy-MM-dd HH:mm}] Mes {evt.Month,3} | ");
        Console.ForegroundColor = evt.EventType switch
        {
            "Muerte" => ConsoleColor.Red,
            "Reproduccion" => ConsoleColor.Green,
            "Visitantes" => ConsoleColor.Cyan,
            "Gastos" => ConsoleColor.Yellow,
            "Compra" => ConsoleColor.Magenta,
            "Construccion" => ConsoleColor.Blue,
            _ => ConsoleColor.White
        };
        Console.Write($"[{evt.EventType,-12}]");
        Console.ResetColor();
        Console.WriteLine($" {evt.Description}");
    }
}

async Task ConsultCrewAiAdvisorAsync()
{
    if (currentGame is null) { PrintError("No hay partida activa."); return; }

    var available = await simulationClient.IsAvailableAsync();
    if (!available)
    {
        PrintError("Servicio de simulación no disponible. Inicie DuneArrakis.SimulationService.");
        return;
    }

    var aiHealth = await simulationClient.GetCrewAiHealthAsync();
    if (aiHealth is null)
    {
        PrintError("No se pudo consultar el estado de CrewAI.");
        return;
    }

    if (!aiHealth.Configured)
    {
        PrintError(aiHealth.Error ?? "La integración con CrewAI no está configurada.");
        return;
    }

    PrintHeader("ASESOR ESTRATÉGICO CREWAI");
    Console.WriteLine($"  Estado del crew: {aiHealth.Status}");
    if (aiHealth.RequiredInputs.Count > 0)
        Console.WriteLine($"  Inputs detectados: {string.Join(", ", aiHealth.RequiredInputs)}");

    Console.WriteLine();
    Console.WriteLine("  Instrucción recomendada: analizar el estado y proponer acciones para el próximo mes.");
    Console.Write("  Prompt para el crew (Enter para usar el recomendado): ");
    var prompt = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(prompt))
        prompt = "Analiza el estado actual de Arrakis Dominion y propone las tres mejores acciones priorizadas para el próximo mes, explicando riesgos, coste estimado y beneficio esperado.";

    try
    {
        var advice = await simulationClient.GetStrategicAdviceAsync(currentGame, prompt);
        if (advice is null)
        {
            PrintError("CrewAI no devolvió una respuesta interpretable.");
            return;
        }

        Console.WriteLine();
        PrintSuccess($"Ejecución enviada a CrewAI. Kickoff: {advice.KickoffId}");
        PrintInfo($"Estado: {advice.Status}");

        if (!string.IsNullOrWhiteSpace(advice.Advice))
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  Recomendación del crew:");
            Console.ResetColor();
            Console.WriteLine();
            foreach (var line in advice.Advice.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                Console.WriteLine($"  {line}");
        }
        else if (!string.IsNullOrWhiteSpace(advice.Error))
        {
            PrintError(advice.Error);
        }
        else
        {
            PrintInfo("La ejecución sigue en curso. Consulte el estado con el kickoff devuelto desde el endpoint de simulación.");
        }
    }
    catch (InvalidOperationException ex)
    {
        PrintError(ex.Message);
    }
}

// ─────────────────────────────────────────────
// HELPER METHODS
// ─────────────────────────────────────────────
void PrintHeader(string title)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.DarkYellow;
    Console.WriteLine($"  ╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine($"  ║  {title,-60}║");
    Console.WriteLine($"  ╚══════════════════════════════════════════════════════════════╝");
    Console.ResetColor();
}

void PrintSeparator() =>
    Console.WriteLine("  ────────────────────────────────────────────────────────────────");

void PrintSuccess(string msg)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"  ✓ {msg}");
    Console.ResetColor();
}

void PrintError(string msg)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"  ✗ {msg}");
    Console.ResetColor();
}

void PrintInfo(string msg)
{
    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine($"  ℹ {msg}");
    Console.ResetColor();
}
