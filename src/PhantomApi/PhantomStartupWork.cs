sealed record PhantomStartupWork(
    CodexAppServerClient? AppServerClient,
    Func<CancellationToken, Task> PrimeWarmRuntimeAsync,
    Func<CancellationToken, Task> WarmConfiguredEndpointsAsync);
