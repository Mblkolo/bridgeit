using Fleck;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BridgeitServer
{
    class Program
    {
        static void Main(string[] args)
        {
            FleckLog.Level = LogLevel.Debug;
            var allSockets = new List<IWebSocketConnection>();
            var server = new WebSocketServer("ws://0.0.0.0:8181");
            var usersById = new Dictionary<Guid, List<IWebSocketConnection>>();

            server.Start(socket =>
                {
                    //Guid __userID;
                    //String __userName;

                    socket.OnOpen = () =>
                        {
                            Console.WriteLine("Open!");
                            allSockets.Add(socket);
                        };
                    socket.OnClose = () =>
                        {
                            Console.WriteLine("Close!");
                            allSockets.Remove(socket);
                        };
                    socket.OnMessage = message =>
                        {
                            Console.WriteLine(message);
                            var __mail = JsonConvert.DeserializeObject<Message>(message);
                            switch(__mail.type)
                            {
                                case "login":
                                    {
                                        Guid __userId = Guid.NewGuid();
                                        usersById[__userId] = new List<IWebSocketConnection>();
                                        usersById[__userId].Add(socket);
                                        var __resive = new Message { type = "login", value = __userId.ToString() };
                                        socket.Send(JsonConvert.SerializeObject(__resive));
                                    }
                                    break;

                                case "welcome":
                                    {
                                        Guid __userId = Guid.Parse(__mail.value);
                                        if (!usersById.ContainsKey(__userId))
                                            socket.Send("fail");
                                        else
                                            socket.Send("ok");
                                    }
                                    break;

                                case "exit":
                                    {
                                        Guid __userId = Guid.Parse(__mail.value);
                                        if (!usersById.ContainsKey(__userId))
                                            socket.Send("fail");
                                        else
                                        {
                                            usersById[__userId].ForEach(x => x.Send("exit"));
                                            usersById.Remove(__userId);
                                        }
                                    }
                                    break;
                            }


                            //allSockets.ToList().ForEach(s => s.Send("Echo: " + message));
                        };
                });


            var input = Console.ReadLine();
            while (input != "exit")
            {
                foreach (var socket in allSockets.ToList())
                {
                    socket.Send(input);
                }
                input = Console.ReadLine();
            }
        }
    }

    class Message
    {
        public string type;
        public string value;
    }
}
