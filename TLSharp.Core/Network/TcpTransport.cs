using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CNPR;
//url: https://github.com/meebey/starksoftproxy
using ChapyarHelper.Tools.ProxyTools.Net.Proxy;

namespace TLSharp.Core.Network
{
    public delegate TcpClient TcpClientConnectionHandler(string address, int port);

    public class TcpTransport : IDisposable
    {
        private readonly TcpClient _tcpClientp;
        private int sendCounter = 0;
        

        public TcpTransport(string address, int port, ProxyType proxyType, string proxyIP,int proxyPort, string proxyUsername,string proxyPassword, TcpClientConnectionHandler handler = null)
        {
            IProxyClient proxyClient;
            try
            {
                if (handler == null)
                {
                    GlobalLogHelper.AddLog($"Connecting to the tcp client using the {address}:{port}");
                    //&port=1010&user=23101000%28join-%40Proxy55%29&pass=join-%40Proxy55
                    // create an instance of the client proxy factory 
                    ProxyClientFactory factory = new ProxyClientFactory();
                    
                    switch (proxyType)
                    {
                        case ProxyType.None:
                            proxyClient = factory.CreateProxyClient(ProxyType.None);
                            break;
                        case ProxyType.Http:
                            proxyClient = factory.CreateProxyClient(ProxyType.Http, proxyIP, proxyPort, proxyUsername, proxyPassword);
                            break;
                        case ProxyType.Socks4:
                            proxyClient = factory.CreateProxyClient(ProxyType.Socks4, proxyIP, proxyPort, proxyUsername, proxyPassword);
                            break;
                        case ProxyType.Socks4a:
                            proxyClient = factory.CreateProxyClient(ProxyType.Socks4a, proxyIP, proxyPort, proxyUsername, proxyPassword);
                            break;
                        case ProxyType.Socks5:
                            proxyClient = factory.CreateProxyClient(ProxyType.Socks5, proxyIP, proxyPort,proxyUsername,proxyPassword);
                            break;
                        default:
                            proxyClient = factory.CreateProxyClient(ProxyType.None);
                            break;
                    }
                    // use the proxy client factory to generically specify the type of proxy to create 
                    // the proxy factory method CreateProxyClient returns an IProxyClient object 
                    //IProxyClient proxy = factory.CreateProxyClient(ProxyType.Socks5, "185.86.181.7", 9090);
                    
                    // create a connection through the proxy to www.starksoft.com over port 80 
                    _tcpClientp = proxyClient.CreateConnection(address, 443);
                    //  _tcpClient = new TcpClient();

                    var ipAddress = IPAddress.Parse(address);
                    //_tcpClientp.Connect(ipAddress, port);
                    GlobalLogHelper.AddLog("Connected to the tcp client");
                }
                else
                    _tcpClientp = handler(address, port);
            }
            catch (Exception ex)
            {
                GlobalLogHelper.AddExceptionLog(ex);
                throw ex;
            }
        }

        public async Task Send(byte[] packet)
        {
            if (!_tcpClientp.Connected)
                throw new InvalidOperationException("Client not connected to server.");

            var tcpMessage = new TcpMessage(sendCounter, packet);

            await _tcpClientp.GetStream().WriteAsync(tcpMessage.Encode(), 0, tcpMessage.Encode().Length);
            sendCounter++;
        }

        public async Task<TcpMessage> Receieve()
        {
            var stream = _tcpClientp.GetStream();

            var packetLengthBytes = new byte[4];
            if (await stream.ReadAsync(packetLengthBytes, 0, 4) != 4)
                throw new InvalidOperationException("Couldn't read the packet length");
            int packetLength = BitConverter.ToInt32(packetLengthBytes, 0);

            var seqBytes = new byte[4];
            if (await stream.ReadAsync(seqBytes, 0, 4) != 4)
                throw new InvalidOperationException("Couldn't read the sequence");
            int seq = BitConverter.ToInt32(seqBytes, 0);

            int readBytes = 0;
            var body = new byte[packetLength - 12];
            int neededToRead = packetLength - 12;

            do
            {
                var bodyByte = new byte[packetLength - 12];
                var availableBytes = await stream.ReadAsync(bodyByte, 0, neededToRead);
                neededToRead -= availableBytes;
                Buffer.BlockCopy(bodyByte, 0, body, readBytes, availableBytes);
                readBytes += availableBytes;
            }
            while (readBytes != packetLength - 12);

            var crcBytes = new byte[4];
            if (await stream.ReadAsync(crcBytes, 0, 4) != 4)
                throw new InvalidOperationException("Couldn't read the crc");
            int checksum = BitConverter.ToInt32(crcBytes, 0);

            byte[] rv = new byte[packetLengthBytes.Length + seqBytes.Length + body.Length];

            Buffer.BlockCopy(packetLengthBytes, 0, rv, 0, packetLengthBytes.Length);
            Buffer.BlockCopy(seqBytes, 0, rv, packetLengthBytes.Length, seqBytes.Length);
            Buffer.BlockCopy(body, 0, rv, packetLengthBytes.Length + seqBytes.Length, body.Length);
            var crc32 = new Ionic.Crc.CRC32();
            crc32.SlurpBlock(rv, 0, rv.Length);
            var validChecksum = crc32.Crc32Result;

            if (checksum != validChecksum)
            {
                throw new InvalidOperationException("invalid checksum! skip");
            }

            return new TcpMessage(seq, body);
        }

        public async Task<TcpMessage> Receieve(CancellationToken token)
        {
            var stream = _tcpClientp.GetStream();

            var packetLengthBytes = new byte[4];
            if (await stream.ReadAsync(packetLengthBytes, 0, 4, token) != 4)
                throw new InvalidOperationException("Couldn't read the packet length");
            int packetLength = BitConverter.ToInt32(packetLengthBytes, 0);

            var seqBytes = new byte[4];
            if (await stream.ReadAsync(seqBytes, 0, 4) != 4)
                throw new InvalidOperationException("Couldn't read the sequence");
            int seq = BitConverter.ToInt32(seqBytes, 0);

            int readBytes = 0;
            var body = new byte[packetLength - 12];
            int neededToRead = packetLength - 12;

            do
            {
                var bodyByte = new byte[packetLength - 12];
                var availableBytes = await stream.ReadAsync(bodyByte, 0, neededToRead);
                neededToRead -= availableBytes;
                Buffer.BlockCopy(bodyByte, 0, body, readBytes, availableBytes);
                readBytes += availableBytes;
            }
            while (readBytes != packetLength - 12);

            var crcBytes = new byte[4];
            if (await stream.ReadAsync(crcBytes, 0, 4) != 4)
                throw new InvalidOperationException("Couldn't read the crc");
            int checksum = BitConverter.ToInt32(crcBytes, 0);

            byte[] rv = new byte[packetLengthBytes.Length + seqBytes.Length + body.Length];

            Buffer.BlockCopy(packetLengthBytes, 0, rv, 0, packetLengthBytes.Length);
            Buffer.BlockCopy(seqBytes, 0, rv, packetLengthBytes.Length, seqBytes.Length);
            Buffer.BlockCopy(body, 0, rv, packetLengthBytes.Length + seqBytes.Length, body.Length);
            var crc32 = new Ionic.Crc.CRC32();
            crc32.SlurpBlock(rv, 0, rv.Length);
            var validChecksum = crc32.Crc32Result;

            if (checksum != validChecksum)
            {
                throw new InvalidOperationException("invalid checksum! skip");
            }

            return new TcpMessage(seq, body);
        }

        public bool IsConnected
        {
            get
            {
                return this._tcpClientp.Connected;
            }
        }

        public void Dispose()
        {
            if (_tcpClientp.Connected)
                _tcpClientp.Close();
        }
    }
}
