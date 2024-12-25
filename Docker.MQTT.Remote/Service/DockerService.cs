using Docker.DotNet;
using Docker.DotNet.Models;
using ContainerStatus = Docker.MQTT.Remote.Model.ContainerStatus;

namespace Docker.MQTT.Remote.Service;

public class DockerService
{
    private readonly DockerClient _client = new DockerClientConfiguration().CreateClient();

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