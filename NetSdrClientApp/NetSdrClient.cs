using NetSdrClientApp.Messages;
using NetSdrClientApp.Networking;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
// 1. ВИДАЛЕНО: using EchoServer; 
using static NetSdrClientApp.Messages.NetSdrMessageHelper;

namespace NetSdrClientApp
{
	/// <summary>
	/// Клієнт для взаємодії з NetSDR через TCP/UDP.
	/// </summary>
	public sealed class NetSdrClient : IDisposable
	{
		private readonly ITcpClient _tcpClient;
		private readonly IUdpClient _udpClient;
		private readonly object _lock = new();
		private TaskCompletionSource<byte[]>? _responseTaskSource;

		private const long DefaultSampleRate = 100_000;
		private const ushort AutomaticFilterMode = 0;
		private static readonly byte[] DefaultAdMode = { 0x00, 0x03 };
		private static readonly string SampleFileName = "samples.bin";
        
        // 2. ВИДАЛЕНО: Жорстку залежність від тестового сервера:
        // private readonly EchoServer.EchoServer _serverHarness = new EchoServer.EchoServer();


		/// <summary>
		/// Вказує, чи активний прийом IQ-даних.
		/// </summary>
		public bool IQStarted { get; private set; }

		public NetSdrClient(ITcpClient tcpClient, IUdpClient udpClient)
		{
			_tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
			_udpClient = udpClient ?? throw new ArgumentNullException(nameof(udpClient));
			
			_tcpClient.MessageReceived += OnTcpMessageReceived;
			_udpClient.MessageReceived += OnUdpMessageReceived;
		}

		public async Task ConnectAsync()
		{
			if (_tcpClient.Connected)
				return;
			
			// _serverHarness.StartAsync(); // Виклик видалено
			
			_tcpClient.Connect();

			if (!_tcpClient.Connected)
			{
				Console.WriteLine("Connection attempt failed.");
				return;
			}

			// 1. Set IQOutputDataSampleRate
			var sampleRateMsg = GetControlItemMessage(
				MsgTypes.SetControlItem, 
				ControlItemCodes.IQOutputDataSampleRate, 
				BitConverter.GetBytes(DefaultSampleRate).Take(5).ToArray()); // 5-byte long
			await SendTcpRequestAsync(sampleRateMsg).ConfigureAwait(false);

			// 2. Set RFFilter
			var rfFilterMsg = GetControlItemMessage(
				MsgTypes.SetControlItem, 
				ControlItemCodes.RFFilter, 
				BitConverter.GetBytes(AutomaticFilterMode).Take(2).ToArray()); // 2-byte ushort
			await SendTcpRequestAsync(rfFilterMsg).ConfigureAwait(false);

			// 3. Set ADModes
			var adModesMsg = GetControlItemMessage(
				MsgTypes.SetControlItem, 
				ControlItemCodes.ADModes, 
				DefaultAdMode);
			await SendTcpRequestAsync(adModesMsg).ConfigureAwait(false);
			
			Console.WriteLine("Client connected and initialized.");
		}

		public void Disconnect()
		{
			if (IQStarted)
			{
				StopIQAsync().Wait();
			}

			_tcpClient.Disconnect();
			// _serverHarness.Stop(); // Виклик видалено
		}
// ... (решта методів без змін)
