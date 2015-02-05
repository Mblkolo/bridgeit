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
            var world = new World();

            server.Start(socket =>
                {
                    var __router = new Router();
                    __router.Init(socket, world);
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
        public string value;
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
            Connection.OnMessage = OnMessage;
        }

        public void OnMessage(string message)
        {
            var __inbox = JsonConvert.DeserializeObject<InboxMessage>(message);

            if (__inbox.type == "join")
            {
                if (__inbox.session == null || !World.Players.TryGetValue(__inbox.session, out Player))
                {
                    Player = new Player(Guid.NewGuid().ToString());
                    World.Players.Add(Player.Id, Player);
                    Connection.Send(JsonConvert.SerializeObject(new OutboxMessage { area = "system", type = "setSession", value = Player.Id }));
                    Player.Area = "welcome";
                }

                Connection.Send(JsonConvert.SerializeObject(new OutboxMessage { area = "system", type = "changeArea", value = Player.Area }));
                return;
            }

            if (Player == null || Player.Id != __inbox.session)
                return;

            if (__inbox.type == "login")
            {
                var __name = __inbox.value;
                if (!string.IsNullOrWhiteSpace(__name))
                {
                    Player.Name = __name;
                    Player.Area = "rooms";
                    Connection.Send(JsonConvert.SerializeObject(new OutboxMessage { area = "system", type = "changeArea", value = Player.Area }));
                }
                else
                    Connection.Send(JsonConvert.SerializeObject(new OutboxMessage { area = "welcome", type = "showError", value = "Плохое имя, попробуй другое" }));
            }
        }
    }

    internal class World
    {
        public Dictionary<string, Player> Players = new Dictionary<string, Player>();

        public List<Player> RoomListeners = new List<Player>();
        public List<Room> Rooms = new List<Room>();

        //Игрок зашёл в комнаты
        public void JoinRooms(Player player)
        {
            RoomListeners.Add(player);
            //TODO отослать пользователю текущее состояние
        }

        //Игрок покидает комнтау
        public void LeaveRooms(Player player)
        {
            RemoveRoom(player);
            RoomListeners.Remove(player);
        }

        public void RemoveRoom(Player player)
        {
            //TODO игрок хочет удалить свою комнату
        }

        public void CreateRoom(Player player)
        {
            //TODO игрок создаёт свою комнату
        }



        public void NotifyAllPlayer()
        {
            //TODO тут типа делаем рассылку всем пользователям
        }


    }

    class Room
    {



    }

    class Player
    {
        public readonly string Id;
        public string Name;

        public Player(string id)
        {
            Id = id;
            Area = "default";
        }

        public string Area { get; set; }
    }
}
