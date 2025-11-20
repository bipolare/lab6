using System;
using System.Net;
using System.Threading.Tasks;

namespace NetSdrClientApp.Networking
{
    public interface IUdpClient : IDisposable
    {
        event EventHandler<byte[]>? MessageReceived;

        // Methods from UdpClientWrapper implementation
        Task StartListeningAsync();
        void StopListening();
        Task SendMessageAsync(byte[] data, IPEndPoint remoteEndPoint);
        Task SendMessageAsync(string message, IPEndPoint remoteEndPoint);
        void Exit();
    }
}
