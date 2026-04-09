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
