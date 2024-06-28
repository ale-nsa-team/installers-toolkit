﻿using PoEWizard.Data;
using PoEWizard.Device;
using PoEWizard.Exceptions;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using static PoEWizard.Data.Constants;
using static PoEWizard.Data.RestUrl;

namespace PoEWizard.Comm
{
    public class RestApiService
    {
        private Dictionary<string, string> _response = new Dictionary<string, string>();
        private readonly IProgress<ProgressReport> _progress;
        public bool IsReady { get; set; } = false;
        public int Timeout { get; set; }
        public ResultCallback Callback { get; set; }
        public SwitchModel SwitchModel { get; set; }
        public RestApiClient RestApiClient { get; set; }

        public RestApiService(SwitchModel device, IProgress<ProgressReport> progress)
        {
            this.SwitchModel = device;
            this._progress = progress;
            this.RestApiClient = new RestApiClient(SwitchModel);
            this.IsReady = false;
            _progress = progress;
        }

        public void Connect()
        {
            try
            {
                List<Dictionary<string, string>> diclist;
                Dictionary<string, string> dict;
                this.IsReady = true;
                Logger.Debug($"Connecting Rest API");
                _progress.Report(new ProgressReport("Connecting to switch..."));
                RestApiClient.Login();
                _progress.Report(new ProgressReport("Reading System information..."));
                this._response = SendRequest(GetRestUrlEntry(RestUrlId.SHOW_SYSTEM));
                dict = CliParseUtils.ParseVTable(_response["RESULT"]);
                SwitchModel.LoadFromDictionary(dict, DictionaryType.System);
                this._response = SendRequest(GetRestUrlEntry(RestUrlId.SHOW_MICROCODE));
                diclist = CliParseUtils.ParseHTable(_response["RESULT"]);
                SwitchModel.LoadFromDictionary(diclist[0], DictionaryType.MicroCode);
                this._response = SendRequest(GetRestUrlEntry(RestUrlId.SHOW_RUNNING_DIR));
                dict = CliParseUtils.ParseVTable(_response["RESULT"]);
                SwitchModel.LoadFromDictionary(dict, DictionaryType.RunningDir);
                _progress.Report(new ProgressReport("Reading chassis and port information..."));
                this._response = SendRequest(GetRestUrlEntry(RestUrlId.SHOW_CHASSIS));
                dict = CliParseUtils.ParseVTable(_response["RESULT"]);
                SwitchModel.LoadFromDictionary(dict, DictionaryType.Chassis);
                this._response = SendRequest(GetRestUrlEntry(RestUrlId.SHOW_LAN_POWER_STATUS, new string[1] { "1/1" }));
                diclist = CliParseUtils.ParseHTable(_response["RESULT"], 2);
                SwitchModel.LoadFromList(diclist, DictionaryType.LanPower);
                this._response = SendRequest(GetRestUrlEntry(RestUrlId.SHOW_PORTS_LIST));
                this._response = SendRequest(GetRestUrlEntry(RestUrlId.SHOW_LAN_POWER, new string[1] { "1/1" }));

                this._response = SendRequest(GetRestUrlEntry(RestUrlId.SHOW_HEALTH));
                this._response = SendRequest(GetRestUrlEntry(RestUrlId.SHOW_TEMPERATURE));

                //SetPoePriority("1/1/26", PriorityLevelType.High);
                //SetPoePriority("1/1/26", PriorityLevelType.Critical);
                //SetPoePriority("1/1/26", PriorityLevelType.Low);

                //this._response = PowerPort(RestUrlId.POWER_DOWN_PORT, "1/1/26");
                //this._response = PowerPort(RestUrlId.POWER_UP_PORT, "1/1/26");
                //this._response = PowerPort(RestUrlId.POWER_4PAIR_PORT, "1/1/28");
                //this._response = PowerPort(RestUrlId.POWER_2PAIR_PORT, "1/1/28");

                //this._response = SetPoeConfiguration(RestUrlId.POWER_823BT_ENABLE, "1/1");
                //this._response = SetPoeConfiguration(RestUrlId.POWER_823BT_DISABLE, "1/1");

                //this._response = SetPoeConfiguration(RestUrlId.POE_FAST_ENABLE, "1/1");
                //this._response = SetPoeConfiguration(RestUrlId.POE_PERPETUAL_ENABLE, "1/1");

            }
            catch (Exception ex)
            {
                if (ex is SwitchConnectionFailure || ex is SwitchConnectionDropped || ex is SwitchLoginFailure || ex is SwitchAuthenticationFailure)
                {
                    if (ex is SwitchLoginFailure || ex is SwitchAuthenticationFailure) this.SwitchModel.Status = SwitchStatus.LoginFail;
                    else this.SwitchModel.Status = SwitchStatus.Unreachable;
                }
                else
                {
                    _progress?.Report(new ProgressReport(ReportType.Error, "Connect", ex.Message));
                }
            }
        }

        private Dictionary<string, string> SetPoeConfiguration(RestUrlId cmd, string slot)
        {
            try
            {
                return SendRequest(GetRestUrlEntry(cmd, new string[1] { slot }));
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message + ":\n" + ex.StackTrace);
                if (ex is SwitchConnectionFailure || ex is SwitchConnectionDropped || ex is SwitchLoginFailure || ex is SwitchAuthenticationFailure)
                {
                    if (ex is SwitchLoginFailure || ex is SwitchAuthenticationFailure) this.SwitchModel.Status = SwitchStatus.LoginFail;
                    else this.SwitchModel.Status = SwitchStatus.Unreachable;
                    return null;
                }
                else
                {
                    throw ex;
                }
            }
        }

        private Dictionary<string, string> PowerPort(RestUrlId cmd, string port)
        {
            try
            {
                return SendRequest(GetRestUrlEntry(cmd, new string[1] { port }));
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message + ":\n" + ex.StackTrace);
                if (ex is SwitchConnectionFailure || ex is SwitchConnectionDropped || ex is SwitchLoginFailure || ex is SwitchAuthenticationFailure)
                {
                    if (ex is SwitchLoginFailure || ex is SwitchAuthenticationFailure) this.SwitchModel.Status = SwitchStatus.LoginFail;
                    else this.SwitchModel.Status = SwitchStatus.Unreachable;
                    return null;
                }
                else
                {
                    throw ex;
                }
            }
        }

        private Dictionary<string, string> SetPoePriority(string port, PriorityLevelType priority)
        {
            try
            {
                return SendRequest(GetRestUrlEntry(RestUrlId.POWER_PRIORITY_PORT, new string[2] { port, priority.ToString() }));
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message + ":\n" + ex.StackTrace);
                if (ex is SwitchConnectionFailure || ex is SwitchConnectionDropped || ex is SwitchLoginFailure || ex is SwitchAuthenticationFailure)
                {
                    if (ex is SwitchLoginFailure || ex is SwitchAuthenticationFailure) this.SwitchModel.Status = SwitchStatus.LoginFail;
                    else this.SwitchModel.Status = SwitchStatus.Unreachable;
                    return null;
                }
                else
                {
                    throw ex;
                }
            }
        }

        public void Close()
        {
            Logger.Debug($"Closing Rest API");
        }

        private RestUrlEntry GetRestUrlEntry(RestUrlId url)
        {
            return GetRestUrlEntry(url, new string[1] { null });
        }

        private RestUrlEntry GetRestUrlEntry(RestUrlId url, string[] data)
        {
            RestUrlEntry entry = new RestUrlEntry(url, data) { Method = HttpMethod.Get };
            return entry;
        }

        private Dictionary<string, string> SendRequest(RestUrlEntry entry)
        {
            Dictionary<string, string> response = this.RestApiClient.SendRequest(entry);
            if (response == null) return null;
            if (response.ContainsKey(ERROR) && !string.IsNullOrEmpty(response[ERROR]))
            {
                throw new SwitchCommandError(response[ERROR]);
            }
            Dictionary<string, string> result = CliParseUtils.ParseXmlToDictionary(response[RESULT], "//nodes//result//*");
            if (result != null && result.ContainsKey(OUTPUT) && !string.IsNullOrEmpty(result[OUTPUT]))
            {
                response[RESULT] = result[OUTPUT];
            }
            LogSendRequest(entry, response);
            return response;
        }

        private void LogSendRequest(RestUrlEntry entry, Dictionary<string, string> response)
        {
            StringBuilder txt = new StringBuilder("API Request sent").Append(Utils.PrintMethodClass(3)).Append(" with ").Append(entry.ToString());
            Logger.Info(txt.ToString());
            txt = new StringBuilder("Request API URL: ").Append(response[REST_URL]);
            if (entry.Response.ContainsKey(RESULT))
            {
                txt.Append("\nSwitch Response:\n").Append(new string('=', 132)).Append("\n").Append(response[RESULT]).Append("\n").Append(new string('=', 132));
            }
            Logger.Info(txt.ToString());
        }
    }

}
