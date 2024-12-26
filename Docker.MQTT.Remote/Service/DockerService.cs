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

    public async Task GetStats(string id)
    {
        logger.LogInformation($"Sending stats for {id}");
        await _client.Containers.GetContainerStatsAsync(id, new ContainerStatsParameters() { OneShot = true, Stream = false},
            new Progress<ContainerStatsResponse>(
                response =>
                {

                    logger.LogInformation($"Got stats for {id}");
                    
                    double cpuPercent = 0;
                    var cpuStats2 = response.CPUStats;

                    if (_cpuUsageCache.TryGetValue(id, out var cpuStats1))
                    {

                        ulong cpuDelta = cpuStats2.CPUUsage.TotalUsage - cpuStats1.CPUUsage.TotalUsage;
                        ulong systemDelta = cpuStats2.SystemUsage - cpuStats1.SystemUsage;

                        if (systemDelta > 0)
                        {
                            // Calculate CPU usage percentage
                            cpuPercent = ((cpuDelta / (double)systemDelta) * (float)cpuStats2.OnlineCPUs) * 100;
                        }

                    }

                    _cpuUsageCache.AddOrUpdate(id, response.CPUStats, (s, usage) => response.CPUStats);

                    var memory = new ContainerMemory(response.MemoryStats);
                    var stats = new ContainerStats(id, memory, Math.Round(cpuPercent, 2));
                    logger.LogTrace(JsonConvert.SerializeObject(stats, Formatting.None));

                    OnStats.Invoke(null, new StatsEventArgs { ContainerStats = stats, Id = id });

                }), CancellationToken.None);
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