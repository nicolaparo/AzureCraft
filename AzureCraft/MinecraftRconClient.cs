using System.Net.Sockets;
using System.Text;

namespace AzureCraft
{
    public class MinecraftRconClient : IDisposable
    {
        private const int MaxMessageSize = 4110;

        private TcpClient client;
        private NetworkStream connection;
        private int lastMessageId = 0;

        public MinecraftRconClient(string host, int port)
        {
            client = new TcpClient(host, port);
            connection = client.GetStream();
        }

        public void Dispose()
        {
            Close();
        }

        public void Close()
        {
            connection.Close();
            client.Close();
        }

        public async Task<bool> AuthenticateAsync(string password)
        {
            var result = await sendMessageAsync(new RconMessage(
                password.Length + MinecraftRconMessageSerializer.HeaderLength,
                Interlocked.Increment(ref lastMessageId),
                RconMessageType.Authenticate,
                password
            ));

            return result.Success;
        }

        public async Task<RconMessageResponse> SendCommandAsync(string command)
        {
            return await sendMessageAsync(new RconMessage(
                command.Length + MinecraftRconMessageSerializer.HeaderLength,
                Interlocked.Increment(ref lastMessageId),
                RconMessageType.Command,
                command
            ));
        }

        private async Task<RconMessageResponse> sendMessageAsync(RconMessage req)
        {
            byte[] encoded = MinecraftRconMessageSerializer.Serialize(req);

            await connection.WriteAsync(encoded, 0, encoded.Length);
            await connection.FlushAsync();

            var responses = new List<RconMessage>();

            CancellationTokenSource cts = new();
            cts.CancelAfter(1000);

            do
            {
                byte[] respBytes = new byte[MaxMessageSize];
                int bytesRead = await connection.ReadAsync(respBytes, 0, respBytes.Length, cts.Token);
                await connection.FlushAsync();
                Array.Resize(ref respBytes, bytesRead);
                responses.Add(MinecraftRconMessageSerializer.Deserialize(respBytes));
                await Task.Delay(100);
            }
            while (connection.DataAvailable);

            if (responses is [var resp])
                return new RconMessageResponse(resp, req.Id == resp.Id);

            return new RconMessageResponse(
                new RconMessage(responses.Sum(r => r.Length), req.Id, RconMessageType.Response, string.Join("", responses.Select(r => r.Body))),
                true
            );
        }

    }

    public enum RconMessageType : int
    {
        Response = 0,
        Command = 2,
        Authenticate = 3
    }

    public record RconMessageResponse(RconMessage Message, bool Success);
    public record RconMessage(int Length, int Id, RconMessageType Type, string Body);

    public class MinecraftRconMessageSerializer
    {
        public const int HeaderLength = 10; // Does not include 4-byte message length.

        public static byte[] Serialize(RconMessage msg)
        {
            List<byte> bytes =
            [
                .. BitConverter.GetBytes(msg.Length),
                .. BitConverter.GetBytes(msg.Id),
                .. BitConverter.GetBytes((int)msg.Type),
                .. Encoding.ASCII.GetBytes(msg.Body),
                .. new byte[] { 0, 0 },
            ];

            return bytes.ToArray();
        }

        public static RconMessage Deserialize(byte[] rawData)
        {
            var messageLength = BitConverter.ToInt32(rawData, 0);
            var messageId = BitConverter.ToInt32(rawData, 4);
            var messageType = BitConverter.ToInt32(rawData, 8);

            var messageBodyLength = rawData.Length - (HeaderLength + 4);
            if (messageBodyLength > 0)
            {
                byte[] bodyBytes = new byte[messageBodyLength];
                Array.Copy(rawData, 12, bodyBytes, 0, messageBodyLength);
                Array.Resize(ref bodyBytes, messageBodyLength);
                return new RconMessage(messageLength, messageId, (RconMessageType)messageType, Encoding.ASCII.GetString(bodyBytes));
            }
            else
            {
                return new RconMessage(messageLength, messageId, (RconMessageType)messageType, "");
            }
        }
    }
}