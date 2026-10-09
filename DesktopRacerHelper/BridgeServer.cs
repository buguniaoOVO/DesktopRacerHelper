using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DesktopRacerHelper;

internal sealed class BridgeServer : IDisposable
{
	private const int PreferredPort = 19100;

	private readonly TcpListener _listener;

	private readonly Func<string, (int Status, string Body)> _handler;

	private volatile bool _running = true;

	public int Port { get; }

	public string InstanceId { get; } = Guid.NewGuid().ToString("N");

	public BridgeServer(Func<string, (int Status, string Body)> handler)
	{
		_handler = handler;
		_listener = BindAvailable(19100);
		Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
		Task.Run((Action)AcceptLoop);
	}

	private static TcpListener BindAvailable(int preferred)
	{
		List<int> list = new List<int>();
		for (int i = preferred; i <= preferred + 10; i++)
		{
			list.Add(i);
		}
		list.Add(0);
		SocketException ex = null;
		foreach (int item in list)
		{
			TcpListener tcpListener = new TcpListener(IPAddress.Loopback, item);
			tcpListener.Server.ExclusiveAddressUse = true;
			try
			{
				tcpListener.Start(16);
				return tcpListener;
			}
			catch (SocketException ex2)
			{
				ex = ex2;
				tcpListener.Stop();
			}
		}
		throw ex ?? new SocketException();
	}

	private void AcceptLoop()
	{
		while (_running)
		{
			try
			{
				TcpClient client = _listener.AcceptTcpClient();
				Task.Run(() =>
				{
					Serve(client);
				});
			}
			catch (SocketException)
			{
				if (!_running)
				{
					break;
				}
			}
			catch (ObjectDisposedException)
			{
				break;
			}
		}
	}

	private void Serve(TcpClient client)
	{
		using (client)
		{
			try
			{
				client.ReceiveTimeout = 5000;
				client.SendTimeout = 5000;
				client.NoDelay = true;
				using NetworkStream networkStream = client.GetStream();
				using StreamReader streamReader = new StreamReader(networkStream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, 1024, leaveOpen: true);
				string text = streamReader.ReadLine() ?? "";
				int num = 0;
				bool flag = false;
				while (num++ < 64)
				{
					string text2 = streamReader.ReadLine();
					if (text2 == null)
					{
						break;
					}
					if (text2.Length == 0)
					{
						flag = true;
						break;
					}
				}
				string[] array = text.Split(' ');
				int value;
				string s;
				if (!flag || array.Length != 3 || array[0] != "GET")
				{
					value = 400;
					s = "INVALID_REQUEST";
				}
				else if (array[1].StartsWith("/api/", StringComparison.Ordinal))
				{
					(value, s) = _handler(array[1].Split('?')[0]);
				}
				else
				{
					value = 404;
					s = "NOT_FOUND";
				}
				byte[] bytes = Encoding.UTF8.GetBytes(s);
				byte[] bytes2 = Encoding.ASCII.GetBytes($"HTTP/1.1 {value} Result\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
				networkStream.Write(bytes2, 0, bytes2.Length);
				networkStream.Write(bytes, 0, bytes.Length);
			}
			catch
			{
			}
		}
	}

	public string Publish()
	{
		Directory.CreateDirectory(Deployment.BridgeFolder);
		string text = Path.Combine(Deployment.BridgeFolder, "helper.properties");
		string contents = $"instance_id={InstanceId}\nport={Port}\n";
		File.WriteAllText(text, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		return text;
	}

	public void Unpublish()
	{
		try
		{
			string path = Path.Combine(Deployment.BridgeFolder, "helper.properties");
			if (File.Exists(path) && File.ReadAllText(path).Contains(InstanceId))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	public void Dispose()
	{
		_running = false;
		_listener.Stop();
		Unpublish();
	}
}
