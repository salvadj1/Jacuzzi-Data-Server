using System.ComponentModel.DataAnnotations;

namespace JacuzziServer.Models
{
    /// <summary>
    /// Muestra de diagnostico (salud del sistema) enviada por el ESP32 a POST /api/diag.
    /// Los textos y eventClass llegan ya calculados desde el ESP32; el servidor
    /// solo los guarda y los devuelve tal cual. Los nombres JSON son camelCase
    /// (la deserializacion no distingue mayusculas/minusculas).
    /// </summary>
    public class DiagSampleIn
    {
        [Required] public long? Ts { get; set; }
        [Required] public long? FreeHeap { get; set; }
        [Required] public long? MinFreeHeap { get; set; }
        [Required] public long? MaxAllocHeap { get; set; }
        [Required] public long? UptimeSec { get; set; }
        [Required] public long? MaxLoopMicros { get; set; }
        [Required] public long? MinStackBytes { get; set; }
        [Required] public long? Rssi { get; set; }
        [Required] public long? WsClients { get; set; }
        [Required] public long? WifiConnected { get; set; }
        [Required] public long? ResetReason { get; set; }

        /// <summary>Opcional: por defecto cadena vacia.</summary>
        public string ResetReasonText { get; set; } = "";

        /// <summary>Opcional: por defecto cadena vacia.</summary>
        public string BreadcrumbText { get; set; } = "";

        [Required] public long? WifiReconnects { get; set; }
        [Required] public long? NtcErrors { get; set; }
        [Required] public long? EventClass { get; set; }
    }
}
