from __future__ import annotations

import argparse
import json
import os
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from textwrap import dedent
from typing import Any

import requests
from dotenv import load_dotenv

try:
    from crewai import Agent, Crew, Process, Task
except ImportError as exc:
    raise SystemExit(
        "Missing dependency 'crewai'. Install dependencies with: pip install -r automation/requirements.txt"
    ) from exc


load_dotenv()


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_API_URL = "https://dune-arrakis-dominion-distributed-developme-97e89950.crewai.com"
DEFAULT_IMPROVEMENT_GOAL = dedent(
    """
    Desarrollar y mejorar automaticamente la solucion distribuida en C# Dune: Arrakis Dominion Distributed,
    respetando la separacion en Cliente de administracion, Servicio de simulacion, Servicio de persistencia y Modelo compartido.
    """
).strip()


@dataclass
class AmpConfig:
    api_url: str
    bearer_token: str
    user_bearer_token: str

    @property
    def is_configured(self) -> bool:
        return bool(self.api_url and (self.bearer_token or self.user_bearer_token))

    @property
    def auth_token(self) -> str:
        return self.user_bearer_token or self.bearer_token


def read_repo_snapshot() -> dict[str, str]:
    files = {
        "README.md": REPO_ROOT / "README.md",
        "Solution": REPO_ROOT / "DuneArrakisDominion.slnx",
        "Admin Program": REPO_ROOT / "src/DuneArrakis.AdminClient/Program.cs",
        "Simulation Engine": REPO_ROOT / "src/DuneArrakis.SimulationService/Services/SimulationEngine.cs",
        "Simulation Controller": REPO_ROOT / "src/DuneArrakis.SimulationService/Controllers/SimulationController.cs",
        "Persistence Service": REPO_ROOT / "src/DuneArrakis.PersistenceService/Services/JsonPersistenceService.cs",
        "Tests": REPO_ROOT / "tests/DuneArrakis.Tests/SimulationEngineTests.cs",
    }

    snapshot: dict[str, str] = {}
    for label, file_path in files.items():
        if file_path.exists():
            snapshot[label] = file_path.read_text(encoding="utf-8")[:12000]
    return snapshot


def build_project_context(improvement_goal: str) -> dict[str, str]:
    snapshot = read_repo_snapshot()
    return {
        "project_name": "Dune: Arrakis Dominion Distributed",
        "architecture_goal": "Solucion distribuida .NET con cuatro componentes desacoplados.",
        "improvement_goal": improvement_goal,
        "current_repo_snapshot": json.dumps(snapshot, ensure_ascii=False, indent=2),
        "milestones": dedent(
            """
            1. Crear el modelo de dominio inicial y establecer los 4 proyectos base.
            2. Desarrollar la carga y guardado en JSON de los escenarios y establecer un canal de comunicacion basico entre componentes.
            3. Programar la logica mensual: reproduccion al 20 por ciento, ingesta, salud, visitantes y donaciones.
            4. Desarrollar el Centro de mando ordenando criaturas por salud y bloqueando compras sin solaris.
            """
        ).strip(),
    }


def create_crew() -> Crew:
    software_architect = Agent(
        role="Software_Architect",
        goal=(
            "Disenar la estructura de la solucion distribuida en C#, definir DTOs, interfaces y la estrategia "
            "de comunicacion asincrona entre servicios sin acoplamiento fuerte."
        ),
        backstory=(
            "Veterano en microservicios .NET que prioriza contratos estables, concurrencia segura, tolerancia a fallos "
            "y ausencia de interbloqueos."
        ),
        allow_delegation=False,
        verbose=True,
    )

    persistence_engineer = Agent(
        role="Persistence_Engineer",
        goal=(
            "Implementar el Servicio de Persistencia en C# para guardar y cargar partidas en JSON interoperable, "
            "con consistencia eventual y tolerancia a fallos."
        ),
        backstory=(
            "Ingeniero de datos obsesionado con la integridad en sistemas distribuidos y con una fuerte disciplina "
            "en serializacion, versionado y recuperacion."
        ),
        allow_delegation=False,
        verbose=True,
    )

    simulation_developer = Agent(
        role="Simulation_Developer",
        goal=(
            "Programar el Servicio de Simulacion y las rondas mensuales con reglas de negocio estrictas para reproduccion, "
            "ingesta, salud, visitantes y donaciones."
        ),
        backstory=(
            "Especialista en algoritmos de simulacion y matematica aplicada a juegos de gestion, con foco en exactitud y pruebas."
        ),
        allow_delegation=False,
        verbose=True,
    )

    frontend_developer = Agent(
        role="Frontend_Developer",
        goal=(
            "Construir el Centro de mando .NET para mostrar el estado agregado de la partida, advertencias de sincronizacion "
            "y errores de forma amigable."
        ),
        backstory=(
            "Especialista en experiencia de operador, estados agregados y flujos robustos para administracion distribuida."
        ),
        allow_delegation=False,
        verbose=True,
    )

    architect_task = Task(
        description=dedent(
            """
            Hito 1.
            Crea el modelo de dominio inicial para Dune: Arrakis Dominion Distributed y define la estructura de la solucion .NET.
            Debes incluir entidades como Enclave, Facility, Creature, GameState, Scenario y SimulationEvent,
            incluyendo criaturas como Gusano de arena, Tigre laza y Muad'Dib.
            Define los cuatro proyectos base: AdminClient, SimulationService, PersistenceService y Domain.
            Entrega DTOs, interfaces y una estrategia de comunicacion asincrona y desacoplada entre servicios.
            """
        ).strip(),
        expected_output=(
            "Diseno tecnico del hito 1 con contratos C#, proyectos base, entidades de dominio, interfaces y decisiones "
            "de comunicacion distribuida."
        ),
        agent=software_architect,
    )

    persistence_task = Task(
        description=dedent(
            """
            Hito 2.
            Usa el diseno del arquitecto para implementar la carga y el guardado en JSON de los escenarios Arrakeen,
            Giedi Prime y Caladan. Define un canal de comunicacion basico entre componentes que permita interoperabilidad,
            versionado y recuperacion segura del estado.
            """
        ).strip(),
        expected_output=(
            "Implementacion del servicio de persistencia con contratos JSON, endpoints o mecanismos de integracion y manejo de fallos."
        ),
        agent=persistence_engineer,
        context=[architect_task],
    )

    simulation_task = Task(
        description=dedent(
            """
            Hito 3.
            Implementa la logica mensual del simulador en C# con estas reglas minimas:
            - 20 por ciento de probabilidad de reproduccion.
            - Si una criatura recibe menos del 25 por ciento de su ingesta requerida, pierde 30 de salud.
            - Si recibe menos del 75 por ciento, pierde 20.
            - Si recibe menos del 100 por ciento, pierde 10.
            - Si recibe alimentacion optima, gana 5.
            - Calculo de visitantes y donaciones por enclave.
            Genera tambien recomendaciones de pruebas para esta logica.
            """
        ).strip(),
        expected_output=(
            "Implementacion detallada del servicio de simulacion con reglas mensuales, formulas y cobertura sugerida de pruebas."
        ),
        agent=simulation_developer,
        context=[architect_task, persistence_task],
    )

    frontend_task = Task(
        description=dedent(
            """
            Hito 4.
            Desarrolla el Centro de mando del cliente de administracion en .NET. Debe mostrar criaturas ordenadas de forma
            descendente por salud y validar que no se permitan compras sin solaris suficientes. Debe reflejar tambien advertencias
            de sincronizacion o disponibilidad de servicios cuando el sistema distribuido no este listo.
            """
        ).strip(),
        expected_output=(
            "Diseno e implementacion del Centro de mando con estados agregados, validaciones de fondos y mensajes operativos claros."
        ),
        agent=frontend_developer,
        context=[architect_task, persistence_task, simulation_task],
    )

    return Crew(
        agents=[software_architect, persistence_engineer, simulation_developer, frontend_developer],
        tasks=[architect_task, persistence_task, simulation_task, frontend_task],
        process=Process.sequential,
        verbose=True,
    )


def call_remote_amp(config: AmpConfig, inputs: dict[str, str], wait: bool) -> dict[str, Any]:
    if not config.is_configured:
        raise RuntimeError("CREWAI_API_URL y al menos un token de CrewAI deben estar configurados.")

    headers = {
        "Authorization": f"Bearer {config.auth_token}",
        "Content-Type": "application/json",
    }

    kickoff_response = requests.post(
        f"{config.api_url.rstrip('/')}/kickoff",
        headers=headers,
        json={
            "inputs": inputs,
            "meta": {
                "source": "automation/crewai_dune_orchestrator.py",
                "repository": "rubences/Dune-Arrakis-Dominion-Distributed",
            },
        },
        timeout=30,
    )
    kickoff_response.raise_for_status()
    kickoff_payload = kickoff_response.json()
    kickoff_id = kickoff_payload.get("kickoff_id", "")

    result: dict[str, Any] = {"kickoff_id": kickoff_id, "status": "submitted"}
    if not wait or not kickoff_id:
        return result

    for _ in range(10):
        status_response = requests.get(
            f"{config.api_url.rstrip('/')}/{kickoff_id}/status",
            headers=headers,
            timeout=30,
        )
        status_response.raise_for_status()
        status_payload = status_response.json()
        status = str(status_payload.get("status", "unknown"))
        result = {
            "kickoff_id": kickoff_id,
            "status": status,
            "payload": status_payload,
        }
        if status.lower() in {"completed", "failed", "error", "cancelled"}:
            return result
        time.sleep(3)

    return result


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="CrewAI orchestrator for Dune: Arrakis Dominion Distributed")
    parser.add_argument("--mode", choices=["local", "remote"], default="local")
    parser.add_argument("--wait", action="store_true", help="Wait for completion when using remote AMP mode")
    parser.add_argument(
        "--goal",
        default=DEFAULT_IMPROVEMENT_GOAL,
        help="Improvement goal passed into the crew context",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    config = AmpConfig(
        api_url=os.getenv("CREWAI_API_URL", DEFAULT_API_URL),
        bearer_token=os.getenv("CREWAI_BEARER_TOKEN", ""),
        user_bearer_token=os.getenv("CREWAI_USER_BEARER_TOKEN", ""),
    )

    inputs = build_project_context(args.goal)

    if args.mode == "remote":
        result = call_remote_amp(config, inputs, wait=args.wait)
        print(json.dumps(result, indent=2, ensure_ascii=False))
        return 0

    crew = create_crew()
    result = crew.kickoff(inputs=inputs)
    print(result)
    return 0


if __name__ == "__main__":
    sys.exit(main())