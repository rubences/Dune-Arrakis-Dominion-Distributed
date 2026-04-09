# Windows Stack Installer (OpenCode + Gentleman)

Este modulo instala y valida en Windows nativo:

- OpenCode
- Engram
- gentle-ai
- Gentleman Guardian Angel (GGA)

Script principal:

- `automation/windows/install-gentleman-stack.ps1`

## Ejecucion rapida (PowerShell)

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned -Force
cd <ruta-del-repo>
powershell -ExecutionPolicy Bypass -File .\automation\windows\install-gentleman-stack.ps1
```

## Opciones utiles

```powershell
# Saltar integracion engram -> opencode
powershell -ExecutionPolicy Bypass -File .\automation\windows\install-gentleman-stack.ps1 -SkipOpenCodeConfig

# Saltar integracion gentle-ai -> opencode
powershell -ExecutionPolicy Bypass -File .\automation\windows\install-gentleman-stack.ps1 -SkipGentleAiConfig

# Cambiar carpeta de fuentes para clonado
powershell -ExecutionPolicy Bypass -File .\automation\windows\install-gentleman-stack.ps1 -SourceRoot "$env:USERPROFILE\src"
```

## Resultado esperado

Al finalizar, se genera un reporte en:

- `automation/output/windows-gentleman-stack-report-YYYYMMDD-HHMMSS.txt`

El reporte incluye:

1. RESUMEN
2. VERSIONES
3. RUTAS IMPORTANTES
4. CAMBIOS EN PATH
5. PENDIENTES
6. ERRORES O RIESGOS

## Notas

- El script es idempotente: detecta instalaciones existentes y reutiliza componentes validos.
- Usa rutas y fuentes oficiales con fallbacks oficiales.
- No inicializa hooks de GGA en repositorios de codigo.
