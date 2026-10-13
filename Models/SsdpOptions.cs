namespace JacuzziServer.Models
{
    /// <summary>
    /// Opciones del anuncio SSDP/UPnP (aparecer en Explorador de Windows -> Red).
    /// Enlazadas a la seccion "Ssdp" de appsettings.json. Reutilizable en otros proyectos.
    /// </summary>
    public class SsdpOptions
    {
        /// <summary>Activa o desactiva el anuncio en la red local.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Nombre que se muestra en Red (se le anade el nombre del PC entre parentesis).</summary>
        public string FriendlyName { get; set; } = "Jacuzzi Server";

        /// <summary>Puerto HTTP del servidor. Debe coincidir con el de "Urls" en appsettings.json.</summary>
        public int Port { get; set; } = 8000;

        /// <summary>Tipo de dispositivo UPnP. "Basic" es el generico para dispositivos con pagina web.</summary>
        public string DeviceType { get; set; } = "urn:schemas-upnp-org:device:Basic:1";
    }
}
