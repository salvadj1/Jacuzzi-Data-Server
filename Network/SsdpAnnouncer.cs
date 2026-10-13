using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JacuzziServer.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JacuzziServer.Network
{
    /// <summary>
    /// Anuncia este servidor en la red local mediante SSDP/UPnP para que aparezca
    /// en el Explorador de Windows -> Red (seccion "Otros dispositivos"), con
    /// doble clic que abre el panel web.
    ///
    /// Funcionamiento:
    ///  - Escucha en UDP 239.255.255.250:1900 y responde a las busquedas (M-SEARCH).
    ///  - Envia anuncios NOTIFY (alive) al arrancar y cada 5 minutos; al parar envia byebye.
    ///  - La descripcion del dispositivo (/device.xml) la sirve DeviceController.
    ///
    /// Si no puede abrir el puerto 1900, lo registra como aviso y el resto del
    /// servidor sigue funcionando. Reutilizable: solo depende de SsdpOptions y LocalNetwork.
    /// </summary>
    public class SsdpAnnouncer : BackgroundService
    {
        private const string MulticastAddress = "239.255.255.250";
        private const int SsdpPort = 1900;
        private const int MaxAgeSeconds = 1800;      // validez del anuncio
        private const int NotifyIntervalSeconds = 300; // frecuencia de re-anuncio

        private static readonly IPAddress Group = IPAddress.Parse(MulticastAddress);
        private static readonly IPEndPoint MulticastEndPoint = new IPEndPoint(Group, SsdpPort);

        private readonly SsdpOptions _opt;
        private readonly ILogger<SsdpAnnouncer> _log;
        private readonly string _uuid;
        private Socket _listener;

        /// <summary>Crea el anunciador con sus opciones y logger (inyectados).</summary>
        public SsdpAnnouncer(IOptions<SsdpOptions> options, ILogger<SsdpAnnouncer> log)
        {
            _opt = options.Value;
            _log = log;
            _uuid = BuildUuid();
        }

        /// <summary>
        /// UUID estable del dispositivo, derivado del nombre del PC: no cambia entre
        /// reinicios, asi Windows no lo duplica en la lista de Red.
        /// </summary>
        public static string BuildUuid()
        {
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes("JacuzziServer-" + Environment.MachineName));
                return new Guid(hash).ToString();
            }
        }

        /// <summary>Tipos de notificacion (NT/ST) que este dispositivo publica.</summary>
        private string[] NotificationTypes
        {
            get { return new[] { "upnp:rootdevice", "uuid:" + _uuid, _opt.DeviceType }; }
        }

        /// <summary>USN (identificador unico de servicio) para un tipo de notificacion.</summary>
        private string UsnFor(string nt)
        {
            return nt.StartsWith("uuid:", StringComparison.Ordinal) ? nt : "uuid:" + _uuid + "::" + nt;
        }

        /// <summary>Cabecera SERVER de los mensajes SSDP.</summary>
        private static string ServerHeader
        {
            get { return "Windows/10 UPnP/1.0 JacuzziServer/1.0"; }
        }

        /// <summary>Bucle principal: abre el socket y lanza escucha y anuncios periodicos.</summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_opt.Enabled)
            {
                _log.LogInformation("SSDP desactivado en appsettings.json");
                return;
            }

            try
            {
                _listener = CreateListener();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "No se pudo abrir UDP {Port} para SSDP; el servidor no aparecera en Red", SsdpPort);
                return;
            }

            _log.LogInformation("SSDP activo: se anuncia como '{Name}' ({Machine})", _opt.FriendlyName, Environment.MachineName);

            // Al cancelar, cerrar el socket desbloquea la recepcion pendiente.
            using (stoppingToken.Register(CloseListener))
            {
                await Task.WhenAll(ListenLoopAsync(stoppingToken), NotifyLoopAsync(stoppingToken));
            }
        }

        /// <summary>Al detener el servicio, avisa a la red de que el dispositivo se va (byebye).</summary>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_opt.Enabled)
            {
                try { SendNotify(false); } catch { /* al parar no es critico */ }
            }
            await base.StopAsync(cancellationToken);
        }

        /// <summary>
        /// Crea el socket de escucha en UDP 1900 compartido (ReuseAddress, porque el
        /// servicio "Descubrimiento SSDP" de Windows tambien lo usa) y se une al grupo
        /// multicast en cada interfaz IPv4 activa.
        /// </summary>
        private Socket CreateListener()
        {
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            s.Bind(new IPEndPoint(IPAddress.Any, SsdpPort));

            foreach (var li in LocalNetwork.GetIPv4Interfaces())
            {
                try
                {
                    s.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                        new MulticastOption(Group, li.Address));
                }
                catch (Exception ex)
                {
                    _log.LogDebug(ex, "No se pudo unir al multicast en {Ip}", li.Address);
                }
            }
            return s;
        }

        /// <summary>Cierra el socket de escucha (provoca que ListenLoopAsync termine).</summary>
        private void CloseListener()
        {
            try { _listener?.Close(); } catch { }
        }

        /// <summary>Recibe paquetes y atiende las busquedas M-SEARCH hasta que se cancela.</summary>
        private async Task ListenLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[4096];
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    var r = await _listener.ReceiveFromAsync(new ArraySegment<byte>(buffer), SocketFlags.None, remote);
                    string msg = Encoding.UTF8.GetString(buffer, 0, r.ReceivedBytes);
                    if (msg.StartsWith("M-SEARCH", StringComparison.OrdinalIgnoreCase))
                        _ = HandleSearchAsync(msg, (IPEndPoint)r.RemoteEndPoint);
                }
                catch (ObjectDisposedException)
                {
                    break; // socket cerrado al parar
                }
                catch (SocketException)
                {
                    if (ct.IsCancellationRequested) break;
                    await Task.Delay(100); // p.ej. reset ICMP de UDP en Windows: se ignora y se sigue
                }
            }
        }

        /// <summary>
        /// Responde (unicast) a una busqueda M-SEARCH si el ST pedido coincide con
        /// alguno de nuestros tipos (o es ssdp:all).
        /// </summary>
        private async Task HandleSearchAsync(string msg, IPEndPoint remote)
        {
            try
            {
                string st = GetHeader(msg, "ST");
                if (string.IsNullOrEmpty(st)) return;

                var matches = NotificationTypes
                    .Where(nt => st == "ssdp:all" || string.Equals(st, nt, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (matches.Count == 0) return;

                var local = LocalNetwork.PickFor(remote.Address, LocalNetwork.GetIPv4Interfaces());
                if (local == null) return;

                await Task.Delay(new Random().Next(0, 400)); // dispersa respuestas simultaneas (MX)

                foreach (var nt in matches)
                {
                    string resp =
                        "HTTP/1.1 200 OK\r\n" +
                        "CACHE-CONTROL: max-age=" + MaxAgeSeconds + "\r\n" +
                        "EXT:\r\n" +
                        "LOCATION: " + LocationFor(local.Address) + "\r\n" +
                        "SERVER: " + ServerHeader + "\r\n" +
                        "ST: " + nt + "\r\n" +
                        "USN: " + UsnFor(nt) + "\r\n\r\n";
                    byte[] data = Encoding.UTF8.GetBytes(resp);
                    await _listener.SendToAsync(new ArraySegment<byte>(data), SocketFlags.None, remote);
                }
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Error atendiendo M-SEARCH");
            }
        }

        /// <summary>Anuncia (alive) al arrancar y luego periodicamente hasta que se cancela.</summary>
        private async Task NotifyLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try { SendNotify(true); }
                    catch (Exception ex) { _log.LogDebug(ex, "Error enviando NOTIFY"); }
                    await Task.Delay(TimeSpan.FromSeconds(NotifyIntervalSeconds), ct);
                }
            }
            catch (OperationCanceledException) { /* parada normal */ }
        }

        /// <summary>
        /// Envia por multicast un NOTIFY por cada tipo y cada interfaz IPv4.
        /// </summary>
        /// <param name="alive">true = ssdp:alive; false = ssdp:byebye.</param>
        private void SendNotify(bool alive)
        {
            foreach (var li in LocalNetwork.GetIPv4Interfaces())
            {
                using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    s.Bind(new IPEndPoint(li.Address, 0));
                    s.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, li.Address.GetAddressBytes());
                    s.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);

                    foreach (var nt in NotificationTypes)
                    {
                        var sb = new StringBuilder();
                        sb.Append("NOTIFY * HTTP/1.1\r\n");
                        sb.Append("HOST: ").Append(MulticastAddress).Append(':').Append(SsdpPort).Append("\r\n");
                        if (alive)
                        {
                            sb.Append("CACHE-CONTROL: max-age=").Append(MaxAgeSeconds).Append("\r\n");
                            sb.Append("LOCATION: ").Append(LocationFor(li.Address)).Append("\r\n");
                        }
                        sb.Append("NT: ").Append(nt).Append("\r\n");
                        sb.Append("NTS: ").Append(alive ? "ssdp:alive" : "ssdp:byebye").Append("\r\n");
                        sb.Append("SERVER: ").Append(ServerHeader).Append("\r\n");
                        sb.Append("USN: ").Append(UsnFor(nt)).Append("\r\n\r\n");

                        byte[] data = Encoding.UTF8.GetBytes(sb.ToString());
                        s.SendTo(data, MulticastEndPoint);
                    }
                }
            }
        }

        /// <summary>URL de la descripcion del dispositivo accesible desde la IP indicada.</summary>
        private string LocationFor(IPAddress ip)
        {
            return "http://" + ip + ":" + _opt.Port + "/device.xml";
        }

        /// <summary>
        /// Extrae el valor de una cabecera de un mensaje SSDP (sin distinguir
        /// mayusculas). Devuelve null si no existe.
        /// </summary>
        public static string GetHeader(string message, string name)
        {
            foreach (var line in message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                int i = line.IndexOf(':');
                if (i <= 0) continue;
                if (string.Equals(line.Substring(0, i).Trim(), name, StringComparison.OrdinalIgnoreCase))
                    return line.Substring(i + 1).Trim();
            }
            return null;
        }
    }
}
