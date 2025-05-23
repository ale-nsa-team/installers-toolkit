﻿using System;

namespace PoEWizard.Device
{
    public class SnmpStation : ICloneable
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public string Status { get; set; }
        public string Version { get; set; } = "v2";
        public string User {  get; set; }
        public string Community { get;set; }
        
        public SnmpStation() { }

        public SnmpStation(string ip_port, string status, string version, string user, string community) 
        {
            string[] parts = ip_port.Split('/');
            if (parts.Length == 2)
            {
                IpAddress = parts[0];
                Port = int.Parse(parts[1]);
            }
            Status = status;
            Version = version;
            if (version == "v2")
            {
                User = "--";
                Community = community;
            }
            else
            {
                Community = "--";
                User = user;
            }
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
