from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import time
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_DIR = REPO_ROOT / "automation" / "output"
DEFAULT_REPORT_FILE = OUTPUT_DIR / "agentic_automation_report.json"


@dataclass
class CommandResult:
    name: str
    command: list[str]
    success: bool
    exit_code: int
    duration_seconds: float
    stdout_tail: str
    stderr_tail: str


@dataclass
class AgentStep:
    agent: str
    action: str
    status: str
    details: str
    timestamp_utc: float


@dataclass
class AutomationReport:
    started_at_utc: float
    finished_at_utc: float = 0.0
    summary_status: str = "running"
    steps: list[AgentStep] = field(default_factory=list)
    commands: list[CommandResult] = field(default_factory=list)
    recommendations: list[str] = field(default_factory=list)


class AgenticAutomation:
    def __init__(self, args: argparse.Namespace) -> None:
        self.args = args
        self.report = AutomationReport(started_at_utc=time.time())

    def add_step(self, agent: str, action: str, status: str, details: str) -> None:
        self.report.steps.append(
            AgentStep(
                agent=agent,
                action=action,
                status=status,
                details=details,
                timestamp_utc=time.time(),
            )
        )

    def run_command(self, name: str, command: list[str]) -> CommandResult:
        started = time.perf_counter()
        completed = subprocess.run(
            command,
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
            check=False,
        )
        duration = time.perf_counter() - started

        result = CommandResult(
            name=name,
            command=command,
            success=completed.returncode == 0,
            exit_code=completed.returncode,
            duration_seconds=round(duration, 3),
            stdout_tail=completed.stdout[-3000:],
            stderr_tail=completed.stderr[-3000:],
        )
        self.report.commands.append(result)
        return result

    def planner_agent(self) -> None:
        self.add_step(
            "PlannerAgent",
            "Build execution plan",
            "ok",
            (
                "Plan initialized with IBM-style multi-agent phases: "
                "plan, execute with tools, validate outcomes, and govern with guardrails."
            ),
        )

    def quality_agent(self) -> bool:
        self.add_step("QualityAgent", "dotnet restore", "running", "Restoring solution dependencies.")
        restore = self.run_command("dotnet-restore", ["dotnet", "restore", "DuneArrakisDominion.slnx"])
        self.add_step(
            "QualityAgent",
            "dotnet restore",
            "ok" if restore.success else "error",
            f"Exit code: {restore.exit_code}",
        )
        if not restore.success and not self.args.continue_on_error:
            return False

        if not self.args.skip_build:
            self.add_step("QualityAgent", "dotnet build", "running", "Building in Release mode.")
            build = self.run_command(
                "dotnet-build",
                ["dotnet", "build", "DuneArrakisDominion.slnx", "-c", "Release", "--no-restore"],
            )
            self.add_step(
                "QualityAgent",
                "dotnet build",
                "ok" if build.success else "error",
                f"Exit code: {build.exit_code}",
            )
            if not build.success and not self.args.continue_on_error:
                return False

        if not self.args.skip_tests:
            self.add_step("QualityAgent", "dotnet test", "running", "Running automated tests.")
            tests = self.run_command(
                "dotnet-test",
                [
                    "dotnet",
                    "test",
                    "DuneArrakisDominion.slnx",
                    "-c",
                    "Release",
                    "--no-build",
                    "--verbosity",
                    "minimal",
                ],
            )
            self.add_step(
                "QualityAgent",
                "dotnet test",
                "ok" if tests.success else "error",
                f"Exit code: {tests.exit_code}",
            )
            if not tests.success and not self.args.continue_on_error:
                return False

        return True

    def simulation_ops_agent(self) -> bool:
        if self.args.skip_ai:
            self.add_step(
                "SimulationOpsAgent",
                "AI monthly automation",
                "skipped",
                "Skipped by --skip-ai flag.",
            )
            return True

        # Uses the same interpreter running this script, avoiding environment drift.
        command = [
            sys.executable,
            "automation/crewai_monthly_decision_automation.py",
            "--mode",
            self.args.ai_mode,
            "--output-file",
            str(self.args.monthly_actions_file),
        ]
        if self.args.ai_mode == "remote" and self.args.wait_remote:
            command.append("--wait")

        self.add_step(
            "SimulationOpsAgent",
            "Run monthly decision automation",
            "running",
            f"Mode={self.args.ai_mode}",
        )
        run_result = self.run_command("monthly-decision-automation", command)
        self.add_step(
            "SimulationOpsAgent",
            "Run monthly decision automation",
            "ok" if run_result.success else "error",
            f"Exit code: {run_result.exit_code}",
        )

        if not run_result.success:
            self.report.recommendations.append(
                "Instala dependencias Python y configura tokens CrewAI para ejecutar la fase de automatizacion AI."
            )
            if not self.args.continue_on_error:
                return False

        return True

    def governance_agent(self) -> None:
        has_remote_tokens = bool(os.getenv("CREWAI_BEARER_TOKEN") or os.getenv("CREWAI_USER_BEARER_TOKEN"))

        if self.args.ai_mode == "remote" and not has_remote_tokens:
            self.add_step(
                "GovernanceAgent",
                "Credential guardrail",
                "warning",
                "Modo remote seleccionado sin token presente en entorno.",
            )
            self.report.recommendations.append(
                "Define CREWAI_BEARER_TOKEN o CREWAI_USER_BEARER_TOKEN antes de usar --ai-mode remote."
            )
        else:
            self.add_step(
                "GovernanceAgent",
                "Credential guardrail",
                "ok",
                "Guardrail de credenciales validado.",
            )

        failed_commands = [command for command in self.report.commands if not command.success]
        if failed_commands:
            self.report.summary_status = "completed-with-errors"
            self.report.recommendations.append(
                "Revisar stdout_tail y stderr_tail de comandos fallidos en el reporte JSON para remediacion automatizada."
            )
        else:
            self.report.summary_status = "completed"

    def save_report(self) -> Path:
        OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
        self.report.finished_at_utc = time.time()

        payload = {
            "started_at_utc": self.report.started_at_utc,
            "finished_at_utc": self.report.finished_at_utc,
            "summary_status": self.report.summary_status,
            "steps": [asdict(step) for step in self.report.steps],
            "commands": [asdict(command) for command in self.report.commands],
            "recommendations": self.report.recommendations,
        }

        self.args.report_file.parent.mkdir(parents=True, exist_ok=True)
        self.args.report_file.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
        return self.args.report_file

    def run(self) -> int:
        self.planner_agent()

        if not self.quality_agent():
            self.governance_agent()
            report_path = self.save_report()
            print(f"Automation report: {report_path}")
            return 1

        if not self.simulation_ops_agent():
            self.governance_agent()
            report_path = self.save_report()
            print(f"Automation report: {report_path}")
            return 1

        self.governance_agent()
        report_path = self.save_report()
        print(f"Automation report: {report_path}")
        return 0 if self.report.summary_status == "completed" else 1


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Agentic project automation for Dune Arrakis Dominion Distributed"
    )
    parser.add_argument("--skip-build", action="store_true", help="Skip dotnet build phase")
    parser.add_argument("--skip-tests", action="store_true", help="Skip dotnet test phase")
    parser.add_argument("--skip-ai", action="store_true", help="Skip AI monthly decision automation phase")
    parser.add_argument(
        "--ai-mode",
        choices=["local", "remote"],
        default="local",
        help="Execution mode for monthly decision automation script",
    )
    parser.add_argument(
        "--wait-remote",
        action="store_true",
        help="Wait for remote completion when ai-mode is remote",
    )
    parser.add_argument(
        "--continue-on-error",
        action="store_true",
        help="Continue pipeline even if a phase fails",
    )
    parser.add_argument(
        "--report-file",
        type=Path,
        default=DEFAULT_REPORT_FILE,
        help="Path to write JSON automation report",
    )
    parser.add_argument(
        "--monthly-actions-file",
        type=Path,
        default=OUTPUT_DIR / "monthly_actions.json",
        help="Path to monthly actions JSON generated by AI automation",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    orchestrator = AgenticAutomation(args)
    return orchestrator.run()


if __name__ == "__main__":
    raise SystemExit(main())
