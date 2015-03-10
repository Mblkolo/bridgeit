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
            var world = new StateManager();

            server.Start(socket =>
                {
                    var __systemState = new SystemState(socket, world);
                    __systemState.Init();
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

    internal class StateManager
    {
        public readonly GameWorld World = new GameWorld();

        private readonly List<SystemState> _states = new List<SystemState>();
        public void Add(SystemState systemState)
        {
            _states.Add(systemState);
        }

        internal void Remove(SystemState systemState)
        {
            _states.Remove(systemState);
        }
    }

    class OutboxMessage
    {
        public string area;
        public string type;
        public string value;

        public OutboxMessage()
        {
        }

        public OutboxMessage(string area, string type, string value)
        {
            this.area = area;
            this.type = type;
            this.value = value;
        }
    }

    class InboxMessage
    {
        public string session;
        public string type;
        public string value;
    }

}
