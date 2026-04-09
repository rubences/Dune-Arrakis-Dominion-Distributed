from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import time
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Any
from xml.etree import ElementTree


REPO_ROOT = Path(__file__).resolve().parents[1]
OUTPUT_DIR = REPO_ROOT / "automation" / "output"
DEFAULT_REPORT_FILE = OUTPUT_DIR / "agentic_automation_report.json"
DEFAULT_HISTORY_FILE = OUTPUT_DIR / "agentic_automation_history.json"
DEFAULT_TEST_RESULTS_FILE = OUTPUT_DIR / "agentic-tests.trx"


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
    execution_profile: dict[str, Any] = field(default_factory=dict)
    remediation: dict[str, Any] = field(default_factory=dict)
    history_snapshot: dict[str, Any] = field(default_factory=dict)


class AgenticAutomation:
    def __init__(self, args: argparse.Namespace) -> None:
        self.args = args
        self.report = AutomationReport(started_at_utc=time.time())
        self.history = self.load_history()
        self.execution_profile = self.build_execution_profile()
        self.report.execution_profile = self.execution_profile
        self.report.history_snapshot = {
            "history_file": str(self.args.history_file),
            "runs_available": len(self.history.get("runs", [])),
        }

    def load_history(self) -> dict[str, Any]:
        if not self.args.history_file.exists():
            return {"runs": []}

        try:
            return json.loads(self.args.history_file.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            self.add_step(
                "PlannerAgent",
                "Load history",
                "warning",
                "No se pudo parsear el historial; se inicia historial limpio.",
            )
            return {"runs": []}

    def build_execution_profile(self) -> dict[str, Any]:
        run_build = not self.args.skip_build
        run_tests = not self.args.skip_tests
        run_ai = not self.args.skip_ai
        auto_remediate = self.args.auto_remediate

        if not self.args.adaptive:
            return {
                "adaptive": False,
                "run_build": run_build,
                "run_tests": run_tests,
                "run_ai": run_ai,
                "auto_remediate": auto_remediate,
                "reasons": ["Adaptive mode disabled"],
            }

        reasons: list[str] = []
        recent_runs = self.history.get("runs", [])[-5:]

        def failed_in(command_name: str) -> int:
            count = 0
            for run in recent_runs:
                failed_commands = run.get("failed_commands", [])
                if command_name in failed_commands:
                    count += 1
            return count

        build_failures = failed_in("dotnet-build")
        test_failures = failed_in("dotnet-test")
        ai_failures = failed_in("monthly-decision-automation")

        if build_failures > 0 and self.args.skip_build:
            run_build = True
            reasons.append("Build forzado por fallos historicos recientes")

        if test_failures > 0 and self.args.skip_tests:
            run_tests = True
            reasons.append("Tests forzados por fallos historicos recientes")

        if test_failures > 0 and not auto_remediate:
            auto_remediate = True
            reasons.append("Auto-remediacion habilitada por historial de fallos en tests")

        has_remote_tokens = bool(os.getenv("CREWAI_BEARER_TOKEN") or os.getenv("CREWAI_USER_BEARER_TOKEN"))
        if ai_failures >= 2 and self.args.ai_mode == "remote" and not has_remote_tokens:
            run_ai = False
            reasons.append("Fase AI remota omitida: fallos historicos + falta de token")

        if not reasons:
            reasons.append("Sin ajustes adaptativos necesarios")

        return {
            "adaptive": True,
            "recent_runs_considered": len(recent_runs),
            "run_build": run_build,
            "run_tests": run_tests,
            "run_ai": run_ai,
            "auto_remediate": auto_remediate,
            "build_failures_recent": build_failures,
            "test_failures_recent": test_failures,
            "ai_failures_recent": ai_failures,
            "reasons": reasons,
        }

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
        self.add_step(
            "PlannerAgent",
            "Adaptive execution profile",
            "ok",
            json.dumps(self.execution_profile, ensure_ascii=False),
        )

    def parse_failed_tests_from_trx(self, trx_file: Path) -> list[str]:
        if not trx_file.exists():
            return []

        try:
            root = ElementTree.parse(trx_file).getroot()
        except ElementTree.ParseError:
            return []

        failed_names: list[str] = []
        for result in root.findall(".//{*}UnitTestResult"):
            if (result.attrib.get("outcome") or "").lower() != "failed":
                continue
            test_name = result.attrib.get("testName", "").strip()
            if test_name:
                failed_names.append(test_name)

        return sorted(set(failed_names))

    def build_test_filter_from_failed(self, failed_tests: list[str]) -> str:
        parts: list[str] = []
        for test_name in failed_tests[:20]:
            method = test_name.split(".")[-1]
            clean = re.sub(r"[^A-Za-z0-9_]", "", method)
            if clean:
                parts.append(f"Name~{clean}")
        return "|".join(parts)

    def run_test_remediation(self, initial_failed_tests: list[str]) -> dict[str, Any]:
        remediation = {
            "enabled": bool(self.execution_profile.get("auto_remediate", False)),
            "attempted": False,
            "initial_failed_tests": initial_failed_tests,
            "initial_failed_count": len(initial_failed_tests),
            "rerun_success": None,
            "rerun_filter": "",
            "rerun_exit_code": None,
            "remaining_failed_tests": [],
            "remaining_failed_count": 0,
        }

        if not remediation["enabled"] or not initial_failed_tests:
            return remediation

        rerun_filter = self.build_test_filter_from_failed(initial_failed_tests)
        if not rerun_filter:
            return remediation

        remediation["attempted"] = True
        remediation["rerun_filter"] = rerun_filter

        rerun_results_file = self.args.test_results_file.with_name("agentic-tests-rerun.trx")
        self.add_step(
            "QualityAgent",
            "Selective test rerun",
            "running",
            f"Filter={rerun_filter}",
        )

        rerun = self.run_command(
            "dotnet-test-rerun",
            [
                "dotnet",
                "test",
                "DuneArrakisDominion.slnx",
                "-c",
                "Release",
                "--no-build",
                "--verbosity",
                "minimal",
                "--results-directory",
                str(rerun_results_file.parent),
                "--logger",
                f"trx;LogFileName={rerun_results_file.name}",
                "--filter",
                rerun_filter,
            ],
        )

        remaining_failed = self.parse_failed_tests_from_trx(rerun_results_file)
        remediation["rerun_success"] = rerun.success
        remediation["rerun_exit_code"] = rerun.exit_code
        remediation["remaining_failed_tests"] = remaining_failed
        remediation["remaining_failed_count"] = len(remaining_failed)

        self.add_step(
            "QualityAgent",
            "Selective test rerun",
            "ok" if rerun.success else "error",
            f"Remaining failed tests: {len(remaining_failed)}",
        )

        return remediation

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

        if self.execution_profile.get("run_build", True):
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
        else:
            self.add_step("QualityAgent", "dotnet build", "skipped", "Skipped by adaptive profile")

        if self.execution_profile.get("run_tests", True):
            self.add_step("QualityAgent", "dotnet test", "running", "Running automated tests.")

            self.args.test_results_file.parent.mkdir(parents=True, exist_ok=True)
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
                    "--results-directory",
                    str(self.args.test_results_file.parent),
                    "--logger",
                    f"trx;LogFileName={self.args.test_results_file.name}",
                ],
            )
            self.add_step(
                "QualityAgent",
                "dotnet test",
                "ok" if tests.success else "error",
                f"Exit code: {tests.exit_code}",
            )

            failed_tests = self.parse_failed_tests_from_trx(self.args.test_results_file)
            self.report.remediation = self.run_test_remediation(failed_tests)

            if self.report.remediation.get("attempted"):
                remaining_failed = int(self.report.remediation.get("remaining_failed_count", 0))
                if remaining_failed == 0:
                    self.report.recommendations.append(
                        "La remediacion automatica estabilizo los tests fallidos en el rerun selectivo."
                    )
                else:
                    self.report.recommendations.append(
                        "Persisten tests fallidos tras remediacion automatica; revisar filtro de rerun y causas raiz."
                    )

            if not tests.success and not self.args.continue_on_error:
                # If remediation fixed all previously failing tests, keep pipeline alive.
                remaining_failed = int(self.report.remediation.get("remaining_failed_count", len(failed_tests)))
                if not self.report.remediation.get("attempted") or remaining_failed > 0:
                    return False
        else:
            self.add_step("QualityAgent", "dotnet test", "skipped", "Skipped by adaptive profile")

        return True

    def simulation_ops_agent(self) -> bool:
        if not self.execution_profile.get("run_ai", True):
            self.add_step(
                "SimulationOpsAgent",
                "AI monthly automation",
                "skipped",
                "Skipped by adaptive profile",
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
            "execution_profile": self.report.execution_profile,
            "remediation": self.report.remediation,
            "history_snapshot": self.report.history_snapshot,
        }

        self.args.report_file.parent.mkdir(parents=True, exist_ok=True)
        self.args.report_file.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
        return self.args.report_file

    def persist_history(self) -> Path:
        self.args.history_file.parent.mkdir(parents=True, exist_ok=True)

        runs = list(self.history.get("runs", []))
        failed_commands = [command.name for command in self.report.commands if not command.success]
        run_item = {
            "timestamp_utc": self.report.finished_at_utc,
            "summary_status": self.report.summary_status,
            "failed_commands": failed_commands,
            "failed_tests": self.report.remediation.get("remaining_failed_tests", []),
            "execution_profile": self.report.execution_profile,
            "report_file": str(self.args.report_file),
        }
        runs.append(run_item)

        max_runs = max(5, self.args.history_keep)
        trimmed_runs = runs[-max_runs:]
        payload = {
            "schema_version": 1,
            "updated_at_utc": self.report.finished_at_utc,
            "runs": trimmed_runs,
        }
        self.args.history_file.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
        return self.args.history_file

    def run(self) -> int:
        self.planner_agent()

        if not self.quality_agent():
            self.governance_agent()
            report_path = self.save_report()
            history_path = self.persist_history()
            print(f"Automation report: {report_path}")
            print(f"Automation history: {history_path}")
            return 1

        if not self.simulation_ops_agent():
            self.governance_agent()
            report_path = self.save_report()
            history_path = self.persist_history()
            print(f"Automation report: {report_path}")
            print(f"Automation history: {history_path}")
            return 1

        self.governance_agent()
        report_path = self.save_report()
        history_path = self.persist_history()
        print(f"Automation report: {report_path}")
        print(f"Automation history: {history_path}")
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
        "--adaptive",
        action="store_true",
        help="Adjust execution profile using historical failures",
    )
    parser.add_argument(
        "--auto-remediate",
        action="store_true",
        help="Enable selective rerun for failed tests and comparative remediation data",
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
    parser.add_argument(
        "--history-file",
        type=Path,
        default=DEFAULT_HISTORY_FILE,
        help="Path to historical run memory used by adaptive mode",
    )
    parser.add_argument(
        "--history-keep",
        type=int,
        default=30,
        help="Maximum number of historical runs to keep",
    )
    parser.add_argument(
        "--test-results-file",
        type=Path,
        default=DEFAULT_TEST_RESULTS_FILE,
        help="Path to trx test results used for remediation",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    orchestrator = AgenticAutomation(args)
    return orchestrator.run()


if __name__ == "__main__":
    raise SystemExit(main())
