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
                            var __mail = JsonConvert.DeserializeObject<OutboxMessage>(message);
                            switch(__mail.type)
                            {
                                case "login":
                                    {
                                        Guid __userId = Guid.NewGuid();
                                        usersById[__userId] = new List<IWebSocketConnection>();
                                        usersById[__userId].Add(socket);
                                        var __resive = new OutboxMessage { type = "login", value = __userId.ToString() };
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

    class OutboxMessage
    {
        public string area;
        public string type;
        public string value;
    }

    class InboxMessage
    {
        public string session;
        public string type;
    }

    class Router
    {
        public IWebSocketConnection Connection;
        public World World;
        public Player Player;

        public void Init(IWebSocketConnection connection, World world)
        {
            Connection = connection;
            World = world;
        }

        public void OnMessage(string message)
        {
            var __inbox = JsonConvert.DeserializeObject<InboxMessage>(message);
            
            if(__inbox.type == "join")
            {
                if(!World.Players.TryGetValue(__inbox.session, out Player))
                {
                    Player = new Player(Guid.NewGuid().ToString());
                    World.Players.Add(Player.Id, Player);
                    Connection.Send(JsonConvert.SerializeObject(new OutboxMessage { area = "system", type = "session", value = Player.Id }));
                }

                Connection.Send(Player.GetState());
            }
        }
    }

    class World
    {
        public Dictionary<string, Player> Players = new Dictionary<string, Player>();


    }

    class Player
    {
        public readonly string Id;
        
        public Player(string id)
        {
            Id = id;
        }

        public string GetState()
        {
            var __message = new OutboxMessage { area = "welcome", type = "login" };
            return JsonConvert.SerializeObject(__message);
        }
    }
}
