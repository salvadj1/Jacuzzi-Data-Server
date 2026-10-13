using System.IO;
using JacuzziServer.Data;
using JacuzziServer.Filters;
using JacuzziServer.Models;
using JacuzziServer.Network;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace JacuzziServer
{
    /// <summary>Configuracion de servicios y del pipeline HTTP.</summary>
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        /// <summary>Registra opciones, base de datos, filtro de clave y controladores.</summary>
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<JacuzziOptions>(Configuration.GetSection("Jacuzzi"));

            // Base de datos: ruta relativa => junto al ejecutable (ContentRoot).
            services.AddSingleton(sp =>
            {
                var opt = sp.GetRequiredService<IOptions<JacuzziOptions>>().Value;
                var env = sp.GetRequiredService<IWebHostEnvironment>();
                string path = Path.IsPathRooted(opt.DbPath)
                    ? opt.DbPath
                    : Path.Combine(env.ContentRootPath, opt.DbPath);
                return new Db(path);
            });

            services.AddScoped<ApiKeyFilter>();

            // Anuncio SSDP/UPnP: hace que el servidor aparezca en Explorador -> Red.
            services.Configure<SsdpOptions>(Configuration.GetSection("Ssdp"));
            services.AddHostedService<SsdpAnnouncer>();

            services.AddControllers().ConfigureApiBehaviorOptions(o =>
            {
                // Los errores de validacion devuelven 422, como FastAPI.
                o.InvalidModelStateResponseFactory = ctx =>
                    new UnprocessableEntityObjectResult(new ValidationProblemDetails(ctx.ModelState));
            });
        }

        /// <summary>Crea las tablas y define el pipeline: panel estatico en "/" + API.</summary>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            JacuzziSchema.Init(app.ApplicationServices.GetRequiredService<Db>());

            app.UseDefaultFiles();   // "/" => wwwroot/index.html
            app.UseStaticFiles();
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }
    }
}
