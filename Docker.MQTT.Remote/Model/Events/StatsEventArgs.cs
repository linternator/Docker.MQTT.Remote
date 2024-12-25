namespace Docker.MQTT.Remote.Model.Events;

public class StatsEventArgs : EventArgs
{
    public ContainerStats ContainerStats { get; set; }
    public object Id { get; set; }
}