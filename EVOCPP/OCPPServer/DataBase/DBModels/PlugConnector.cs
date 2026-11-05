using System.ComponentModel.DataAnnotations;

namespace OCPPServer.DataBase.DBModels
{
    /// <summary>
    /// One connector of a charging station (<see cref="Plug"/> = the station / OCPP id).
    /// A two-connector charger (Wallbox Supernova) reports connectorId 1 and 2 under one OCPP id,
    /// each with its own meter and session.
    ///
    /// Holds what must survive an OCPP server restart: the last status and the running
    /// transaction with its MeterStartWh (a StopTransaction after a restart carries only the
    /// transactionId — this row tells which connector and baseline it belongs to).
    /// Created on the first StatusNotification / StartTransaction for the connector.
    /// </summary>
    public class PlugConnector
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(128)]
        public string OcppId { get; set; } = null!;

        /// <summary>OCPP 1.6 connectorId / OCPP 2.x evseId, &gt; 0.</summary>
        public int ConnectorId { get; set; }

        /// <summary>Last OCPP status string as sent by the charger.</summary>
        [MaxLength(32)]
        public string? Status { get; set; }

        public DateTime? LastStatusUpdate { get; set; }

        /// <summary>Local transaction ID of the running session; null when idle.</summary>
        public int? TransactionId { get; set; }

        /// <summary>Meter reading (Wh) at the start of the running session; null when idle.</summary>
        public decimal? MeterStartWh { get; set; }
    }
}
