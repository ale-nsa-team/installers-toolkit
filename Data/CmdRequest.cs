﻿using System.Linq;
using static PoEWizard.Data.Constants;

namespace PoEWizard.Data
{
    public enum Command
    {
        CAPACITOR_DETECTION_DISABLE,
        CAPACITOR_DETECTION_ENABLE,
        CHANGE_MAX_POWER,
        CHECK_823BT,
        CHECK_CAPACITOR_DETECTION,
        CHECK_MAX_POWER,
        CHECK_POWER_PRIORITY,
        CLEAR_SWLOG,
        CLEAR_TDR_STATISTICS,
        DEBUG_CLI_SHOW_LPCMM_LEVEL,
        DEBUG_CLI_SHOW_LPNI_LEVEL,
        DEBUG_CLI_UPDATE_LPCMM_LEVEL,
        DEBUG_CLI_UPDATE_LPNI_LEVEL,
        DEBUG_CREATE_LOG,
        DEBUG_SHOW_APP_LIST,
        DEBUG_SHOW_LAN_POWER_STATUS,
        DEBUG_SHOW_LEVEL,
        DEBUG_SHOW_LPCMM_LEVEL,
        DEBUG_SHOW_LPNI_LEVEL,
        DEBUG_UPDATE_LLDPNI_LEVEL,
        DEBUG_UPDATE_LPCMM_LEVEL,
        DEBUG_UPDATE_LPNI_LEVEL,
        DELETE_COMMUNITY,
        DELETE_DNS_SERVER,
        DELETE_NTP_SERVER,
        DELETE_STATION,
        DELETE_USER,
        DHCP_RELAY_DEST,
        DISABLE_AUTO_FABRIC,
        DISABLE_DHCP_RELAY,
        DISABLE_FTP,
        DISABLE_MULTICAST,
        DISABLE_NTP,
        DISABLE_QUERYING,
        DISABLE_SSH,
        DISABLE_TELNET,
        DNS_DOMAIN,
        DNS_LOOKUP,
        ENABLE_DDM,
        ENABLE_DHCP_RELAY,
        ENABLE_FTP,
        ENABLE_MGT_VLAN,
        ENABLE_MULTICAST,
        ENABLE_NTP,
        ENABLE_QUERIER_FWD,
        ENABLE_QUERYING,
        ENABLE_SPAN_TREE,
        ENABLE_SSH,
        ENABLE_TDR,
        ENABLE_TELNET,
        ETHERNET_DISABLE,
        ETHERNET_ENABLE,
        FTP_AUTH_LOCAL,
        LLDP_ADDRESS_ENABLE,
        LLDP_EXT_POWER_MDI_DISABLE,
        LLDP_EXT_POWER_MDI_ENABLE,
        LLDP_POWER_MDI_DISABLE,
        LLDP_POWER_MDI_ENABLE,
        LLDP_SYSTEM_DESCRIPTION_ENABLE,
        MULTICAST_VLAN,
        NO_DNS_LOOKUP,
        POE_FAST_DISABLE,
        POE_FAST_ENABLE,
        POE_PERPETUAL_DISABLE,
        POE_PERPETUAL_ENABLE,
        POWER_2PAIR_PORT,
        POWER_4PAIR_PORT,
        POWER_823BT_DISABLE,
        POWER_823BT_ENABLE,
        POWER_CLASS_DETECTION_ENABLE,
        POWER_DOWN_PORT,
        POWER_DOWN_SLOT,
        POWER_HDMI_DISABLE,
        POWER_HDMI_ENABLE,
        POWER_PRIORITY_PORT,
        POWER_UP_PORT,
        POWER_UP_SLOT,
        REBOOT_SWITCH,
        RESET_POWER_PORT,
        RUN_PYTHON_SCRIPT,
        SET_CONTACT,
        SET_DEFAULT_GATEWAY,
        SET_DNS_SERVER,
        SET_IP_INTERFACE,
        SET_LOCATION,
        SET_LOOPBACK_DET,
        SET_MAX_POWER_PORT,
        SET_MGT_INTERFACE,
        SET_NTP_SERVER,
        SET_PASSWORD,
        SET_PORT_ALIAS,
        SET_SYSTEM_NAME,
        SHOW_AAA_AUTH,
        SHOW_ARP,
        SHOW_BLOCKED_PORTS,
        SHOW_CHASSIS,
        SHOW_CHASSIS_LAN_POWER_STATUS,
        SHOW_CMM,
        SHOW_CONFIGURATION,
        SHOW_DDM_INTERFACES,
        SHOW_DHCP_CONFIG,
        SHOW_DHCP_RELAY,
        SHOW_DNS_CONFIG,
        SHOW_FREE_SPACE,
        SHOW_HEALTH,
        SHOW_HEALTH_CONFIG,
        SHOW_HW_INFO,
        SHOW_INTERFACE_PORT,
        SHOW_INTERFACES,
        SHOW_IP_INTERFACE,
        SHOW_IP_ROUTES,
        SHOW_IP_SERVICE,
        SHOW_LAN_POWER,
        SHOW_LAN_POWER_CONFIG,
        SHOW_LAN_POWER_FEATURE,
        SHOW_LINKAGG,
        SHOW_LINKAGG_PORT,
        SHOW_LLDP_INVENTORY,
        SHOW_LLDP_LOCAL,
        SHOW_LLDP_REMOTE,
        SHOW_MAC_LEARNING,
        SHOW_MICROCODE,
        SHOW_MULTICAST_GLOBAL,
        SHOW_MULTICAST_VLAN,
        SHOW_NTP_CONFIG,
        SHOW_NTP_STATUS,
        SHOW_PORT_ALIAS,
        SHOW_PORT_LLDP_REMOTE,
        SHOW_PORT_MAC_ADDRESS,
        SHOW_PORT_POWER,
        SHOW_PORT_STATUS,
        SHOW_PORTS_LIST,
        SHOW_POWER_SUPPLIES,
        SHOW_POWER_SUPPLY,
        SHOW_RUNNING_DIR,
        SHOW_SLOT,
        SHOW_SLOT_LAN_POWER_STATUS,
        SHOW_SNMP_COMMUNITY,
        SHOW_SNMP_SECURITY,
        SHOW_SNMP_STATION,
        SHOW_SYSTEM,
        SHOW_SYSTEM_RUNNING_DIR,
        SHOW_TDR_STATISTICS,
        SHOW_TEMPERATURE,
        SHOW_USER,
        SHOW_VLAN,
        SHOW_VLAN_MEMBERS,
        SNMP_AUTH_LOCAL,
        SNMP_COMMUNITY_MAP,
        SNMP_COMMUNITY_MODE,
        SNMP_NO_SECURITY,
        SNMP_STATION,
        SNMP_TRAP_AUTH,
        SNMP_USER,
        SSH_AUTH_LOCAL,
        SYSTEM_TIMEZONE,
        TELNET_AUTH_LOCAL,
        UPDATE_UBOOT,
        WRITE_MEMORY,
    }

    public class CmdRequest
    {
        public Command Command { get; }
        public ParseType ParseType { get; }
        public DictionaryType DictionaryType { get; }
        public string[] Data { get; }

        public CmdRequest(Command command) : this(command, ParseType.Text, DictionaryType.None, null) { }
        public CmdRequest(Command command, params string[] data) : this(command, ParseType.Text, DictionaryType.None, data) { }
        public CmdRequest(Command command, ParseType type) : this(command, type, DictionaryType.None, null) { }
        public CmdRequest(Command command, ParseType type, params string[] data) : this(command, type, DictionaryType.None, data) { }
        public CmdRequest(Command command, ParseType type, DictionaryType dtype, params string[] data)
        {
            Command = command;
            ParseType = type;
            DictionaryType = dtype;
            Data = data?.ToArray();
        }
    }
}
