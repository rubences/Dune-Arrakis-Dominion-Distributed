#!/usr/bin/env python3
import json
from http.server import BaseHTTPRequestHandler, HTTPServer
from threading import Thread
from urllib.parse import urlparse, parse_qs
import time

state = {"saves": {}}

def response(handler, code, payload):
    body = json.dumps(payload).encode()
    handler.send_response(code)
    handler.send_header("Content-Type", "application/json")
    handler.send_header("Content-Length", str(len(body)))
    handler.end_headers()
    handler.wfile.write(body)

class SimHandler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path == "/api/simulation/health":
            return response(self, 200, {"status": "healthy", "service": "mock-simulation"})
        if self.path == "/api/simulation/ai/health":
            return response(self, 200, {"configured": False, "status": "mock"})
        if self.path == "/api/simulation/ai/inputs":
            return response(self, 200, ["game_state", "prompt"])
        return response(self, 404, {"error": "not found"})

    def do_POST(self):
        parsed = urlparse(self.path)
        if parsed.path == "/api/simulation/new-game":
            q = parse_qs(parsed.query)
            save_name = q.get("saveName", ["mock-save"])[0]
            return response(self, 200, {"saveName": save_name, "activeScenario": {"name": "Mock", "currentMonth": 1, "eventLog": []}})
        if parsed.path == "/api/simulation/process-month":
            return response(self, 200, {"month": 1, "currentSolaris": 25000, "events": []})
        return response(self, 404, {"error": "not found"})

class PersistHandler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path == "/api/gamestate/health":
            return response(self, 200, {"status": "healthy", "service": "mock-persistence"})
        if self.path == "/api/gamestate/list":
            return response(self, 200, list(state["saves"].keys()))
        if self.path.startswith("/api/gamestate/load/"):
            key = self.path.rsplit("/", 1)[-1]
            if key in state["saves"]:
                return response(self, 200, state["saves"][key])
            return response(self, 404, {"error": "not found"})
        return response(self, 404, {"error": "not found"})

    def do_POST(self):
        if self.path == "/api/gamestate/save":
            length = int(self.headers.get("Content-Length", "0"))
            body = json.loads(self.rfile.read(length) or b"{}")
            key = body.get("saveName", f"save-{int(time.time())}")
            state["saves"][key] = body
            return response(self, 200, {"message": f"saved {key}"})
        return response(self, 404, {"error": "not found"})


def serve(port, handler):
    server = HTTPServer(("0.0.0.0", port), handler)
    server.serve_forever()

if __name__ == "__main__":
    t1 = Thread(target=serve, args=(5200, SimHandler), daemon=True)
    t2 = Thread(target=serve, args=(5100, PersistHandler), daemon=True)
    t1.start(); t2.start()
    while True:
        time.sleep(3600)
