using System;
using System.Security;
using System.Text;
using JacuzziServer.Models;
using JacuzziServer.Network;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JacuzziServer.Controllers
{
    /// <summary>
    /// Sirve la descripcion UPnP del dispositivo (/device.xml). Windows la lee tras
    /// recibir el anuncio SSDP para mostrar el nombre en Explorador -> Red y para
    /// saber a que URL abrir al hacer doble clic (presentationURL).
    /// </summary>
    [ApiController]
    public class DeviceController : ControllerBase
    {
        private readonly SsdpOptions _opt;

        public DeviceController(IOptions<SsdpOptions> options)
        {
            _opt = options.Value;
        }

        /// <summary>
        /// GET /device.xml - Descripcion UPnP basica. La presentationURL se construye
        /// con el host que uso el cliente, asi siempre es una direccion que le funciona.
        /// </summary>
        [HttpGet("device.xml")]
        public ContentResult GetDeviceDescription()
        {
            string name = SecurityElement.Escape(_opt.FriendlyName + " (" + Environment.MachineName + ")");
            string type = SecurityElement.Escape(_opt.DeviceType);
            string presentation = SecurityElement.Escape(Request.Scheme + "://" + Request.Host + "/");

            string xml =
                "<?xml version=\"1.0\"?>\r\n" +
                "<root xmlns=\"urn:schemas-upnp-org:device-1-0\">\r\n" +
                "  <specVersion><major>1</major><minor>0</minor></specVersion>\r\n" +
                "  <device>\r\n" +
                "    <deviceType>" + type + "</deviceType>\r\n" +
                "    <friendlyName>" + name + "</friendlyName>\r\n" +
                "    <manufacturer>Jacuzzi</manufacturer>\r\n" +
                "    <modelName>JacuzziServer</modelName>\r\n" +
                "    <UDN>uuid:" + SsdpAnnouncer.BuildUuid() + "</UDN>\r\n" +
                "    <presentationURL>" + presentation + "</presentationURL>\r\n" +
                "  </device>\r\n" +
                "</root>";

            return Content(xml, "text/xml", Encoding.UTF8);
        }
    }
}
