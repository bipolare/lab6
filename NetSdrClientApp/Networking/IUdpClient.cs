using System; // Required for IDisposable
using System.Threading.Tasks;

namespace NetSdrClientApp.Networking
{
    // Inherit from IDisposable to enable the .Dispose() method 
    // for clean resource shutdown.
    public interface IUdpClient : IDisposable
    {
        event EventHandler<byte[]>? MessageReceived;

        Task StartListeningAsync();

        void StopListening();
        void Exit();
    }
}
