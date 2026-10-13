using System.ComponentModel.DataAnnotations;

namespace JacuzziServer.Models
{
    /// <summary>
    /// Muestra de temperaturas enviada por el ESP32 a POST /api/data.
    /// Los campos son anulables + [Required] para que, igual que en el
    /// servidor original, un campo ausente produzca error de validacion
    /// (y no un 0 silencioso).
    /// </summary>
    public class SampleIn
    {
        /// <summary>Epoch en segundos.</summary>
        [Required] public long? Ts { get; set; }

        /// <summary>Temperatura del jacuzzi (C).</summary>
        [Required] public double? T1 { get; set; }

        /// <summary>Temperatura solar (C).</summary>
        [Required] public double? T2 { get; set; }

        /// <summary>Mascara de bits: 1=bomba, 2=auto, 4=valvulas, 8=solar forzado.</summary>
        [Required] public int? Flags { get; set; }
    }
}
