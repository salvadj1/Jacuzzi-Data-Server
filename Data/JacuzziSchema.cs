namespace JacuzziServer.Data
{
    /// <summary>
    /// Esquema de la base de datos del jacuzzi. Mismo esquema que el servidor
    /// original (FastAPI), por lo que un jacuzzi.db existente se puede reutilizar.
    /// </summary>
    public static class JacuzziSchema
    {
        /// <summary>Tablas que el panel de administracion puede listar y consultar.</summary>
        public static readonly string[] AdminTables = { "samples", "diag_samples" };

        /// <summary>
        /// Crea las tablas e indices si no existen (idempotente). Se llama al arrancar.
        /// - samples: temperaturas; ts es PRIMARY KEY, asi un reintento del ESP32 no duplica.
        /// - diag_samples: salud del sistema (heap, WiFi, motivos de reinicio...).
        /// </summary>
        public static void Init(Db db)
        {
            db.Execute(@"
                CREATE TABLE IF NOT EXISTS samples (
                    ts     INTEGER PRIMARY KEY,
                    t1     REAL NOT NULL,
                    t2     REAL NOT NULL,
                    flags  INTEGER NOT NULL
                )");

            db.Execute(@"
                CREATE TABLE IF NOT EXISTS diag_samples (
                    ts               INTEGER PRIMARY KEY,
                    free_heap        INTEGER NOT NULL,
                    min_free_heap    INTEGER NOT NULL,
                    max_alloc_heap   INTEGER NOT NULL,
                    uptime_sec       INTEGER NOT NULL,
                    max_loop_micros  INTEGER NOT NULL,
                    min_stack_bytes  INTEGER NOT NULL,
                    rssi             INTEGER NOT NULL,
                    ws_clients       INTEGER NOT NULL,
                    wifi_connected   INTEGER NOT NULL,
                    reset_reason     INTEGER NOT NULL,
                    reset_reason_text TEXT NOT NULL,
                    breadcrumb_text  TEXT NOT NULL,
                    wifi_reconnects  INTEGER NOT NULL,
                    ntc_errors       INTEGER NOT NULL,
                    event_class      INTEGER NOT NULL
                )");

            db.Execute("CREATE INDEX IF NOT EXISTS idx_diag_ts ON diag_samples(ts)");
        }
    }
}
