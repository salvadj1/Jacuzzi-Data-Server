namespace JacuzziServer.Models
{
    /// <summary>
    /// Opciones del servidor, enlazadas a la seccion "Jacuzzi" de appsettings.json.
    /// Equivalen a las constantes de configuracion del antiguo main.py.
    /// </summary>
    public class JacuzziOptions
    {
        /// <summary>Clave compartida con el ESP32 (cabecera X-API-Key). Debe coincidir con REMOTE_LOG_API_KEY de config.h.</summary>
        public string ApiKey { get; set; } = "cambia-esta-clave";

        /// <summary>Ruta de la base de datos SQLite. Si es relativa, se resuelve respecto a la carpeta del ejecutable.</summary>
        public string DbPath { get; set; } = "jacuzzi.db";

        /// <summary>Dias que devuelve /api/history si no se indica rango.</summary>
        public int HistoryDefaultDays { get; set; } = 2;

        /// <summary>Horas que devuelve /api/diag si no se indica rango.</summary>
        public int DiagDefaultHours { get; set; } = 12;

        /// <summary>Tope de filas por respuesta (red de seguridad para la RAM del ESP32).</summary>
        public int MaxRowsHardLimit { get; set; } = 3000;
    }
}
