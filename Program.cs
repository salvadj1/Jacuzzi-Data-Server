using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;

namespace JacuzziServer
{
    /// <summary>
    /// Punto de entrada. Funciona igual como consola (dotnet run / F5 en VS)
    /// que como servicio de Windows (UseWindowsService detecta el contexto).
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        /// <summary>
        /// Construye el host. UseWindowsService() hace dos cosas cuando corre
        /// como servicio: integra el ciclo de vida con el SCM de Windows y
        /// fija la carpeta del ejecutable como ContentRoot (asi appsettings.json,
        /// wwwroot y jacuzzi.db se buscan junto al .exe y no en System32).
        /// Fuera de un servicio no tiene ningun efecto.
        /// </summary>
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
    }
}
