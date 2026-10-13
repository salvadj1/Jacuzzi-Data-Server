using System;

namespace JacuzziServer.Data
{
    /// <summary>Utilidad reutilizable para trabajar con epoch en segundos (UTC).</summary>
    public static class EpochClock
    {
        /// <summary>Segundos epoch actuales.</summary>
        public static long Now()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
