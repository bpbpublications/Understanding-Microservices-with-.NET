"""Compare a SQLite basket read with a 60-second Redis read-through cache.
Requires Python 3 and 'python -m pip install redis'. Use a local lab Redis server.
"""
import json
import os
import sqlite3
import statistics
import time
import uuid

import redis

db = sqlite3.connect(":memory:")
db.execute("CREATE TABLE basket(buyer TEXT PRIMARY KEY, payload TEXT NOT NULL)")
db.execute("INSERT INTO basket VALUES (?, ?)", ("reader", json.dumps({"items": [{"id": 1, "qty": 2}]})))
cache = redis.Redis.from_url(os.getenv("REDIS_URL", "redis://localhost:6379"), decode_responses=True)
key = f"book-lab:{uuid.uuid4()}:reader"

def measure(use_cache, count=500):
    elapsed = []
    reads = 0
    for _ in range(count):
        start = time.perf_counter_ns()
        value = cache.get(key) if use_cache else None
        if value is None:
            value = db.execute("SELECT payload FROM basket WHERE buyer=?", ("reader",)).fetchone()[0]
            reads += 1
            if use_cache:
                cache.setex(key, 60, value)
        assert json.loads(value)["items"][0]["qty"] == 2
        elapsed.append((time.perf_counter_ns() - start) / 1000)
    return statistics.median(elapsed), reads

try:
    base, old_reads = measure(False)
    cached, new_reads = measure(True)
    print(f"median μs: {base:.1f} -> {cached:.1f}; DB reads: {old_reads} -> {new_reads}")
    print(f"median improvement: {100*(base-cached)/base:.1f}%")
finally:
    cache.delete(key)
    db.close()
