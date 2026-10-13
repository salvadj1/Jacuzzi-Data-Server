using JacuzziServer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace JacuzziServer.Filters
{
    /// <summary>
    /// Filtro de autenticacion minima por clave fija compartida con el ESP32
    /// (cabecera X-API-Key). Se aplica a los endpoints de escritura/borrado con
    /// [ServiceFilter(typeof(ApiKeyFilter))]. Responde 401 con {"detail": "..."}
    /// (mismo formato de error que el servidor original).
    /// Se ejecuta DESPUES de la validacion del modelo, igual que en el original.
    /// </summary>
    public class ApiKeyFilter : IActionFilter
    {
        private readonly string _apiKey;

        /// <summary>La clave esperada se lee de appsettings.json (Jacuzzi:ApiKey).</summary>
        public ApiKeyFilter(IOptions<JacuzziOptions> options)
        {
            _apiKey = options.Value.ApiKey;
        }

        /// <summary>Corta la peticion con 401 si la cabecera falta o no coincide.</summary>
        public void OnActionExecuting(ActionExecutingContext context)
        {
            string key = null;
            if (context.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var values) && values.Count > 0)
                key = values[0];

            if (key != _apiKey)
            {
                context.Result = new ObjectResult(new { detail = "API key invalida o ausente" })
                {
                    StatusCode = 401
                };
            }
        }

        /// <summary>Sin accion posterior.</summary>
        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
