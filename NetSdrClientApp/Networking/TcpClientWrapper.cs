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
        
        // CancellationTokenSource для управления отменой асинхронных операций
        private CancellationTokenSource? _cts = null; 
        private bool _disposed = false; // Флаг для проверки, был ли вызван Dispose

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

            // Очистка предыдущих ресурсов
            Dispose(true); 

            _tcpClient = new TcpClient();

            try
            {
                _cts = new CancellationTokenSource(); 
                _tcpClient.Connect(_host, _port);
                _stream = _tcpClient.GetStream();
                Console.WriteLine($"Connected to {_host}:{_port}");
                
                // Запуск асинхронного прослушивания (fire-and-forget)
                _ = StartListeningAsync(); 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect: {ex.Message}");
                // Обеспечение обнуления ресурсов при сбое
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
                // Логика отключения перемещена в метод Dispose для централизованной очистки
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
            var data = Encoding.UTF8.GetBytes(str);
            if (Connected && _stream != null && _stream.CanWrite)
            {
                // 🛑 ИСПРАВЛЕНИЕ ОШИБКИ: Завершение оператора Console.WriteLine
                Console.WriteLine($"Message sent: " + data.Select(b => Convert.ToString(b, toBase: 16)).Aggregate((l, r) => $"{l} {r}"));
                await _stream.WriteAsync(data, 0, data.Length, _cts?.Token ?? CancellationToken.None);
            }
            else
            {
                throw new InvalidOperationException("Not connected to a server.");
            }
        }
        
        // 🛑 ДОБАВЛЕНИЕ: Реализация асинхронного прослушивания сообщений
        public async Task StartListeningAsync()
        {
            var buffer = new byte[4096];
            var cancellationToken = _cts?.Token ?? CancellationToken.None;

            try
            {
                while (!cancellationToken.IsCancellationRequested && Connected && _stream != null)
                {
                    // Чтение данных из потока с учетом токена отмены
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    
                    if (bytesRead == 0) // Сервер закрыл соединение
                    {
                        Console.WriteLine("Connection closed by remote server.");
                        break; 
                    }

                    // Копирование полученных данных
                    var receivedData = new byte[bytesRead];
                    Array.Copy(buffer, receivedData, bytesRead);
                    
                    // Вызов события
                    Task.Run(() => MessageReceived?.Invoke(this, receivedData));
                }
            }
            catch (OperationCanceledException)
            {
                // Ожидается при отмене токена
                Console.WriteLine("Listener stopped gracefully by cancellation.");
            }
            catch (IOException ex) when (ex.InnerException is SocketException se)
            {
                // Обработка ожидаемых ошибок сокета (например, сброс соединения)
                Console.WriteLine($"Socket error while listening: {se.SocketErrorCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during listening: {ex.Message}");
            }
            finally
            {
                // Обеспечение правильного отключения при выходе из цикла/исключении
                Disconnect(); 
            }
        }

        // 🛑 ДОБАВЛЕНИЕ: Реализация IDisposable
        public void Dispose()
        {
            // Не меняйте этот код. Поместите код очистки в 'Dispose(bool disposing)'
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Отмена задачи прослушивания
                    try
                    {
                        _cts?.Cancel();
                    }
                    catch (ObjectDisposedException) { } // Игнорировать, если уже очищено

                    // Очистка управляемых ресурсов
                    _stream?.Dispose();
                    _tcpClient?.Close(); // Безопаснее, чем Dispose() для TcpClient
                    _cts?.Dispose();
                }

                // Обнуление больших полей
                _stream = null;
                _tcpClient = null;
                _cts = null;

                _disposed = true;
            }
        }

    } // Закрывающая скобка для класса TcpClientWrapper
} // Закрывающая скобка для пространства имен NetSdrClientApp.Networking
