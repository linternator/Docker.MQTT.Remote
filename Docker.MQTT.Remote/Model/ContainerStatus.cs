namespace Docker.MQTT.Remote.Model;

public record ContainerStatus (List<string> Names, string Id, string Command, string Image, string Status, string State);