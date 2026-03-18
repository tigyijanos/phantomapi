sealed class PhantomOptions
{
    public string CliCommand { get; set; } = string.Empty;
    public string CliArgumentsTemplate { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ReasoningEffort { get; set; } = string.Empty;
    public int CliTimeoutSeconds { get; set; }
    public int WarmTurnGraceSeconds { get; set; }
    public bool UseWarmAppServer { get; set; }
    public bool UseExecSessionPool { get; set; } = true;
    public bool FallbackToColdExecution { get; set; } = true;
    public bool FastModeEnabled { get; set; }
    public string FastModeModel { get; set; } = string.Empty;
    public string FastModeReasoningEffort { get; set; } = string.Empty;
    public string? FastModeServiceTier { get; set; }
    public string? NormalServiceTier { get; set; }
}
