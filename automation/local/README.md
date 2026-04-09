# Gentleman Local Stack (Repositorio)

Este modulo instala y usa las herramientas Gentleman directamente dentro del repositorio, sin instalacion global del sistema.

## Incluye

- automation/external/engram
- automation/external/gentle-ai
- automation/external/gentleman-guardian-angel
- automation/external/Gentleman-Skills
- automation/tools/bin/engram
- automation/tools/bin/gentle-ai
- automation/tools/bin/gga
- automation/tools/skills/curated -> symlink a Gentleman-Skills/curated
- automation/tools/skills/community -> symlink a Gentleman-Skills/community

## Bootstrap local (sync + build)

```bash
bash automation/local/bootstrap-gentleman-local.sh
```

## Comando unico integrado al juego

Ejecuta en un solo flujo:

- `engram setup opencode`
- `gentle-ai install --agent opencode --preset full-gentleman`
- automatizacion mensual CrewAI existente del proyecto
- revision GGA sobre cambios staged de simulacion/tests

```bash
bash automation/local/run-game-ai-stack.sh
```

Opciones utiles:

```bash
# Solo configurar stack local (sin CrewAI ni GGA)
bash automation/local/run-game-ai-stack.sh --skip-crewai --skip-gga

# CrewAI remoto con espera
bash automation/local/run-game-ai-stack.sh --crewai-mode remote --wait-remote

# CrewAI con estado de juego custom
bash automation/local/run-game-ai-stack.sh --state-file /ruta/game_state.json

# Solo GGA y con stage automatico de archivos de simulacion/tests
bash automation/local/run-game-ai-stack.sh --skip-bootstrap --skip-crewai --gga-only --auto-stage-simulation
```

Task de VS Code recomendada para este flujo:

- `Local: Auto-Stage Simulation + GGA Review`

## Usar herramientas en la shell actual

```bash
source automation/local/use-gentleman-tools.sh
```

## Verificacion

```bash
engram version
gentle-ai --version
gga version
```

## Integracion con tu proyecto/juego

- Usa `engram` para memoria persistente del desarrollo y decisiones del simulador.
- Usa `gentle-ai` para configurar presets/workflows de agentes sobre tu base de codigo.
- Usa `gga run` manualmente para revisar cambios sin instalar hooks en tus repos.
- Usa las skills desde `automation/tools/skills/curated` y `automation/tools/skills/community` como base para prompts/agentes del proyecto.
