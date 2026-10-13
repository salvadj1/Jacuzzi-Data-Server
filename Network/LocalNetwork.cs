using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace JacuzziServer.Network
{
    /// <summary>Direccion IPv4 local junto con su mascara de subred.</summary>
    public class LocalIPv4
    {
        /// <summary>Direccion IPv4 de la interfaz.</summary>
        public IPAddress Address { get; set; }

        /// <summary>Mascara de subred de la interfaz.</summary>
        public IPAddress Mask { get; set; }
    }

    /// <summary>
    /// Utilidades para consultar las interfaces de red locales.
    /// Clase estatica sin dependencias del proyecto: reutilizable tal cual.
    /// </summary>
    public static class LocalNetwork
    {
        /// <summary>
        /// Devuelve las direcciones IPv4 de las interfaces activas con multicast
        /// (se excluyen loopback, tuneles y direcciones APIPA 169.254.x.x).
        /// Se consulta en cada llamada, asi que refleja cambios de IP en caliente.
        /// </summary>
        public static List<LocalIPv4> GetIPv4Interfaces()
        {
            var result = new List<LocalIPv4>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                if (!ni.SupportsMulticast) continue;

                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    byte[] b = ua.Address.GetAddressBytes();
                    if (b[0] == 169 && b[1] == 254) continue; // APIPA: sin red real

                    result.Add(new LocalIPv4 { Address = ua.Address, Mask = ua.IPv4Mask });
                }
            }
            return result;
        }

        /// <summary>
        /// Elige la IP local que esta en la misma subred que <paramref name="remote"/>
        /// (la que el otro equipo puede alcanzar). Si ninguna coincide, devuelve la
        /// primera disponible; si no hay ninguna, null.
        /// </summary>
        public static LocalIPv4 PickFor(IPAddress remote, List<LocalIPv4> locals)
        {
            foreach (var l in locals)
                if (SameSubnet(l.Address, remote, l.Mask)) return l;
            return locals.Count > 0 ? locals[0] : null;
        }

        /// <summary>Indica si dos direcciones IPv4 pertenecen a la misma subred segun la mascara.</summary>
        public static bool SameSubnet(IPAddress a, IPAddress b, IPAddress mask)
        {
            if (a == null || b == null || mask == null) return false;
            byte[] ab = a.GetAddressBytes(), bb = b.GetAddressBytes(), mb = mask.GetAddressBytes();
            if (ab.Length != 4 || bb.Length != 4 || mb.Length != 4) return false;
            for (int i = 0; i < 4; i++)
                if ((ab[i] & mb[i]) != (bb[i] & mb[i])) return false;
            return true;
        }
    }
}
