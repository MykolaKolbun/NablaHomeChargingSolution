namespace OCPPServer.DataBase.DBModels
{
    public class Connector
    {
        /// <summary>
        /// Defines the unique identifier of the connector.
        /// </summary> 
        int Id { get; set; }

        /// <summary>
        /// Defines the OCPP identifier of the connector.
        /// </summary>
        string OcppId { get; set; }

        /// <summary>
        /// Defines the status of the connector.
        /// </summary>
        string Status { get; set; }

        /// <summary>
        /// Defines the description of the connector status.
        /// </summary>
        string StatusDescription { get; set; }

        /// <summary>
        /// Defines the meter value of the connector.
        /// </summary>
        decimal MeterValue { get; set; }

        /// <summary>
        /// Last Update date
        /// </summary>
        public DateTime LastUpdate { get; set; }

        /// <summary>
        /// DateTime of creation
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
