using Kelvin.Simulator;

var options = SimulatorOptions.Parse(args);
using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

var simulator = new GatewaySimulator(options);
await simulator.RunAsync(cancellationSource.Token);
