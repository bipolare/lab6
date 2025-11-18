using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Linq; // Додано для використання Linq у UdpTimedSender

// --- ТОЧКА ВХОДУ ДЛЯ КОНСОЛЬНОГО ЗАСТОСУНКУ (ВИПРАВЛЕННЯ CS5001) ---
try
{
    Console.WriteLine("Starting EchoServer (TCP) and UdpTimedSender...");
    
    // Використовуємо using для автоматичного виклику Dispose
    using var server = new EchoServer(50000); 
    using var udpSender = new UdpTimedSender("127.0.0.1", 60000); 
    
    udpSender.StartSending(100);
    
    // Сервер працює, поки не буде скасовано
    await server.StartAsync(); 
}
catch (Exception ex)
{
    Console.WriteLine($"Критична помилка: {ex.Message}");
}
finally
{
    Console.WriteLine("EchoServer stopped.");
}
// -------------------------------------------------------------------

namespace EchoServer
{
    // Додано IDisposable для виправлення Sonar S2930
    public class EchoServer : IDisposable
    {
        private readonly int _port;
        // Змінено на nullable
        private TcpListener? _listener; 
        // Зроблено readonly для виправлення Sonar S2933
        private readonly CancellationTokenSource _cancellationTokenSource; 

        //constuctor
        public EchoServer(int port)
        {
            _port = port;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            Console.WriteLine($"Server started on port {_port}.");

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    Console.WriteLine("Client connected.");

                    // РЕФАКТОРИНГ: Передаємо NetworkStream для тестування
                    _ = Task.Run(() => HandleClientAsync(client.GetStream(), _cancellationTokenSource.Token));
                }
                catch (ObjectDisposedException)
                {
                    // Listener has been closed
                    break;
                }
            }

            Console.WriteLine("Server shutdown.");
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _listener?.Stop(); 
        }

        // РЕФАКТОРИНГ: Змінено на PUBLIC та приймає абстракцію Stream для тестування
        public async Task HandleClientAsync(Stream stream, CancellationToken token)
        {
            using (stream) 
            {
                byte[] buffer = new byte[1024];
                int bytesRead;

                try
                {
                    while (!token.IsCancellationRequested && (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) != 0)
                    {
                        Console.WriteLine($"Received: {bytesRead} bytes.");

                        // Echo logic: send received data back
                        await stream.WriteAsync(buffer, 0, bytesRead, token); 
                        Console.WriteLine($"Sent: {bytesRead} bytes.");
                    }
                }
                catch (IOException ex) when (ex.InnerException is SocketException se && se.SocketErrorCode == SocketError.ConnectionReset)
                {
                    Console.WriteLine("Client disconnected forcefully.");
                }
                catch (OperationCanceledException)
                {
                    // Токен скасовано
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling client: {ex.Message}");
                }
            }
            Console.WriteLine("Client disconnected.");
        }
        
        // РЕФАКТОРИНГ: Додано Dispose для IDisposable
        public void Dispose()
        {
            Stop(); // Забезпечуємо зупинку
            _cancellationTokenSource.Dispose();
        }
    }

    public class UdpTimedSender : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly UdpClient _udpClient;
        private Timer? _timer; 

        public UdpTimedSender(string host, int port)
        {
            _host = host;
            _port = port;
            _udpClient = new UdpClient();
        }

        public void StartSending(int intervalMilliseconds)
        {
            if (_timer != null)
                throw new InvalidOperationException("Sender is already running.");

            _timer = new Timer(SendMessageCallback, null, 0, intervalMilliseconds);
        }

        ushort i = 0;

        private void SendMessageCallback(object? state)
        {
            try
            {
                //dummy data
                Random rnd = new Random();
                byte[] samples = new byte[1024];
                rnd.NextBytes(samples);
                i++;

                // Змінено на явний масив для Concat
                byte[] msg = (new byte[] { 0x04, 0x84 }).Concat(BitConverter.GetBytes(i)).Concat(samples).ToArray();
                var endpoint = new IPEndPoint(IPAddress.Parse(_host), _port);

                _udpClient.Send(msg, msg.Length, endpoint);
                Console.WriteLine($"Message sent to {_host}:{_port} ");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }

        public void StopSending()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public void Dispose()
        {
            StopSending();
            _udpClient.Dispose();
        }
    }
}
