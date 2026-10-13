using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using JacuzziServer.Data;
using JacuzziServer.Filters;
using JacuzziServer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace JacuzziServer.Controllers
{
    /// <summary>
    /// Historico de temperaturas: guardado desde el ESP32, consulta (cruda y
    /// agregada) y borrado. Mismas rutas y formato JSON que el servidor original.
    /// </summary>
    [ApiController]
    [Route("api")]
    public class DataController : ControllerBase
    {
        private readonly Db _db;
        private readonly JacuzziOptions _opt;

        public DataController(Db db, IOptions<JacuzziOptions> options)
        {
            _db = db;
            _opt = options.Value;
        }

        /// <summary>
        /// POST /api/data - Guarda una muestra. Idempotente: si ya existe una
        /// con el mismo ts (reintento del ESP32) se sobreescribe sin duplicar.
        /// </summary>
        [HttpPost("data")]
        [ServiceFilter(typeof(ApiKeyFilter))]
        public IActionResult StoreSample([FromBody] SampleIn sample)
        {
            if (sample.Ts.Value <= 0)
                return BadRequest(new { detail = "timestamp invalido" });

            _db.Execute(
                "INSERT INTO samples (ts, t1, t2, flags) VALUES (@p0, @p1, @p2, @p3) " +
                "ON CONFLICT(ts) DO UPDATE SET t1=excluded.t1, t2=excluded.t2, flags=excluded.flags",
                sample.Ts.Value, sample.T1.Value, sample.T2.Value, sample.Flags.Value);

            return Ok(new { ok = true });
        }

        /// <summary>
        /// GET /api/history - Devuelve {"samples":[[ts,t1,t2,flags],...]}.
        /// Sin fechas: ultimos HistoryDefaultDays dias. Tope MaxRowsHardLimit
        /// filas, quedandose con las MAS RECIENTES (orden final cronologico).
        /// </summary>
        /// <param name="desde">Epoch segundos, inclusive.</param>
        /// <param name="hasta">Epoch segundos, exclusive.</param>
        [HttpGet("history")]
        public IActionResult GetHistory([FromQuery] long? desde, [FromQuery] long? hasta)
        {
            long h = hasta ?? EpochClock.Now() + 1;
            long d = desde ?? h - _opt.HistoryDefaultDays * 86400L;

            var rows = _db.Query(
                "SELECT ts, t1, t2, flags FROM samples WHERE ts >= @p0 AND ts < @p1 " +
                "ORDER BY ts DESC LIMIT @p2",
                d, h, (long)_opt.MaxRowsHardLimit);
            rows.Reverse(); // se pidieron las mas recientes primero; se devuelven en orden cronologico

            return Ok(new { samples = rows });
        }

        /// <summary>
        /// GET /api/history/agregado - Media de T1/T2 por hora o por dia
        /// (periodo = "hora" | "dia"). Devuelve [[ts_minimo_del_grupo, t1, t2],...].
        /// Sin fechas: ultimo ano.
        /// </summary>
        [HttpGet("history/agregado")]
        public IActionResult GetHistoryAggregated(
            [FromQuery, RegularExpression("^(hora|dia)$")] string periodo = "dia",
            [FromQuery] long? desde = null,
            [FromQuery] long? hasta = null)
        {
            long h = hasta ?? EpochClock.Now() + 1;
            long d = desde ?? h - 365 * 86400L;

            // El formato sale de una constante validada por la regex, no del usuario.
            string fmt = periodo == "dia" ? "%Y-%m-%d" : "%Y-%m-%d %H:00";

            var rows = _db.Query(
                "SELECT strftime('" + fmt + "', ts, 'unixepoch') AS bucket, AVG(t1), AVG(t2), MIN(ts) " +
                "FROM samples WHERE ts >= @p0 AND ts < @p1 GROUP BY bucket ORDER BY bucket ASC",
                d, h);

            var puntos = new List<object[]>();
            foreach (var r in rows)
                puntos.Add(new object[] { r[3], Math.Round((double)r[1], 2), Math.Round((double)r[2], 2) });

            return Ok(new { periodo, samples = puntos });
        }

        /// <summary>POST /api/history/deleteday?from=&amp;to= - Borra las muestras en [from, to).</summary>
        [HttpPost("history/deleteday")]
        [ServiceFilter(typeof(ApiKeyFilter))]
        public IActionResult DeleteRange(
            [FromQuery(Name = "from"), Required] long? fromTs,
            [FromQuery(Name = "to"), Required] long? toTs)
        {
            _db.Execute("DELETE FROM samples WHERE ts >= @p0 AND ts < @p1", fromTs.Value, toTs.Value);
            return Ok(new { ok = true });
        }

        /// <summary>POST /api/history/format - Borra TODO el historico de temperaturas.</summary>
        [HttpPost("history/format")]
        [ServiceFilter(typeof(ApiKeyFilter))]
        public IActionResult FormatAll()
        {
            _db.Execute("DELETE FROM samples");
            return Ok(new { ok = true });
        }
    }
}
