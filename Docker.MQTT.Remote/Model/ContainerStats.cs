using Docker.MQTT.Remote.Model.Stats;

namespace Docker.MQTT.Remote.Model;

public record ContainerStats(string Id, ContainerMemory Memory, double cpuPercent);