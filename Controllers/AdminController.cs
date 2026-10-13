using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using JacuzziServer.Data;
using Microsoft.AspNetCore.Mvc;

namespace JacuzziServer.Controllers
{
    /// <summary>
    /// Endpoints de solo lectura para el panel web (esquema dinamico, filas de
    /// cualquier tabla permitida) y comprobacion de salud.
    /// </summary>
    [ApiController]
    [Route("api")]
    public class AdminController : ControllerBase
    {
        private readonly Db _db;

        public AdminController(Db db)
        {
            _db = db;
        }

        /// <summary>
        /// GET /api/schema - Columnas de cada tabla del panel (nombre, tipo, es_pk).
        /// Metadata, no datos: no requiere clave (igual que el original).
        /// </summary>
        [HttpGet("schema")]
        public IActionResult GetSchema()
        {
            var tablas = new Dictionary<string, object>();
            foreach (var tabla in JacuzziSchema.AdminTables)
            {
                // PRAGMA table_info: cid, name, type, notnull, dflt_value, pk
                var cols = _db.Query("PRAGMA table_info(" + tabla + ")");
                tablas[tabla] = cols.Select(c => new
                {
                    nombre = (string)c[1],
                    tipo = (string)c[2],
                    es_pk = Convert.ToInt64(c[5]) > 0
                }).ToList();
            }
            return Ok(new { tablas });
        }

        /// <summary>
        /// GET /api/table/{tabla} - Filas de una tabla permitida, mas recientes
        /// primero, junto con sus columnas. 404 si la tabla no esta en la lista.
        /// </summary>
        /// <param name="limite">Maximo de filas (hasta 2000).</param>
        /// <param name="desde">Epoch segundos, columna ts, inclusive.</param>
        /// <param name="hasta">Epoch segundos, columna ts, exclusive.</param>
        [HttpGet("table/{tabla}")]
        public IActionResult GetTableRows(
            string tabla,
            [FromQuery, Range(int.MinValue, 2000)] int limite = 200,
            [FromQuery] long? desde = null,
            [FromQuery] long? hasta = null)
        {
            if (!JacuzziSchema.AdminTables.Contains(tabla))
                return NotFound(new { detail = "tabla no reconocida" });

            var columnas = _db.Query("PRAGMA table_info(" + tabla + ")")
                              .Select(c => (string)c[1]).ToList();

            long h = hasta ?? EpochClock.Now() + 1;
            long d = desde ?? 0;

            // "tabla" solo llega aqui si esta en la lista blanca: no hay inyeccion posible.
            var filas = _db.Query(
                "SELECT * FROM " + tabla + " WHERE ts >= @p0 AND ts < @p1 ORDER BY ts DESC LIMIT @p2",
                d, h, (long)limite);

            return Ok(new { columnas, filas });
        }

        /// <summary>GET /api/health - Comprueba que el servidor esta vivo.</summary>
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { ok = true, time = EpochClock.Now() });
        }
    }
}
