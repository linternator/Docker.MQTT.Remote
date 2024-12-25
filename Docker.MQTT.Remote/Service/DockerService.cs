using System.Collections.Concurrent;
using System.Net.Http.Json;
using Docker.DotNet;
using Docker.DotNet.Models;
using Docker.MQTT.Remote.Model;
using Docker.MQTT.Remote.Model.Events;
using Docker.MQTT.Remote.Model.Stats;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ContainerStatus = Docker.MQTT.Remote.Model.ContainerStatus;

namespace Docker.MQTT.Remote.Service;

public class DockerService(ILogger<DockerService> logger)
{
    private readonly DockerClient _client = new DockerClientConfiguration().CreateClient();
    private ConcurrentDictionary<string, CPUStats> _cpuUsageCache = new ();
    
    public EventHandler<StatsEventArgs> OnStats;

    public void StartStatusStream(string id, CancellationToken ctsToken)
    {
        logger.LogInformation($"Starting status stream for {id}");
        Task.Run(() =>
            _client.Containers.GetContainerStatsAsync(id, new ContainerStatsParameters(),
                new Progress<ContainerStatsResponse>(
                    response =>
                    {

                        double cpuPercent = 0;
                        var cpuStats2 = response.CPUStats;
                        
                        if (_cpuUsageCache.TryGetValue(response.ID, out var cpuStats1))
                        {
                            
                            ulong cpuDelta = cpuStats2.CPUUsage.TotalUsage - cpuStats1.CPUUsage.TotalUsage;
                            ulong systemDelta = cpuStats2.SystemUsage - cpuStats1.SystemUsage;

                            if (systemDelta > 0)
                            {
                                // Calculate CPU usage percentage
                                cpuPercent = ((cpuDelta / (double)systemDelta) * (float)cpuStats2.OnlineCPUs) * 100;
                            }
                            
                        }
                        
                        _cpuUsageCache.AddOrUpdate(response.ID, response.CPUStats, (s, usage) => response.CPUStats);
                        
                        var memory = new ContainerMemory(response.MemoryStats);
                        var stats = new ContainerStats(response.ID, memory, Math.Round(cpuPercent,2));
                        logger.LogTrace(JsonConvert.SerializeObject(stats, Formatting.None));
                        
                        OnStats.Invoke(null, new StatsEventArgs {ContainerStats = stats, Id = response.ID});
                        
                    }), ctsToken), ctsToken);
    }

    public async Task<List<ContainerStatus>> GetContainers()
    {
        var containers = await _client.Containers.ListContainersAsync(new ContainersListParameters(){All = true}, CancellationToken.None);
        return containers.Select(response => new ContainerStatus(response.Names.ToList(), response.ID, response.Command, response.Image, response.Status,response.State)).ToList();
    }

    public async Task StopContainer(string id)
    {
        await _client.Containers.StopContainerAsync(id, new ContainerStopParameters(), CancellationToken.None);
    }

    public async Task StartContainer(string id)
    {
        await _client.Containers.StartContainerAsync(id, new ContainerStartParameters(), CancellationToken.None);
    }
}