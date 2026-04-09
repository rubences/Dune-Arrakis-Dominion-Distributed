# Dune Arrakis Dominion - Unity Project Setup Guide

## 1. Requisitos del Proyecto Unity

| Requisito | Versión recomendada |
|---|---|
| Unity Editor | **2022.3 LTS** o **2023.2+** |
| Render Pipeline | **Universal Render Pipeline (URP)** |
| TextMeshPro | Incluido (instalar via Package Manager) |
| Newtonsoft.Json (opcional) | `com.unity.nuget.newtonsoft-json` 3.x |

---

## 2. Crear el Proyecto Unity

1. Abre **Unity Hub** → `New Project`
2. Selecciona template: **3D (URP)**
3. Nombre del proyecto: `DuneArrakisDominion`
4. Localización: `Dune-Arrakis-Dominion-Distributed/unity/`

---

## 3. Estructura de Carpetas de Assets

Después de crear el proyecto, crea esta estructura dentro de `Assets/`:

```
Assets/
├── Scripts/
│   ├── Core/
│   │   └── GameController.cs          ← ya generado
│   ├── Data/
│   │   └── DomainModels.cs            ← ya generado
│   ├── Network/
│   │   └── BackendManager.cs          ← ya generado
│   ├── UI/
│   │   ├── UIManager.cs               ← ya generado
│   │   ├── EnclaveView.cs             ← ya generado
│   │   ├── CreatureSlot.cs            ← ya generado
│   │   ├── AgentMonitorPanel.cs       ← ya generado
│   │   ├── MainMenuController.cs      ← ya generado
│   │   ├── CreatureShopController.cs  ← ya generado
│   │   └── TransferCreatureDialogManager.cs ← ya generado
│   └── VFX/
│       ├── DesertParticleController.cs ← ya generado
│       └── HolographicScanlineEffect.cs ← ya generado
├── Scenes/
│   ├── MainMenu.unity
│   └── GameScene.unity
├── Prefabs/
│   ├── UI/
│   │   ├── EnclaveCard.prefab
│   │   ├── CreatureSlot.prefab
│   │   ├── EventLogEntry.prefab
│   │   ├── ToastNotification.prefab
│   │   └── CreatureShopCard.prefab
├── Materials/
│   ├── DesertSand.mat
│   ├── HolographicPanel.mat
│   └── ScanlineOverlay.mat
└── Settings/
    └── UniversalRenderPipelineAsset.asset
```

---

## 4. Configurar la Escena Principal (GameScene)

### Hierarchy recomendada:

```
GameScene
├── --- MANAGERS ---
│   ├── [BackendManager]      → BackendManager.cs
│   ├── [GameController]      → GameController.cs
│   └── [UIManager]           → UIManager.cs
├── --- CAMERA ---
│   └── Main Camera           → Con Volume (HolographicScanlineEffect)
├── --- VFX ---
│   ├── SandstormParticles    → DesertParticleController.cs
│   └── AmbientDust
├── --- ENVIRONMENT ---
│   ├── DesertTerrain         (mesh de terreno árido)
│   ├── EnclaveZone_01        → EnclaveView.cs    (enclaveId: se asigna en Runtime)
│   └── EnclaveZone_02        → EnclaveView.cs
└── --- UI ---
    └── Canvas (Screen Space - Overlay)
        ├── Panel_MainMenu     → MainMenuController.cs
        ├── Panel_HUD
        ├── Panel_AgentMonitor → AgentMonitorPanel.cs
        ├── Panel_MonthResults
        ├── Panel_CreatureShop → CreatureShopController.cs
        ├── Panel_Advice
        ├── Panel_Loading
        └── Panel_TransferDialog → TransferCreatureDialogManager.cs
```

---

## 5. Conectar el BackendManager

En el Inspector del GameObject `[BackendManager]`:
- `Base Url`: `http://localhost:5000` (puerto donde corre tu SimulationService)

Para iniciar el backend .NET antes de ejecutar Unity:
```powershell
cd src\DuneArrakis.SimulationService
dotnet run
```

---

## 6. Paleta de Colores y Estética "Dune 2026"

El juego usa una estética **desert-punk / holográfico** oscura:

| Elemento | Color HEX |
|---|---|
| Fondo base | `#0A0806` |
| Arena / Desierto | `#C4963A` |
| Acento dorado | `#FFD700` |
| Azul holográfico | `#00B4D8` |
| Verde salud | `#44FF88` |
| Rojo peligro | `#FF4455` |
| Texto principal | `#E8D5A3` |
| Panel transparente | `#1A1410` + alpha 0.85 |

**Fuentes recomendadas:** `Cinzel` (títulos épicos) + `Rajdhani` (HUD / datos).
Disponibles en Google Fonts para importar en Unity.

---

## 7. Efecto Holográfico (URP Post Processing)

El script `HolographicScanlineEffect.cs` define las variables de un Volume Override custom.  
Para activarlo necesitarás implementar el correspondiente **Renderer Feature** (shader HLSL).

Un shader mínimo de scanlines en URP puede hacerse con ShaderGraph:
1. Create → ShaderGraph → FullScreen Shader Graph
2. Nodo `Screen Position` → `UV` → nodo `Sine` (frequency = 800) → multiply con `Time` → add al color final

---

## 8. Flujo de Juego en Unity

```
MainMenu
  └─ StartNewGame(scenarioType, saveName)
       └─ BackendManager.NewGame → HTTP POST /api/simulation/new-game
            └─ GameController.OnStateReceived
                 └─ UIManager.RefreshAll()
                 └─ Phase = Planning

Planning
  ├─ Jugador puede:
  │   ├─ ComprarCriatura → BackendManager.PurchaseCreature
  │   ├─ Alimentar       → BackendManager.FeedCreature
  │   └─ Trasladar       → BackendManager.TransferCreature
  └─ Jugador pulsa "Fin de Turno"
       └─ BackendManager.ProcessMonth
            └─ .NET: SimulationEngine.ProcessMonthAsync()
                  └─ MediatR publica SimulationMonthEndedEvent (PARALELO)
                       ├─ StrategicAdvisorAgent → CrewAI
                       └─ LogisticsAutomationAgent → CrewAI (Decision)
            └─ Devuelve SimulationResult
                 └─ UIManager.ShowMonthResults()
                 └─ Phase = MonthResolution

MonthResolution
  └─ Jugador pulsa "Continuar"
       └─ Phase = Planning (siguiente mes)
```
