#!/usr/bin/env python3
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import argparse
import mimetypes
import os


class WebGLHandler(SimpleHTTPRequestHandler):
    def end_headers(self):
        if self.path.endswith(".js.br"):
            self.send_header("Content-Encoding", "br")
            self.send_header("Content-Type", "application/javascript")
        elif self.path.endswith(".wasm.br"):
            self.send_header("Content-Encoding", "br")
            self.send_header("Content-Type", "application/wasm")
        elif self.path.endswith(".data.br"):
            self.send_header("Content-Encoding", "br")
            self.send_header("Content-Type", "application/octet-stream")
        elif self.path.endswith(".symbols.json.br"):
            self.send_header("Content-Encoding", "br")
            self.send_header("Content-Type", "application/json")
        self.send_header("Cache-Control", "no-cache")
        super().end_headers()

    def guess_type(self, path):
        if path.endswith(".js.br"):
            return "application/javascript"
        if path.endswith(".wasm.br"):
            return "application/wasm"
        if path.endswith(".data.br"):
            return "application/octet-stream"
        if path.endswith(".symbols.json.br"):
            return "application/json"
        return mimetypes.guess_type(path)[0] or "application/octet-stream"


def main():
    parser = argparse.ArgumentParser(description="Serve Unity WebGL output with Brotli headers.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8001)
    parser.add_argument(
        "--root",
        default=str(Path(__file__).resolve().parents[1] / "Builds" / "WebGL"),
    )
    args = parser.parse_args()

    os.chdir(args.root)
    server = ThreadingHTTPServer((args.host, args.port), WebGLHandler)
    print(f"Serving {args.root} at http://{args.host}:{args.port}/")
    server.serve_forever()


if __name__ == "__main__":
    main()
