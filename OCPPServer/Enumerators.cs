using System;
using System.Collections.Generic;
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
    }
}
