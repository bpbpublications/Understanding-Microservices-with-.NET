-- wrk records response times in microseconds.
function done(summary, latency)
  io.write(string.format("p95: %.3f ms\n", latency:percentile(95) / 1000))
end
