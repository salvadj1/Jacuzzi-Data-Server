using JacuzziServer.Data;
using JacuzziServer.Filters;
using JacuzziServer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JacuzziServer.Controllers
{
    /// <summary>
    /// Historico de diagnostico (salud del sistema: heap, WiFi, reinicios...).
    /// Mismas rutas, columnas y orden que el servidor original.
    /// </summary>
    [ApiController]
    [Route("api/diag")]
    public class DiagController : ControllerBase
    {
        private readonly Db _db;
        private readonly JacuzziOptions _opt;

        public DiagController(Db db, IOptions<JacuzziOptions> options)
        {
            _db = db;
            _opt = options.Value;
        }

        /// <summary>
        /// POST /api/diag - Guarda una muestra de diagnostico. Idempotente por ts
        /// (un reintento del ESP32 no duplica).
        /// </summary>
        [HttpPost]
        [ServiceFilter(typeof(ApiKeyFilter))]
        public IActionResult StoreDiag([FromBody] DiagSampleIn s)
        {
            if (s.Ts.Value < 0)
                return BadRequest(new { detail = "timestamp invalido" });

            _db.Execute(
                @"INSERT INTO diag_samples (
                    ts, free_heap, min_free_heap, max_alloc_heap, uptime_sec,
                    max_loop_micros, min_stack_bytes, rssi, ws_clients,
                    wifi_connected, reset_reason, reset_reason_text,
                    breadcrumb_text, wifi_reconnects, ntc_errors, event_class
                  ) VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15)
                  ON CONFLICT(ts) DO UPDATE SET
                    free_heap=excluded.free_heap, min_free_heap=excluded.min_free_heap,
                    max_alloc_heap=excluded.max_alloc_heap, uptime_sec=excluded.uptime_sec,
                    max_loop_micros=excluded.max_loop_micros, min_stack_bytes=excluded.min_stack_bytes,
                    rssi=excluded.rssi, ws_clients=excluded.ws_clients,
                    wifi_connected=excluded.wifi_connected, reset_reason=excluded.reset_reason,
                    reset_reason_text=excluded.reset_reason_text, breadcrumb_text=excluded.breadcrumb_text,
                    wifi_reconnects=excluded.wifi_reconnects, ntc_errors=excluded.ntc_errors,
                    event_class=excluded.event_class",
                s.Ts.Value, s.FreeHeap.Value, s.MinFreeHeap.Value, s.MaxAllocHeap.Value,
                s.UptimeSec.Value, s.MaxLoopMicros.Value, s.MinStackBytes.Value, s.Rssi.Value,
                s.WsClients.Value, s.WifiConnected.Value, s.ResetReason.Value,
                s.ResetReasonText ?? "", s.BreadcrumbText ?? "", s.WifiReconnects.Value,
                s.NtcErrors.Value, s.EventClass.Value);

            return Ok(new { ok = true });
        }

        /// <summary>
        /// GET /api/diag - Devuelve {"intervalMs":0,"samples":[[16 columnas],...]}.
        /// Sin fechas: ultimas DiagDefaultHours horas. Tope MaxRowsHardLimit filas
        /// (las mas recientes, en orden cronologico). intervalMs es un ajuste del
        /// ESP32, por eso aqui siempre es 0.
        /// </summary>
        [HttpGet]
        public IActionResult GetDiag([FromQuery] long? desde, [FromQuery] long? hasta)
        {
            long h = hasta ?? EpochClock.Now() + 1;
            long d = desde ?? h - _opt.DiagDefaultHours * 3600L;

            var rows = _db.Query(
                @"SELECT ts, free_heap, min_free_heap, max_alloc_heap, uptime_sec,
                         max_loop_micros, min_stack_bytes, rssi, ws_clients,
                         wifi_connected, reset_reason, reset_reason_text,
                         breadcrumb_text, wifi_reconnects, ntc_errors, event_class
                  FROM diag_samples WHERE ts >= @p0 AND ts < @p1
                  ORDER BY ts DESC LIMIT @p2",
                d, h, (long)_opt.MaxRowsHardLimit);
            rows.Reverse();

            return Ok(new { intervalMs = 0, samples = rows });
        }

        /// <summary>POST /api/diag/clear - Borra TODO el historico de diagnostico.</summary>
        [HttpPost("clear")]
        [ServiceFilter(typeof(ApiKeyFilter))]
        public IActionResult ClearDiag()
        {
            _db.Execute("DELETE FROM diag_samples");
            return Ok(new { ok = true });
        }
    }
}
