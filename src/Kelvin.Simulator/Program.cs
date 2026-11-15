using Kelvin.Simulator;

var options = SimulatorOptions.Parse(args);
using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

var simulator = new GatewaySimulator(
    options,
    new SensorFleet(options.SensorCount, options.BaseTemperatureC),
    new HmiFleet(options.HmiCount, options.BaseTemperatureC),
    new AmbientSimulation(options.BaseTemperatureC)
);
await simulator.RunAsync(cancellationSource.Token);
