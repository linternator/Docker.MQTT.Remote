using Docker.DotNet.Models;

namespace Docker.MQTT.Remote.Model.Stats;

public record ContainerMemory
{
    
    public ulong Total { get; set; }
    public ulong Used { get; set; }
    public decimal Percent { get; set; }
    
    public ContainerMemory(MemoryStats memoryStats)
    {
        Total = memoryStats.Limit;
        Used = memoryStats.Usage;
        
        if (Total != 0) Percent = Math.Round(((decimal)Used / Total) * 100, 2);
    }
}