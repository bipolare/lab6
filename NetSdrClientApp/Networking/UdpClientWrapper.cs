using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientApp.Networking
{
    // Inherit from IDisposable for proper cleanup
    public class UdpClientWrapper : IUdpClient, IDisposable
    {
        private readonly IPEndPoint _localEndPoint;
        private CancellationTokenSource? _cts;
        private UdpClient? _udpClient;
        private bool _disposed = false;

        public event EventHandler<byte[]>? MessageReceived;

        public UdpClientWrapper(int port)
        {
            // IPAddress.Any means the client will listen on all network interfaces
            _localEndPoint = new IPEndPoint(IPAddress.Any, port);
        }

        public async Task StartListeningAsync()
        {
            // Ensure old resources are cleaned up before starting
            if (_udpClient != null)
            {
                StopListening();
            }

            _cts = new CancellationTokenSource();
            Console.WriteLine($"Start listening for UDP messages on port {_localEndPoint.Port}...");

            try
            {
                // Bind the client to the local port for listening
                _udpClient = new UdpClient(_localEndPoint); 
                
                while (!_cts.Token.IsCancellationRequested)
                {
                    // Use the CancellationToken in the ReceiveAsync call
                    UdpReceiveResult result = await _udpClient.ReceiveAsync(_cts.Token);
                    
                    // Invoke event on a separate task to avoid blocking the listener loop
                    Task.Run(() => MessageReceived?.Invoke(this, result.Buffer));

                    // Logging is safe here
                    Console.WriteLine($"Received {result.Buffer.Length} bytes from {result.RemoteEndPoint}");
                }
            }
            catch (OperationCanceledException)
            {
                // This is the expected exit path when StopListening is called
                Console.WriteLine("UDP listener stopped gracefully.");
            }
            catch (Exception ex)
            {
                if (!_disposed) // Log only if the error wasn't due to disposal
                {
                    Console.WriteLine($"Error receiving message: {ex.Message}");
                }
            }
            finally
            {
                // Ensure resources are closed if the listening task exits
                _udpClient?.Close();
                _udpClient = null;
            }
        }
        
        // 🛑 IMPLEMENTATION: Send byte array
        public async Task SendMessageAsync(byte[] data, IPEndPoint remoteEndPoint)
        {
            // A new UdpClient can be created for sending if one isn't already active
            // This is necessary because the listening UdpClient is bound to the local port.
            using var sender = new UdpClient();
            await sender.SendAsync(data, data.Length, remoteEndPoint);
            Console.WriteLine($"Sent {data.Length} bytes to {remoteEndPoint}");
        }

        // 🛑 IMPLEMENTATION: Send string
        public async Task SendMessageAsync(string message, IPEndPoint remoteEndPoint)
        {
            var data = Encoding.UTF8.GetBytes(message);
            await SendMessageAsync(data, remoteEndPoint);
        }

        public void StopListening()
        {
            // Uses Dispose logic for cleanup
            Dispose(true);
            Console.WriteLine("Stopped listening for UDP messages.");
        }

        public void Exit()
        {
            // Use Dispose logic for cleanup
            Dispose(true);
            Console.WriteLine("Exit cleanup complete.");
        }

        // 🛑 IMPLEMENTATION: IDisposable Pattern
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Cancel token first to unblock ReceiveAsync
                    try
                    {
                        _cts?.Cancel();
                        _cts?.Dispose();
                    }
                    catch (ObjectDisposedException) { }
                }

                // Close the UdpClient socket
                _udpClient?.Close();
                _udpClient?.Dispose(); // UdpClient implements IDisposable

                _udpClient = null;
                _cts = null;
                _disposed = true;
            }
        }
        
        public override int GetHashCode()
        {
            var payload = $"{nameof(UdpClientWrapper)}|{_localEndPoint.Address}|{_localEndPoint.Port}";

            // The MD5 class should be disposed of properly
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(payload));

            return BitConverter.ToInt32(hash, 0);
        }
    } // Закрывающая скобка для класса
} // Закрывающая скобка для пространства имен NetSdrClientApp.Networking
