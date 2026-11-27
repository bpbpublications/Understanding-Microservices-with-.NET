"""Compare process reuse and clean starts for the Chapter 21 recommendation handler."""
import json
import math
import statistics
import subprocess
import sys
import time
from pathlib import Path

PILOT = Path(__file__).resolve().parents[1] / "pilot"
sys.path.insert(0, str(PILOT))
from app import handler

EVENT = {"queryStringParameters": {"category": "architecture"}}
EXPECTED = ["distributed-systems", "observability"]


def invoke():
    response = handler(EVENT, None)
    return response["statusCode"] == 200 and json.loads(response["body"])["recommendations"] == EXPECTED


def start_new():
    result = subprocess.run([sys.executable, __file__, "--once"], check=False)
    return result.returncode == 0


def measure(name, call, count):
    samples = []
    correct = 0
    for _ in range(count):
        start = time.perf_counter_ns()
        correct += call()
        samples.append((time.perf_counter_ns() - start) / 1_000_000)
    p95 = sorted(samples)[math.ceil(count * 0.95) - 1]
    print(f"{name}: {correct}/{count} correct, median {statistics.median(samples):.3f} ms, p95 {p95:.3f} ms")
    return correct == count


if __name__ == "__main__":
    if sys.argv[1:] == ["--once"]:
        sys.exit(0 if invoke() else 1)
    invoke()  # Warm up the resident interpreter before collecting its samples.
    warm = measure("Resident Python process", invoke, 1000)
    cold = measure("New Python process per call", start_new, 100)
    sys.exit(0 if warm and cold else 1)
