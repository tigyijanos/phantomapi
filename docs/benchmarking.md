# Benchmarking

If you would like Copilot to perform the benchmarking ritual on your behalf, try:

```text
Clone https://github.com/tigyijanos/phantomapi, figure out how to run the BenchmarkDotNet suite plus the warm benchmark harness, run the fastest safe benchmark command first, and tell me where the results land.
```

PhantomAPI now has two benchmark layers:

- `benchmarks/PhantomApi.Benchmarks`
  microbenchmarks for local C# hotspots with `BenchmarkDotNet`
- `scripts/benchmark-warm.ps1`
  end-to-end latency and warm/cold runtime experiments

The manual old-school way, for people who still prefer typing their own benchmark commands:

Run the `BenchmarkDotNet` suite in release mode:

```bash
dotnet run -c Release --project benchmarks/PhantomApi.Benchmarks/PhantomApi.Benchmarks.csproj
```

Current microbench targets focus on deterministic runtime setup work:

- instruction bundle compilation
- endpoint contract and schema resolution
- endpoint frontmatter parsing and warm-start discovery

Use the PowerShell harness when you want request-level timing, trace capture, and variant comparisons against the live HTTP entrypoint.
