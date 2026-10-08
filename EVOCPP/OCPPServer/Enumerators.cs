using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPPServer.ChargingStationInterface
{
    public class Enumerators
    {
        /// <summary>
        /// Defined types of EV charging connectors.
        /// </summary>
        public enum Type
        {
            Type1,
            Type2,
            CCS,
            CHAdeMO,
            Tesla
        }

        /// <summary>
        /// Result of registration in response to BootNotification.req.
        /// </summary>
        public enum RegistrationStatus
        {
            Accepted,
            Pending,
            Rejected
        }
        /// <summary>
        /// Status reported in StatusNotification.req. A status can be reported for the Charge Point main controller
        /// (connectorId = 0) or for a specific connector. Status for the Charge Point main controller is a subset of the
        /// enumeration: Available, Unavailable or Faulted.
        /// Full OCPP 1.6 set (section 7.27).
        /// </summary>
        public enum ChargePointStatus
        {
            Available,
            Preparing,
            Charging,
            SuspendedEVSE,
            SuspendedEV,
            Finishing,
            Reserved,
            Unavailable,
            Faulted
        }
    }
}
