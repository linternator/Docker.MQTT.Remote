// See https://aka.ms/new-console-template for more information

using Docker.MQTT.Remote.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

var builder = Host.CreateDefaultBuilder(args);
builder.UseSerilog();

builder.ConfigureServices(collection =>
{
    collection.AddSerilog();

    collection.AddSingleton<DockerService>();
    collection.AddSingleton<MqttService>();

    collection.AddHostedService<MainService>();

});

await builder.RunConsoleAsync();