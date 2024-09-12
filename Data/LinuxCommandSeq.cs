﻿using System;
using System.Collections.Generic;
using System.Linq;
using static PoEWizard.Data.Constants;
using static PoEWizard.Data.RestUrl;

namespace PoEWizard.Data
{
    public class LinuxCommand
    {
        public string Command { get; set; }
        public int DelaySec { get; set; }
        public Dictionary<string, string> Response { get; set; }

        public LinuxCommand(string cmd) : this(cmd, 0) { }
        public LinuxCommand(string cmd, int delaySec)
        {
            this.Command = cmd;
            this.DelaySec = delaySec;
            this.Response = new Dictionary<string, string> { [REST_URL] = "Run linux command", [ERROR] = string.Empty, [DURATION] = string.Empty };
        }
    }

    public class LinuxCommandSeq
    {
        public List<LinuxCommand> CommandSeq { get; set; }
        public DateTime StartTime { get; set; }
        public string Duration { get; set; }
        public LinuxCommandSeq(List<LinuxCommand> cmdSeq) 
        {
            this.StartTime = DateTime.Now;
            this.CommandSeq = cmdSeq;
        }

        public LinuxCommandSeq(LinuxCommand linuxCmd)
        {
            this.CommandSeq = new List<LinuxCommand> { linuxCmd };
        }

        public void AddLinuxCommand(LinuxCommand linuxCmd)
        {
            if (this.CommandSeq == null) this.CommandSeq = new List<LinuxCommand>();
            this.CommandSeq.Add(linuxCmd);
        }

        public Dictionary<string, string> GetResponse(string cmd)
        {
            LinuxCommand linuxCmd = this.CommandSeq?.FirstOrDefault(d => d.Command == cmd);
            return linuxCmd?.Response;
        }
    }
}