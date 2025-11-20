using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientApp.Networking
{
    public class TcpClientWrapper : ITcpClient, IDisposable
    {
        private string _host;
        private int _port;
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        
        // Change to nullable and initialize to null to fix CS8618 (Non-nullable field must contain non-null value)
        private CancellationTokenSource? _cts = null; 
        private bool _disposed = false; // Flag to check if Dispose has been called

        public bool Connected => _tcpClient != null && _tcpClient.Connected && _stream != null;

        public event EventHandler<byte[]>? MessageReceived;

        public TcpClientWrapper(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public void Connect()
        {
            if (Connected)
            {
                Console.WriteLine($"Already connected to {_host}:{_port}");
                return;
            }

            // Dispose any previous client/stream that might be lingering
            Dispose(true); 

            _tcpClient = new TcpClient();

            try
            {
                // Initialize CancellationTokenSource here where it's first used, fixing S2930 if Disposed correctly
                _cts = new CancellationTokenSource(); 
                _tcpClient.Connect(_host, _port);
                _stream = _tcpClient.GetStream();
                Console.WriteLine($"Connected to {_host}:{_port}");
                
                // CS4014 Warning fix: Store the Task or await it if blocking is okay.
                // Since this is a listener, fire-and-forget is often intended, but better to capture it
                // to prevent warnings or unhandled exceptions from crashing the application.
                // For this example, we keep the original intent but acknowledge the warning.
                _ = StartListeningAsync(); 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect: {ex.Message}");
                // Ensure resources are nullified on failure
                _tcpClient = null;
                _stream = null;
                _cts?.Dispose();
                _cts = null;
            }
        }

        public void Disconnect()
        {
            if (Connected)
            {
                // Disconnect logic is moved into the Dispose method for centralized cleanup
                Dispose(true);
                
                Console.WriteLine("Disconnected.");
            }
            else
            {
                Console.WriteLine("No active connection to disconnect.");
            }
        }

        public async Task SendMessageAsync(byte[] data)
        {
            // ... (rest of the method remains the same)
            if (Connected && _stream != null && _stream.CanWrite)
            {
                Console.WriteLine($"Message sent: " + data.Select(b => Convert.ToString(b, toBase: 16)).Aggregate((l, r) => $"{l} {r}"));
                await _stream.WriteAsync(data, 0, data.Length, _cts?.Token ?? CancellationToken.None);
            }
            else
            {
                throw new InvalidOperationException("Not connected to a server.");
            }
        }

        public async Task SendMessageAsync(string str)
        {
            // ... (rest of the method remains the same)
            var data = Encoding.UTF8.GetBytes(str);
            if (Connected && _stream != null && _stream.CanWrite)
            {
                Console.WriteLine($"Message sent: " + data.Select(b => Convert.ToString(b, toBase: 1
