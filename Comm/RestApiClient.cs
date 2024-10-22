using PoEWizard.Data;
using PoEWizard.Device;
using PoEWizard.Exceptions;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using static PoEWizard.Data.Constants;
using static PoEWizard.Data.RestUrl;
using static PoEWizard.Data.Utils;

namespace PoEWizard.Comm
{
    public class RestApiClient
    {
        #region Internal Constants

        private const int WAIT_SWITCH_CONNECT_DELAY_MS = 100;

        #endregion Internal Constants

        #region Internal Variables

        private HttpClient _httpClient;
        private readonly string _ip_address;
        private readonly string _login;
        private readonly string _password;
        private readonly double _cnx_timeout;
        private bool _connected = false;
        private DateTime _start_connect_time;
        private HttpRequestMessage _http_request;

        #endregion Internal Variables

        #region Properties

        #endregion Properties

        #region Constructors

        public RestApiClient(SwitchModel device)
        {
            this._ip_address = device.IpAddress;
            this._login = device.Login;
            this._password = device.Password;
            this._cnx_timeout = device.CnxTimeout;
            CreateHttpClient();
        }

        private void CreateHttpClient()
        {
            this._httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(this._ip_address))
            {
                this._httpClient.BaseAddress = new Uri($"https://{this._ip_address}");
                this._httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.alcatellucentaos+xml");
                this._httpClient.DefaultRequestHeaders.Add("Alu_context", "vrf=default");
                this._httpClient.Timeout = TimeSpan.FromSeconds(this._cnx_timeout);
            }
            System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
        }
        #endregion Constructors

        #region Open Rest Api Client Method

        public void Login()
        {
            ConnectToSwitch();
        }

        private void ConnectToSwitch()
        {
            if (this._httpClient == null) return;
            this._start_connect_time = DateTime.Now;
            try
            {
                string domain = "authv2";
                string url = $"{this._httpClient.BaseAddress}?domain={domain}&username={this._login}&password={this._password}";
                var response = this._httpClient.GetAsync(url);
                while (!response.IsCompleted)
                {
                    if (IsTimeExpired(this._start_connect_time, this._cnx_timeout))
                    {
                        throw new SwitchConnectionFailure($"Failed to establish a connection to {this._ip_address}!");
                    }
                    Thread.Sleep(WAIT_SWITCH_CONNECT_DELAY_MS);
                };
                if (response.IsCanceled || response.Result == null || response.Result.Content == null)
                {
                    throw new SwitchConnectionFailure(PrintUnreachableError(this._start_connect_time));
                }
                XmlDocument xmlDoc = GetRestApiResponse(response.Result.Content.ReadAsStringAsync().Result);
                XmlNode tokenNode;
                if (response.Result.StatusCode == HttpStatusCode.OK)
                {
                    tokenNode = xmlDoc.SelectSingleNode("/nodes/result/data/token");
                    if (tokenNode != null && !string.IsNullOrEmpty(tokenNode.InnerText))
                    {
                        RemoveToken();
                        var authenticationHeaderValue = new AuthenticationHeaderValue("Bearer", tokenNode.InnerText);
                        this._httpClient.DefaultRequestHeaders.Authorization = authenticationHeaderValue;
                        this._connected = true;
                    }
                    else
                    {
                        this._connected = false;
                        if (!string.IsNullOrEmpty(xmlDoc.InnerXml) && xmlDoc.InnerXml.ToLower().Contains("unsupported"))
                        {
                            throw NotSupportApiException();
                        }
                        throw new SwitchAuthenticationFailure("Invalid response body - token not found!");
                    }
                }
                else
                {
                    this._connected = false;
                    tokenNode = xmlDoc.SelectSingleNode("/nodes/result/error");
                    string error = "Connection failed";
                    if (tokenNode != null)
                    {
                        error = tokenNode.InnerText;
                        if (error.ToLower().Contains("denied")) throw new SwitchRejectConnection(error);
                        if (error.ToLower().Contains("authentication fail")) throw new SwitchAuthenticationFailure(error);
                    }
                    if (tokenNode != null) throw new SwitchConnectionFailure(error);
                }
            }
            catch (HttpRequestException ex)
            {
                string error = PrintUnreachableError(this._start_connect_time);
                if (ex.InnerException != null)
                {
                    error = ex.InnerException.Message;
                }
                throw new SwitchConnectionFailure(error);
            }
            catch (SwitchConnectionFailure ex)
            {
                throw ex;
            }
            catch (SwitchRejectConnection ex)
            {
                throw ex;
            }
            catch (SwitchAuthenticationFailure ex)
            {
                throw ex;
            }
            catch (Exception ex)
            {
                string error = ex.Message;
                string stackTrace = ex.StackTrace;
                if (ex.InnerException != null)
                {
                    error = ex.InnerException.Message;
                    if (ex.InnerException.InnerException != null)
                    {
                        error = ex.InnerException.InnerException.Message;
                        if (error.ToLower().Contains("unable to connect") || error.ToLower().Contains("was closed"))
                        {
                            error = $"Failed to establish a connection to {this._ip_address} within {this._cnx_timeout} sec!";
                        }
                        if (!string.IsNullOrEmpty(ex.InnerException.InnerException.StackTrace)) stackTrace = ex.InnerException.InnerException.StackTrace;
                    }
                }
                Logger.Error($"{error}\n{stackTrace}");
                throw new SwitchConnectionFailure(error);
            }
        }

        private XmlDocument GetRestApiResponse(string xml)
        {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xml);
                return xmlDoc;
            }
            catch
            {
                throw NotSupportApiException();
            }
        }

        private SwitchRejectConnection NotSupportApiException()
        {
            return new SwitchRejectConnection($"Device {this._ip_address} doesn't support ALE Rest API or is not an ALE switch!");
        }

        private void RemoveToken()
        {
            if (this._httpClient.DefaultRequestHeaders.Authorization != null)
            {
                this._httpClient.DefaultRequestHeaders.Remove("Authorization");
            }
        }

        #endregion Open Rest Api Client Method

        #region Send Api Resquest

        public Dictionary<string, string> SendRequest(RestUrlEntry entry)
        {
            string url = ParseUrl(entry);
            if (string.IsNullOrEmpty(url)) throw new SwitchCommandError("Command line is missing!");
            if (this._httpClient == null) return new Dictionary<string, string> { [REST_URL] = url, [ERROR] = $"Switch {this._ip_address} is disconnected", [DURATION] = string.Empty };
            entry.StartTime = DateTime.Now;
            Dictionary<string, string> response = SendRestApiRequest(entry, url);
            response[DURATION] = CalcStringDuration(entry.StartTime);
            entry.Duration = response[DURATION];
            entry.Response = response;
            return response;
        }

        private Dictionary<string, string> SendRestApiRequest(RestUrlEntry entry, string url)
        {
            Dictionary<string, string> response = new Dictionary<string, string> { [REST_URL] = url, [ERROR] = string.Empty, [DURATION] = string.Empty };
            try
            {
                url = $"{this._httpClient.BaseAddress}{url}";
                response[REST_URL] = url;
                Task<HttpResponseMessage> http_response = SendHttpRequest(entry, url);
                if (http_response == null)
                {
                    response[ERROR] = $"Switch {this._ip_address} didn't respond within {this._cnx_timeout} sec";
                    return response;
                }
                string error;
                int nbRetry = 1;
                while (http_response.IsCanceled || http_response?.Result == null)
                {
                    error = GetHttpResponseError(url, http_response);
                    Logger.Warn($"{error ?? "Unknown error"} (Try #{nbRetry}, duration: {CalcStringDuration(entry.StartTime)})");
                    nbRetry++;
                    if (nbRetry >= 3) break;
                    ReconnectSwitch();
                    Thread.Sleep(3000);
                    http_response = SendHttpRequest(entry, url);
                }
                error = GetHttpResponseError(url, http_response);
                if (!string.IsNullOrEmpty(error))
                {
                    response[ERROR] = error;
                    return response;
                }
                if (http_response.Result.StatusCode == HttpStatusCode.OK)
                {
                    response[RESULT] = http_response.Result.Content.ReadAsStringAsync().Result;
                }
                else
                {
                    string errorDescr = ParseError(http_response, url);
                    if (!string.IsNullOrEmpty(errorDescr)) response[ERROR] = errorDescr;
                }
            }
            catch (Exception ex)
            {
                if (ex.InnerException is HttpRequestException)
                {
                    throw new SwitchConnectionFailure($"Switch {this._ip_address} connection failure!");
                }
                else
                {
                    Logger.Error(ex);
                    throw ex;
                }
            }
            return response;
        }

        private Task<HttpResponseMessage> SendHttpRequest(RestUrlEntry entry, string url)
        {
            Task<HttpResponseMessage> http_response = SendAsyncRequest(entry, url);
            if (http_response.IsCanceled) return http_response;
            if (http_response?.Result?.StatusCode == HttpStatusCode.Unauthorized)
            {
                Logger.Info($"Switch {this._ip_address} token expired after {CalcStringDuration(this._start_connect_time, true)}, reconnecting to the switch.");
                ReconnectSwitch();
                http_response = SendAsyncRequest(entry, url);
            }
            return http_response;
        }

        private Task<HttpResponseMessage> SendAsyncRequest(RestUrlEntry entry, string url)
        {
            this._http_request = GetHttpRequest(entry, url);
            Task<HttpResponseMessage> http_response = this._httpClient.SendAsync(this._http_request);
            while (http_response != null && !http_response.IsCompleted) { };
            if (http_response != null) return http_response;
            return null;
        }

        private void ReconnectSwitch()
        {
            CloseHttpClient();
            CreateHttpClient();
            ConnectToSwitch();
        }

        private string GetHttpResponseError(string url, Task<HttpResponseMessage> http_response)
        {
            if (http_response.IsCanceled) return $"Requested URL: {url}\r\nError: Switch {this._ip_address} didn't respond within {this._cnx_timeout} sec";
            else if (http_response.Result == null) return $"Requested URL: {url}\r\nError: Switch {this._ip_address} rejected the request";
            return null;
        }

        private string ParseError(Task<HttpResponseMessage> http_response, string url)
        {
            try
            {
                string xmlError = http_response.Result.Content.ReadAsStringAsync().Result;
                var errorList = CliParseUtils.ParseXmlToDictionary(xmlError);
                if (errorList.ContainsKey(API_ERROR) && !string.IsNullOrEmpty(errorList[API_ERROR]))
                {
                    string error = errorList[API_ERROR].Trim();
                    if (!string.IsNullOrEmpty(error))
                    {
                        error = ReplaceFirst(error, ":", "");
                        string errMsg = error.ToLower();
                        if (errorList.ContainsKey(HTTP_RESPONSE) && !string.IsNullOrEmpty(errorList[HTTP_RESPONSE]))
                        {
                            HttpStatusCode code = ConvertToHttpStatusCode(errorList);
                            string errorMsg = $"Requested URL: {url}\r\nHTTP Response: {code} ({errorList[HTTP_RESPONSE]})\r\nError: {error}";
                            if (errMsg.Contains("lanpower") && (errMsg.Contains("not supported") || errMsg.Contains("invalid entry"))) return error;
                            else if (errMsg.Contains("not supported") || errMsg.Contains("command in progress") || errMsg.Contains("power range supported"))
                            {
                                string[] split = url.Split('=');
                                if (split.Length >= 2) error += $" ({WebUtility.UrlDecode(split[1])})";
                                Logger.Warn(errorMsg);
                            }
                            else
                            {
                                Logger.Error(errorMsg);
                            }
                        }
                    }
                    return error;
                }
            }
            catch { }
            return null;
        }

        private HttpRequestMessage GetHttpRequest(RestUrlEntry entry, string url)
        {
            HttpRequestMessage request = new HttpRequestMessage(entry.Method, url);
            if (entry.Method == HttpMethod.Post && entry.Content != null)
            {
                request.Content = new FormUrlEncodedContent(entry.Content);
            }
            return request;
        }

        private string PrintUnreachableError(DateTime startTime)
        {
            return $"Couldn't connect to the device within {CalcStringDuration(startTime, true)}.";
        }

        #endregion Send Api Resquest

        #region Other Public Methods

        public void Close()
        {
            Logger.Activity($"Closing Rest API client on {this._ip_address}");
            CloseHttpClient();
            this._connected = false;
        }

        private void CloseHttpClient()
        {
            this._http_request?.Dispose();
            this._http_request = null;
            this._httpClient?.Dispose();
            this._httpClient = null;
        }

        public bool IsConnected()
        {
            return this._httpClient != null && this._connected;
        }

        public override string ToString()
        {
            StringBuilder txt = new StringBuilder("RestApiClient for Switch IP Address: ");
            txt.Append(this._ip_address).Append(", BaseUrl: ").Append(this._httpClient.BaseAddress);
            return txt.ToString();
        }

        #endregion Other Public Methods
    }
}
