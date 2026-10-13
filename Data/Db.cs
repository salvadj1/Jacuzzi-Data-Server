using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace JacuzziServer.Data
{
    /// <summary>
    /// Acceso generico y reutilizable a una base de datos SQLite.
    /// No conoce ninguna tabla concreta: solo abre conexiones (modo WAL, mas
    /// resistente a cortes de luz) y ejecuta consultas con parametros.
    /// Puede copiarse tal cual a otros proyectos.
    /// </summary>
    public class Db
    {
        private readonly string _connectionString;
        private readonly int _timeoutSeconds;

        /// <summary>Crea el acceso a la base de datos.</summary>
        /// <param name="dbPath">Ruta completa del fichero .db (se crea si no existe).</param>
        /// <param name="timeoutSeconds">Espera maxima si la base esta bloqueada.</param>
        public Db(string dbPath, int timeoutSeconds = 5)
        {
            _connectionString = $"Data Source={dbPath}";
            _timeoutSeconds = timeoutSeconds;
        }

        /// <summary>
        /// Abre una conexion nueva con journal_mode=WAL. El llamador debe
        /// liberarla (using). Con WAL, las lecturas no bloquean a las escrituras.
        /// </summary>
        public SqliteConnection Open()
        {
            var conn = new SqliteConnection(_connectionString);
            conn.DefaultTimeout = _timeoutSeconds; // espera maxima si la base esta bloqueada
            conn.Open();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode=WAL;";
                cmd.ExecuteNonQuery();
            }
            return conn;
        }

        /// <summary>
        /// Ejecuta una sentencia de escritura (INSERT/UPDATE/DELETE/CREATE).
        /// Los parametros se referencian en el SQL como @p0, @p1, @p2...
        /// segun su posicion en <paramref name="args"/>.
        /// </summary>
        /// <returns>Numero de filas afectadas.</returns>
        public int Execute(string sql, params object[] args)
        {
            using (var conn = Open())
            using (var cmd = Build(conn, sql, args))
            {
                return cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Ejecuta una consulta SELECT (o PRAGMA) y devuelve todas las filas.
        /// Cada fila es un object[] con los valores nativos de SQLite
        /// (long, double, string); NULL se devuelve como null.
        /// Parametros: @p0, @p1... igual que en <see cref="Execute"/>.
        /// </summary>
        public List<object[]> Query(string sql, params object[] args)
        {
            var rows = new List<object[]>();
            using (var conn = Open())
            using (var cmd = Build(conn, sql, args))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var row = new object[reader.FieldCount];
                    for (int i = 0; i < row.Length; i++)
                        row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    rows.Add(row);
                }
            }
            return rows;
        }

        /// <summary>Crea el comando y enlaza los argumentos como @p0..@pN.</summary>
        private static SqliteCommand Build(SqliteConnection conn, string sql, object[] args)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            for (int i = 0; i < args.Length; i++)
                cmd.Parameters.AddWithValue("@p" + i, args[i] ?? System.DBNull.Value);
            return cmd;
        }
    }
}
