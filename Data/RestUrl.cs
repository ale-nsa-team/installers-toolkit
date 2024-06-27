﻿using PoEWizard.Exceptions;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace PoEWizard.Data
{
    public class RestUrl
    {

        public const string REST_URL = "REQUEST_URL";
        public const string RESULT = "RESULT";
        public const string RESPONSE = "RESPONSE";
        public const string DURATION = "DURATION";
        public const string API_ERROR = "error";
        public const string OUTPUT = "output";
        public const string NODE = "node";
        public const string HTTP_RESPONSE = "diag";

        public const string DATA_0 = "%1_DATA_1%";
        public const string DATA_1 = "%2_DATA_2%";
        public const string DATA_2 = "%3_DATA_3%";
        public const string DATA_3 = "%4_DATA_4%";

        public enum RestUrlId
        {
            // 0 - 19: Basic commands to gather switch data
            SHOW_SYSTEM = 0,
            SHOW_CHASSIS = 1,
            SHOW_PORTS_LIST = 2,
            SHOW_POWER_SUPPLY = 3,
            SHOW_LAN_POWER = 4,
            SHOW_LAN_POWER_STATUS = 5,
            SHOW_SLOT = 6,
            SHOW_MAC_LEARNING = 7,
            SHOW_TEMPERATURE = 8,
            SHOW_HEALTH = 9,
            // 20 - 39: Commands related to actions on power
            POWER_DOWN_PORT = 20,
            POWER_UP_PORT = 21,
            POWER_PRIORITY_PORT = 22,
            POWER_4PAIR_PORT = 23,
            POWER_2PAIR_PORT = 24,
            POWER_DOWN_SLOT = 25,
            POWER_UP_SLOT = 26,
            POWER_823BT_ENABLE = 27,
            POWER_823BT_DISABLE = 28,
            POWER_HDMI_ENABLE = 29,
            POWER_HDMI_DISABLE = 30,
            LLDP_POWER_MDI_ENABLE = 31,
            LLDP_POWER_MDI_DISABLE = 32,
            LLDP_EXT_POWER_MDI_ENABLE = 33,
            LLDP_EXT_POWER_MDI_DISABLE = 34,
            POE_FAST_ENABLE = 35,
            POE_PERPETUAL_ENABLE = 36,
            // 40 - 59: Special switch commands
            WRITE_MEMORY = 40
        }

        public static Dictionary<RestUrlId, string> REST_URL_TABLE = new Dictionary<RestUrlId, string>
        {
            // 0 - 19: Basic commands to gather switch data
            [RestUrlId.SHOW_SYSTEM] = "cli/aos?cmd=show system",                                            //  0
            [RestUrlId.SHOW_CHASSIS] = "cli/aos?cmd=show chassis",                                          //  1
            [RestUrlId.SHOW_PORTS_LIST] = "cli/aos?cmd=show interfaces status",                             //  2
            [RestUrlId.SHOW_POWER_SUPPLY] = $"cli/aos?cmd=show powersupply {DATA_0}",                       //  3
            [RestUrlId.SHOW_LAN_POWER] = $"cli/aos?cmd=show lanpower slot {DATA_0}",                        //  4
            [RestUrlId.SHOW_LAN_POWER_STATUS] = $"cli/aos?cmd=show lanpower slot {DATA_0} status",          //  5
            [RestUrlId.SHOW_SLOT] = $"cli/aos?cmd=show slot {DATA_0}",                                      //  6
            [RestUrlId.SHOW_MAC_LEARNING] = $"cli/aos?cmd=show mac-learning",                               //  7
            [RestUrlId.SHOW_TEMPERATURE] = $"cli/aos?cmd=show temperature",                                 //  8
            [RestUrlId.SHOW_HEALTH] = $"cli/aos?cmd=show health",                                           //  9
            // 20 - 39: Commands related to actions on power
            [RestUrlId.POWER_DOWN_PORT] = $"cli/aos?cmd=lanpower port {DATA_0} admin-state disable",        // 20
            [RestUrlId.POWER_UP_PORT] = $"cli/aos?cmd=lanpower port {DATA_0} admin-state enable",           // 21
            [RestUrlId.POWER_PRIORITY_PORT] = $"lanpower port {DATA_0} priority {DATA_1}",                  // 22
            [RestUrlId.POWER_4PAIR_PORT] = $"cli/aos?cmd=lanpower port {DATA_0} 4pair enable",              // 23
            [RestUrlId.POWER_2PAIR_PORT] = $"cli/aos?cmd=lanpower port {DATA_0} 4pair disable",             // 24
            [RestUrlId.POWER_DOWN_SLOT] = $"cli/aos?cmd=lanpower slot {DATA_0} service stop",               // 25
            [RestUrlId.POWER_UP_SLOT] = $"cli/aos?cmd=lanpower slot {DATA_0} service start",                // 26
            [RestUrlId.POWER_823BT_ENABLE] = $"cli/aos?cmd=lanpower slot {DATA_0} 8023bt enable",           // 27
            [RestUrlId.POWER_823BT_DISABLE] = $"cli/aos?cmd=lanpower slot {DATA_0} 8023bt disable",         // 28
            [RestUrlId.POWER_HDMI_ENABLE] = $"cli/aos?cmd=lanpower port {DATA_0} power-over-hdmi enable",   // 29
            [RestUrlId.POWER_HDMI_DISABLE] = $"cli/aos?cmd=lanpower port {DATA_0} power-over-hdmi disable", // 30
            [RestUrlId.LLDP_POWER_MDI_ENABLE] = $"cli/aos?cmd=lldp nearest-bridge port {DATA_0} tlv dot3 power-via-mdi enable",          // 31
            [RestUrlId.LLDP_POWER_MDI_DISABLE] = $"cli/aos?cmd=lldp nearest-bridge port {DATA_0} tlv dot3 power-via-mdi disable",        // 32
            [RestUrlId.LLDP_EXT_POWER_MDI_ENABLE] = $"cli/aos?cmd=lldp nearest-bridge port {DATA_0} tlv med ext-power-via-mdi enable",   // 33
            [RestUrlId.LLDP_EXT_POWER_MDI_DISABLE] = $"cli/aos?cmd=lldp nearest-bridge port {DATA_0} tlv med ext-power-via-mdi disable", // 34
            [RestUrlId.POE_FAST_ENABLE] = $"cli/aos?cmd=lanpower slot {DATA_0} fpoe enable",                // 35
            [RestUrlId.POE_PERPETUAL_ENABLE] = $"cli/aos?cmd=lanpower slot {DATA_0} ppoe enable",           // 36
            // 40 - 59: Special switch commands
            [RestUrlId.WRITE_MEMORY] = "cli/aos?cmd=write memory flash-synchro"                             // 40
        };

        public static string ParseUrl(RestUrlEntry entry)
        {
            string url = GetUrlFromTable(entry.RestUrl, entry.Data).Trim();
            string[] urlSplit = url.Split('=');
            url = $"{urlSplit[0]}={urlSplit[1].Replace(" ", "%20").Replace("/", "%2F")}";
            return url;
        }

        private static string GetUrlFromTable(RestUrlId restUrlId, string[] data)
        {
            if (REST_URL_TABLE.ContainsKey(restUrlId))
            {
                string url = REST_URL_TABLE[restUrlId];
                switch (restUrlId)
                {
                    // 0 - 19: Basic commands to gather switch data
                    case RestUrlId.SHOW_SYSTEM:                 //  0
                    case RestUrlId.SHOW_CHASSIS:                //  1
                    case RestUrlId.SHOW_PORTS_LIST:             //  2
                    case RestUrlId.SHOW_POWER_SUPPLY:           //  3
                    case RestUrlId.SHOW_SLOT:                   //  6
                    case RestUrlId.SHOW_MAC_LEARNING:           //  7
                    case RestUrlId.SHOW_TEMPERATURE:            //  8
                    case RestUrlId.SHOW_HEALTH:                 //  9
                    // 20 - 39: Commands related to actions on power
                    case RestUrlId.WRITE_MEMORY:                // 40
                        return url;

                    // 0 - 19: Basic commands to gather switch data
                    case RestUrlId.SHOW_LAN_POWER:              //  4
                    case RestUrlId.SHOW_LAN_POWER_STATUS:       //  5
                    // 20 - 39: Commands related to actions on power
                    case RestUrlId.POWER_DOWN_PORT:             // 20
                    case RestUrlId.POWER_UP_PORT:               // 21
                    case RestUrlId.POWER_4PAIR_PORT:            // 23
                    case RestUrlId.POWER_2PAIR_PORT:            // 24
                    case RestUrlId.POWER_DOWN_SLOT:             // 25
                    case RestUrlId.POWER_UP_SLOT:               // 26
                    case RestUrlId.POWER_823BT_ENABLE:          // 27
                    case RestUrlId.POWER_823BT_DISABLE:         // 28
                    case RestUrlId.POWER_HDMI_ENABLE:           // 29
                    case RestUrlId.POWER_HDMI_DISABLE:          // 30
                    case RestUrlId.LLDP_POWER_MDI_ENABLE:       // 31
                    case RestUrlId.LLDP_POWER_MDI_DISABLE:      // 32
                    case RestUrlId.LLDP_EXT_POWER_MDI_ENABLE:   // 33
                    case RestUrlId.LLDP_EXT_POWER_MDI_DISABLE:  // 34
                    case RestUrlId.POE_FAST_ENABLE:             // 35
                    case RestUrlId.POE_PERPETUAL_ENABLE:        // 36
                        if (data == null || data.Length < 1) throw new SwitchCommandError($"Invalid url {Utils.PrintEnum(restUrlId)}!");
                        return url.Replace(DATA_0, (data == null || data.Length < 1) ? "" : data[0]);

                    // 20 - 39: Commands related to actions on power
                    case RestUrlId.POWER_PRIORITY_PORT:     // 22
                        if (data == null || data.Length < 2) throw new SwitchCommandError($"Invalid url {Utils.PrintEnum(restUrlId)}!");
                        return url.Replace(DATA_0, data[0]).Replace(DATA_1, data[1]);

                    default:
                        throw new SwitchCommandError($"Invalid url {Utils.PrintEnum(restUrlId)}!");
                }
            }
            else
            {
                throw new SwitchCommandError($"Invalid url {Utils.PrintEnum(restUrlId)}!");
            }
        }

    }
}
