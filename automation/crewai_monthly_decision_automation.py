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


load_dotenv()


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_DIR = REPO_ROOT / "automation" / "output"
DEFAULT_API_URL = "https://gaming-analytics-content-automation-v1-ef54-0203d4b9.crewai.com"


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


def load_crewai():
    try:
        from crewai import Agent, Crew, Process, Task
    except ImportError as exc:
        raise SystemExit(
            "Missing dependency 'crewai'. Install dependencies with: pip install -r automation/requirements.txt"
        ) from exc

    return Agent, Crew, Process, Task


def default_game_state() -> dict[str, Any]:
    return {
        "solarís": 25000,
        "suministros_disponibles": 180,
        "precio_suministro": 5,
        "mes_actual": 6,
        "enclaves": [
            {
                "id": "aclimatacion-01",
                "nombre": "Zona de Aclimatación",
                "tipo": "Aclimatacion",
                "criaturas": [
                    {
                        "id": "CR-001",
                        "nombre": "Gusano Alfa",
                        "tipo": "GusanoDeArenaJuvenil",
                        "salud": 84,
                        "edad_meses": 14,
                        "es_adulta": True,
                        "ingesta_requerida": 100,
                        "enclave": "Aclimatacion"
                    },
                    {
                        "id": "CR-002",
                        "nombre": "Tigre Rojo",
                        "tipo": "TigreLaza",
                        "salud": 72,
                        "edad_meses": 10,
                        "es_adulta": True,
                        "ingesta_requerida": 50,
                        "enclave": "Aclimatacion"
                    }
                ]
            },
            {
                "id": "exhibicion-01",
                "nombre": "Zona de Exhibición",
                "tipo": "Exhibicion",
                "criaturas": [
                    {
                        "id": "CR-003",
                        "nombre": "Muad'Dib Prime",
                        "tipo": "MuadDib",
                        "salud": 91,
                        "edad_meses": 9,
                        "es_adulta": True,
                        "ingesta_requerida": 10,
                        "enclave": "Exhibicion"
                    }
                ]
            }
        ]
    }


def load_game_state(path: str | None) -> dict[str, Any]:
    if not path:
        return default_game_state()

    with open(path, "r", encoding="utf-8") as handle:
        return json.load(handle)


def normalize_actions(raw_text: str) -> dict[str, Any]:
    try:
        if raw_text.strip().startswith("{"):
            payload = json.loads(raw_text)
            return {
                "comprar_suministros": int(payload.get("comprar_suministros", 0)),
                "trasladar_criaturas": list(payload.get("trasladar_criaturas", [])),
                "registrar_letargo": list(payload.get("registrar_letargo", [])),
            }
    except json.JSONDecodeError:
        pass

    return {
        "comprar_suministros": 0,
        "trasladar_criaturas": [],
        "registrar_letargo": [],
        "raw_output": raw_text,
    }


def write_actions_file(actions: dict[str, Any], output_file: Path | None = None) -> Path:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    target = output_file or OUTPUT_DIR / "monthly_actions.json"
    target.write_text(json.dumps(actions, ensure_ascii=False, indent=2), encoding="utf-8")
    return target


def create_crew():
    Agent, Crew, Process, Task = load_crewai()

    mentat_financiero = Agent(
        role="Mentat_Financiero",
        goal=(
            "Analizar fondos, suministros y apetito total de las criaturas para comprar comida a 5 solaris la unidad "
            "sin llevar al reino a la bancarrota."
        ),
        backstory=(
            "Estratega calculador que conoce el coste exacto de la inanicion: por debajo del 25 por ciento de ingesta, "
            "las criaturas pierden 30 de salud y el negocio colapsa."
        ),
        allow_delegation=False,
        verbose=True,
    )

    maestro_de_bestias = Agent(
        role="Maestro_de_Bestias",
        goal=(
            "Revisar la salud, adultez y enclave actual de cada criatura para trasladar a exhibicion los adultos sanos "
            "con salud superior a 75 y registrar letargo cuando la salud llegue a cero."
        ),
        backstory=(
            "Experto del desierto que prioriza la supervivencia de los Gusanos de Arena y los Tigres Laza y domina la logistica de enclaves."
        ),
        allow_delegation=False,
        verbose=True,
    )

    supply_audit = Task(
        description=dedent(
            """
            Tarea 1. Auditoria de suministros.
            Lee el JSON del estado del juego, calcula la comida requerida total del mes, compara con el almacen disponible
            y devuelve un comando o valor para comprar suministros solo si hace falta, asumiendo 5 solaris por unidad.
            Debes mantener liquidez suficiente y nunca proponer una compra que deje solaris negativos.
            """
        ).strip(),
        expected_output=(
            "JSON parcial con el campo comprar_suministros y una breve justificacion economica."
        ),
        agent=mentat_financiero,
    )

    enclave_management = Task(
        description=dedent(
            """
            Tarea 2. Gestion de enclaves.
            Lee el estado de las criaturas. Para cada criatura en Aclimatacion que sea adulta y tenga salud mayor a 75,
            genera una orden de traslado a Exhibicion. Si la salud es cero, registrala en letargo. El resultado final debe ser
            un JSON estricto con esta forma aproximada:
            {
              "comprar_suministros": 100,
              "trasladar_criaturas": ["ID_01", "ID_03"],
              "registrar_letargo": ["ID_09"]
            }
            """
        ).strip(),
        expected_output=(
            "JSON final con acciones a ejecutar, incluyendo comprar_suministros, trasladar_criaturas y registrar_letargo."
        ),
        agent=maestro_de_bestias,
        context=[supply_audit],
    )

    return Crew(
        agents=[mentat_financiero, maestro_de_bestias],
        tasks=[supply_audit, enclave_management],
        process=Process.sequential,
        verbose=True,
    )


def build_inputs(game_state: dict[str, Any]) -> dict[str, str]:
    return {
        "game_name": "Dune: Arrakis Dominion",
        "game_state": json.dumps(game_state, ensure_ascii=False),
        "month": str(game_state.get("mes_actual", 1)),
        "instructions": (
            "Devuelve exclusivamente un JSON con acciones a ejecutar para este mes: comprar_suministros, trasladar_criaturas y registrar_letargo."
        ),
        "resource_rules": (
            "Cada unidad de suministro cuesta 5 solaris. Si una criatura recibe menos del 25 por ciento de ingesta pierde 30 de salud. "
            "Mover a Exhibicion toda criatura adulta en Aclimatacion con salud superior a 75."
        ),
    }


def get_required_inputs(config: AmpConfig) -> list[str]:
    headers = {"Authorization": f"Bearer {config.auth_token}"}
    response = requests.get(f"{config.api_url.rstrip('/')}/inputs", headers=headers, timeout=30)
    response.raise_for_status()
    payload = response.json()
    return list(payload.get("inputs", []))


def adapt_inputs_for_remote(required_inputs: list[str], canonical_inputs: dict[str, str]) -> dict[str, str]:
    if not required_inputs:
        return canonical_inputs

    lowered = {key.lower(): value for key, value in canonical_inputs.items()}
    adapted: dict[str, str] = {}
    for key in required_inputs:
        match = lowered.get(key.lower())
        if match is not None:
            adapted[key] = match
            continue

        if "game_name" == key.lower() or "game" in key.lower():
            adapted[key] = canonical_inputs["game_name"]
        elif "state" in key.lower():
            adapted[key] = canonical_inputs["game_state"]
        elif "month" in key.lower():
            adapted[key] = canonical_inputs["month"]
        elif "instruction" in key.lower() or "prompt" in key.lower():
            adapted[key] = canonical_inputs["instructions"]
        elif "rule" in key.lower() or "resource" in key.lower():
            adapted[key] = canonical_inputs["resource_rules"]
        else:
            adapted[key] = canonical_inputs["game_state"]

    return adapted


def call_remote_amp(config: AmpConfig, inputs: dict[str, str], wait: bool) -> dict[str, Any]:
    if not config.is_configured:
        raise RuntimeError("CREWAI_API_URL y al menos un token de CrewAI deben estar configurados.")

    headers = {
        "Authorization": f"Bearer {config.auth_token}",
        "Content-Type": "application/json",
    }

    required_inputs = get_required_inputs(config)
    adapted_inputs = adapt_inputs_for_remote(required_inputs, inputs)

    kickoff_response = requests.post(
        f"{config.api_url.rstrip('/')}/kickoff",
        headers=headers,
        json={
            "inputs": adapted_inputs,
            "meta": {
                "source": "automation/crewai_monthly_decision_automation.py",
                "required_inputs": required_inputs,
            },
        },
        timeout=30,
    )
    kickoff_response.raise_for_status()
    kickoff_payload = kickoff_response.json()
    kickoff_id = kickoff_payload.get("kickoff_id", "")

    result: dict[str, Any] = {
        "kickoff_id": kickoff_id,
        "status": "submitted",
        "required_inputs": required_inputs,
        "sent_inputs": list(adapted_inputs.keys()),
    }
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
            "required_inputs": required_inputs,
            "payload": status_payload,
        }
        if status.lower() in {"completed", "failed", "error", "cancelled"}:
            return result
        time.sleep(3)

    return result


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="CrewAI monthly decision automation for Dune: Arrakis Dominion")
    parser.add_argument("--mode", choices=["local", "remote"], default="local")
    parser.add_argument("--wait", action="store_true", help="Wait for completion when using remote AMP mode")
    parser.add_argument("--state-file", help="Path to a JSON file with the current game state")
    parser.add_argument("--output-file", help="Path for the resulting actions JSON file")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    game_state = load_game_state(args.state_file)
    output_file = Path(args.output_file) if args.output_file else None
    inputs = build_inputs(game_state)

    config = AmpConfig(
        api_url=os.getenv("CREWAI_API_URL", DEFAULT_API_URL),
        bearer_token=os.getenv("CREWAI_BEARER_TOKEN", ""),
        user_bearer_token=os.getenv("CREWAI_USER_BEARER_TOKEN", ""),
    )

    if args.mode == "remote":
        result = call_remote_amp(config, inputs, wait=args.wait)
        raw_output = json.dumps(result, ensure_ascii=False, indent=2)
        write_actions_file(normalize_actions(raw_output), output_file)
        print(raw_output)
        return 0

    crew = create_crew()
    result = crew.kickoff(inputs=inputs)
    actions = normalize_actions(str(result))
    target = write_actions_file(actions, output_file)
    print(json.dumps(actions, ensure_ascii=False, indent=2))
    print(f"Actions file written to {target}")
    return 0


if __name__ == "__main__":
    sys.exit(main())