using System.IO.Ports;

namespace Kelvin.Simulator;

internal sealed class GatewayConnection : IDisposable
{
    private readonly SerialPort port;
    private readonly GatewayProtocol protocol = new();

    public GatewayConnection(string portName)
    {
        port = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            NewLine = "\n",
            ReadTimeout = 250,
            WriteTimeout = 250,
        };
    }

    public void Open() => port.Open();

    public void Write(byte[] frame) => port.Write(frame, 0, frame.Length);

    public void ReadIncoming(Action<byte[], byte[]> receive)
    {
        var available = port.BytesToRead;
        if (available > 0)
        {
            var bytes = new byte[available];
            var read = port.Read(bytes, 0, available);
            protocol.Append(bytes.AsSpan(0, read));
        }

        while (protocol.TryRead(out var input))
        {
            switch (input)
            {
                case GatewayInfoRequest:
                    Write(GatewayProtocol.EncodeGatewayInfo());
                    break;
                case GatewayDownlink downlink:
                    receive(downlink.MacAddress, downlink.Payload);
                    break;
            }
        }
    }

    public void Dispose() => port.Dispose();
}
