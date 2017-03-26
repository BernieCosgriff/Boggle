using CustomNetworking;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.ServiceModel.Web;
using System.Text;
using System.Text.RegularExpressions;

namespace Boggle
{
    public class WebServer
    {
        public static void Main()
        {
            new WebServer();
            Console.Read();
        }

        private TcpListener server;

        public WebServer()
        {
            server = new TcpListener(IPAddress.Any, 60000);
            server.Start();
            server.BeginAcceptSocket(ConnectionRequested, null);
        }
        
        private void ConnectionRequested(IAsyncResult ar)
        {
            Socket s = server.EndAcceptSocket(ar);
            server.BeginAcceptSocket(ConnectionRequested, null);
            new HttpRequestHandler(new StringSocket(s, new UTF8Encoding())); 
        }

    }

    class HttpRequestHandler
    {
        // Routes matchers.
        private static readonly Regex ROOT_PATTERN = new Regex(@"^/$");
        private static readonly Regex USERS_PATTERN = new Regex(@"^/BoggleService.svc/users$");
        private static readonly Regex GAMES_PATTERN = new Regex(@"^/BoggleService.svc/games$");
        private static readonly Regex GAMES_WITH_ID_PATTERN = new Regex(@"^/BoggleService.svc/games/([a-zA-Z0-9]+)$");
        private static readonly Regex GAMES_WITH_ID_AND_OPTIONAL_BRIEF_PATTERN = new Regex(@"^/BoggleService.svc/games/([a-zA-Z0-9]+)(?:\?(?i)brief(?-i)=(\S+))?$");

        // Socket through which to respond.
        private StringSocket ss;

        // Request state. 
        private int lineCount;
        private int contentLength;

        private HttpMethod reqMethod; 
        private string reqUrl;

        public HttpRequestHandler(StringSocket stringSocket)
        {
            this.ss = stringSocket;
            ss.BeginReceive(LineReceived, null);
        }

        private void LineReceived(string s, Exception e, object payload)
        {
            lineCount ++;
            Console.WriteLine(s);
            if (s != null)
            {
                if (lineCount == 1)
                {
                    Regex r = new Regex(@"^(\S+)\s+(\S+)");
                    Match m = r.Match(s);

                    reqMethod = new HttpMethod(m.Groups[1].Value);
                    reqUrl = m.Groups[2].Value;

                    if (reqMethod == HttpMethod.Get)
                    {
                        RouteRequest(reqMethod, reqUrl);
                        return; 
                    }
                    else if (!USERS_PATTERN.IsMatch(reqUrl) &&
                        !GAMES_PATTERN.IsMatch(reqUrl) &&
                        !GAMES_WITH_ID_PATTERN.IsMatch(reqUrl))
                    {
                        SendResponseAndCloseSocket(HttpResponseType.HTML,
                            HttpStatusCode.BadRequest, null);
                        return;
                    }
                }
                else if (s.ToLower().StartsWith("content-length:"))
                {
                    contentLength = Int32.Parse(s.Substring(16).Trim());
                }
                else if (s == "\r")
                {
                    ss.BeginReceive(ContentReceived, null, contentLength);
                    return;
                }

                Debug.WriteLine($"Begin receive has been called {lineCount} times. Line received is {s}");
                ss.BeginReceive(LineReceived, null); 
            }
        }

        private void ContentReceived(string s, Exception e, object payload)
        {
            RouteRequest(reqMethod, reqUrl, s);
        }

        private void RouteRequest(HttpMethod method, string url)
        {
            if (method == HttpMethod.Get)
            {
                if (ROOT_PATTERN.IsMatch(reqUrl))
                {
                    var response = new BoggleService().API();
                    SendResponseAndCloseSocket(HttpResponseType.HTML,
                        response.Item1, response.Item2);
                    return;
                }
                if (GAMES_WITH_ID_AND_OPTIONAL_BRIEF_PATTERN.IsMatch(reqUrl))
                {
                    var match = GAMES_WITH_ID_AND_OPTIONAL_BRIEF_PATTERN.Match(reqUrl);
                    var gameId = match.Groups[1].Value;
                    var brief = match.Groups[2].Value;
                    var response = new BoggleService().GameStatus(gameId, brief);
                    SendResponseAndCloseSocket(HttpResponseType.JSON, response.Item1,
                        response.Item2 == null ? null : JsonConvert.SerializeObject(response.Item2));
                    return;
                }
            }

            // If this code executes, the request is bad.
            SendResponseAndCloseSocket(HttpResponseType.JSON, HttpStatusCode.BadRequest, null);
        }

        /// <summary>
        /// Generates an appropriate response to the request based on the given httpMethod and string url. 
        /// The url should be relative, not absolute. Content is the body of the request. 
        /// </summary>
        private void RouteRequest(HttpMethod method, string url, string content)
        {
            if (method == HttpMethod.Post)
            {
                if (USERS_PATTERN.IsMatch(url))
                {
                    UserInfo userInfo;
                    if (Extensions.TryJsonDeserialize(content, out userInfo))
                    {
                        var response = new BoggleService().CreateUser(userInfo);
                        SendResponseAndCloseSocket(HttpResponseType.JSON, response.Item1,
                            response.Item2 == null ? null : JsonConvert.SerializeObject(response.Item2));
                        return;
                    }
                }
                else if (GAMES_PATTERN.IsMatch(url))
                {
                    JoinGameInfo info;
                    if (Extensions.TryJsonDeserialize(content, out info))
                    {
                        var response = new BoggleService().JoinGame(info);
                        SendResponseAndCloseSocket(HttpResponseType.JSON, response.Item1,
                            response.Item2 == null ? null : JsonConvert.SerializeObject(response.Item2));
                        return;
                    }
                }
            }
            else if (method == HttpMethod.Put)
            {
                if (GAMES_WITH_ID_PATTERN.IsMatch(url))
                {
                    var gameId = GAMES_WITH_ID_PATTERN.Match(url).Groups[1].Value;
                    WordToPlayInfo wordToPlayInfo;
                    if (Extensions.TryJsonDeserialize(content, out wordToPlayInfo))
                    {
                        var response = new BoggleService().PlayWord(gameId, wordToPlayInfo);
                        SendResponseAndCloseSocket(HttpResponseType.JSON, response.Item1,
                            response.Item2 == null ? null : JsonConvert.SerializeObject(response.Item2));
                        return;
                    }
                }
                else if (GAMES_PATTERN.IsMatch(url))
                {
                    CancelInfo cancelInfo;
                    if (Extensions.TryJsonDeserialize(content, out cancelInfo))
                    {
                        var responseStatus = new BoggleService().CancelJoinRequest(cancelInfo);
                        SendResponseAndCloseSocket(HttpResponseType.JSON, responseStatus, null);
                        return;
                    }
                }
            }

            // If this code executes, the request is bad.
            SendResponseAndCloseSocket(HttpResponseType.HTML, HttpStatusCode.BadRequest, null);
        }

        /// <summary>
        /// Sends the given response information over the string socket and terminates the socket connection.
        /// Parameters 'type' and 'status' should not be null, 'content' may be null, if there is no content to send.
        /// </summary>
        private void SendResponseAndCloseSocket(HttpResponseType type, HttpStatusCode status, string content)
        {
            if (content == null)
            {
                ss.BeginSend($"HTTP/1.1 {((int)status)} {status}\r\nConnection: close\r\n\r\n", (ex, py) => { ss.Shutdown(); }, null);
            }
            else
            {
                ss.BeginSend($"HTTP/1.1 {((int)status)} {status}\r\n", Ignore, null);
                ss.BeginSend($"Content-Type: {type.GetContentTypeString()}\r\n", Ignore, null);
                ss.BeginSend($"Content-Length: {content.Length}\r\n", Ignore, null);
                ss.BeginSend("\r\n", Ignore, null);
                ss.BeginSend(content, (ex, py) => { ss.Shutdown(); }, null);
            }
        }

        private void Ignore(Exception e, object payload)
        {
        }
    }

    internal enum HttpResponseType
    {
        JSON, HTML
    }

    internal static class Extensions
    {

        internal static bool TryJsonDeserialize<T>(string value, out T result) where T : class
        {
            try
            {
                result = JsonConvert.DeserializeObject<T>(value);
                return true;
            }
            catch (JsonReaderException)
            {
                result = null;
                return false;
            }
        }

        internal static string GetContentTypeString(this HttpResponseType type)
        {
            switch (type)
            {
                case HttpResponseType.HTML:
                    return "text/html";
                case HttpResponseType.JSON:
                    return "application/json";
                default:
                    return "";
            }
        }
    }
}