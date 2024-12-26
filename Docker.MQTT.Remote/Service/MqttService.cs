using System.Data;
using System.Text;
using Docker.MQTT.Remote.Model;
using Docker.MQTT.Remote.Model.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Docker.MQTT.Remote.Service;

public class MqttService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MqttService> _logger;
    private readonly IMqttClient _mqttClient;
    private readonly MqttClientFactory _mqttFactory;
    
    public EventHandler<IdEventArgs> StartContainerReceived;
    public EventHandler<IdEventArgs> StopContainerReceived;
    
    public EventHandler StatusRequestReceived;
    public EventHandler StatsRequestReceived;

    public MqttService(IConfiguration configuration, ILogger<MqttService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        _mqttFactory = new MqttClientFactory();
        _mqttClient = _mqttFactory.CreateMqttClient();
        
        _mqttClient.ApplicationMessageReceivedAsync += MqttClientOnApplicationMessageReceivedAsync;

    }

    private Task MqttClientOnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        _logger.LogTrace("Received application message.");
        var data = EncodingExtensions.GetString(Encoding.UTF8, arg.ApplicationMessage.Payload);

        try
        {
            
            if (arg.ApplicationMessage.Topic.EndsWith("/stop")) {
                
                _logger.LogTrace("Stopping Docker service.");
                StopContainerReceived.Invoke(null, new IdEventArgs() { Id = GetId(data) });
                
            } 
            else if (arg.ApplicationMessage.Topic.EndsWith("/start")) {
                
                _logger.LogTrace("Starting Docker service.");
                StartContainerReceived.Invoke(null, new IdEventArgs() { Id = GetId(data) });
                
            } else if (arg.ApplicationMessage.Topic.EndsWith("/status/request")) {
                
                StatusRequestReceived.Invoke(null, null);
                
            } else if (arg.ApplicationMessage.Topic.EndsWith("/stats/request")) {
                
                StatsRequestReceived.Invoke(null, null);
                
            }
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
        }
        
        return Task.CompletedTask;
    }

    private string GetId(string data)
    {
        JObject json = JObject.Parse(data);
        if(json["id"] == null) throw new Exception("Invalid MQTT Payload.");
        var id = json["id"]!.ToString();
        return id;
    }

    public async Task Connect()
    {
        
        _logger.LogInformation($"Connecting to MQTT broker on {_configuration["Mqtt:Host"]}:{_configuration["Mqtt:Port"]}");
        
        var mqttClientOptions = new MqttClientOptionsBuilder().WithTcpServer(_configuration["Mqtt:Host"], Convert.ToInt32(_configuration["Mqtt:Port"])).Build();
        await _mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);
        
        var mqttSubscribeOptionsStart = _mqttFactory.CreateSubscribeOptionsBuilder().WithTopicFilter($"{_configuration["Mqtt:Topic"]}/start").Build();
        await _mqttClient.SubscribeAsync(mqttSubscribeOptionsStart, CancellationToken.None);
        
        var mqttSubscribeOptionsStop = _mqttFactory.CreateSubscribeOptionsBuilder().WithTopicFilter($"{_configuration["Mqtt:Topic"]}/stop").Build();
        await _mqttClient.SubscribeAsync(mqttSubscribeOptionsStop, CancellationToken.None);
        
        var mqttSubscribeOptionsStatus = _mqttFactory.CreateSubscribeOptionsBuilder().WithTopicFilter($"{_configuration["Mqtt:Topic"]}/status/request").Build();
        await _mqttClient.SubscribeAsync(mqttSubscribeOptionsStatus, CancellationToken.None);
        
        var mqttSubscribeOptionsStats = _mqttFactory.CreateSubscribeOptionsBuilder().WithTopicFilter($"{_configuration["Mqtt:Topic"]}/stats/request").Build();
        await _mqttClient.SubscribeAsync(mqttSubscribeOptionsStats, CancellationToken.None);
        
    }

    public async Task SendContainerStatus(List<ContainerStatus> containers)
    {
        var applicationMessage = new MqttApplicationMessageBuilder()
            .WithTopic($"{_configuration["Mqtt:Topic"]}/status")
            .WithPayload(JsonConvert.SerializeObject(containers))
            .Build();

        await _mqttClient.PublishAsync(applicationMessage, CancellationToken.None);
    }

    public async Task SendContainerStats(object id, ContainerStats stats)
    {
        var applicationMessage = new MqttApplicationMessageBuilder()
            .WithTopic($"{_configuration["Mqtt:Topic"]}/state")
            .WithPayload(JsonConvert.SerializeObject(stats))
            .Build();

        await _mqttClient.PublishAsync(applicationMessage, CancellationToken.None);
    }
}