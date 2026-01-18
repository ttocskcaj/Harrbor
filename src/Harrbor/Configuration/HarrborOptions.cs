namespace Harrbor.Configuration;

public class HarrborOptions
{
    public const string SectionName = "Harrbor";

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(1);
    public int TransferParallelism { get; set; } = 2;
    public string DataPath { get; set; } = "./data";
}
