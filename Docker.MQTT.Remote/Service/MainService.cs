using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Docker.MQTT.Remote.Service;

public class MainService(ILogger<MainService> logger, DockerService dockerService, MqttService mqttService) : IHostedService, IDisposable
{
    private Timer? _timer;
    private Dictionary<string, CancellationTokenSource> _trackingList = new();

    public async Task StartAsync(CancellationToken cancellationToken)
    {

        await mqttService.Connect();
        
        _timer = new Timer(SendStatus, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

        mqttService.StopContainerReceived += async (sender, args) =>
        {
            await dockerService.StopContainer(args.Id);
        };

        mqttService.StartContainerReceived += async (sender, args) =>
        {
            await dockerService.StartContainer(args.Id);
        };

        dockerService.OnStats += async (sender, args) =>
        {
            await mqttService.SendContainerStats(args.Id, args.ContainerStats);
        };

    }

    private async void SendStatus(object? state)
    {
        try
        {
            var containers = await dockerService.GetContainers();
            
            logger.LogInformation("Sending status to containers.");
            logger.LogTrace(JsonConvert.SerializeObject(containers, Formatting.None));
            
            await mqttService.SendContainerStatus(containers);

            foreach (var listKey in _trackingList.Keys.Where(listKey => containers.All(x => x.Id != listKey)))
            {
                await _trackingList[listKey].CancelAsync();
                _trackingList.Remove(listKey);
            }
            
            foreach (var container in containers)
            {
                if (_trackingList.ContainsKey(container.Id)) continue;
                
                var cts = new CancellationTokenSource();
                dockerService.StartStatusStream(container.Id, cts.Token);
                _trackingList.Add(container.Id, cts);
            }
            
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}