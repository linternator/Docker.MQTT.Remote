using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Docker.MQTT.Remote.Service;

public class MainService(ILogger<MainService> logger, IConfiguration configuration, DockerService dockerService, MqttService mqttService) : IHostedService, IDisposable
{
    private Timer? _statusTimer;
    private Timer? _statsTimer;

    public async Task StartAsync(CancellationToken cancellationToken)
    {

        await mqttService.Connect();

        if (configuration["Frequency:Status"] != "0")
        {
            _statusTimer = new Timer(SendStatus, null, TimeSpan.Zero, TimeSpan.FromSeconds(Convert.ToInt32(configuration["Frequency:Status"])));
        }
        else
        {
            logger.LogInformation("Status frequency set to 0. Disabling status.");
        }

        if (configuration["Frequency:Stats"] != "0")
        {
            _statsTimer = new Timer(SendStats, null, TimeSpan.Zero, TimeSpan.FromSeconds(Convert.ToInt32(configuration["Frequency:Stats"])));
        }
        else
        {
            logger.LogInformation("Stats frequency set to 0. Disabling stats.");
        }

        mqttService.StopContainerReceived += async (sender, args) =>
        {
            await dockerService.StopContainer(args.Id);
        };

        mqttService.StartContainerReceived += async (sender, args) =>
        {
            await dockerService.StartContainer(args.Id);
        };

        mqttService.StatsRequestReceived += async (sender, args) =>
        {
            SendStats(null);
        };
        
        mqttService.StatusRequestReceived += async (sender, args) =>
        {
            SendStatus(null);
        };

        dockerService.OnStats += async (sender, args) =>
        {
            await mqttService.SendContainerStats(args.Id, args.ContainerStats);
        };

    }

    private async void SendStats(object? state)
    {
        var containers = await dockerService.GetContainers();

        foreach (var container in containers.Where(container => container.State == "running"))
        {
            await dockerService.GetStats(container.Id);
        }
    }

    private async void SendStatus(object? state)
    {
        try
        {
            var containers = await dockerService.GetContainers();
            
            logger.LogInformation("Sending status to containers.");
            logger.LogTrace(JsonConvert.SerializeObject(containers, Formatting.None));
            
            await mqttService.SendContainerStatus(containers);
            
        }
        catch (Exception e)
        {
            logger.LogError(e, e.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _statusTimer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _statusTimer?.Dispose();
    }
}