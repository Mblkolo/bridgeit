using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fleck;
using Newtonsoft.Json;

namespace BridgeitServer
{
    sealed class GameWorld
    {
        private readonly List<Session> _unidentifiedSession = new List<Session>();
        private readonly Dictionary<Guid, Session> _sessions = new Dictionary<Guid, Session>();

        public void ConnectionCreated(IWebSocketConnection connection)
        {
            var __session = new Session(this);
            __session.Join(connection);
            _unidentifiedSession.Add(__session);
        }

        public void Join(Session session, Guid sessionID)
        {
            if (_sessions.ContainsKey(sessionID))
            {
                session.Connections.ForEach(x => _sessions[sessionID].Join(x));
                session.Connections.Clear();
            }
            else
                _sessions[session.Id] = session;

            _unidentifiedSession.Remove(session);
        }

        public void Login(Session session, string name)
        {
            if (_sessions.Values.Any(x => x.Name == name))
            {
                session.ShowError("Имя занято, выбери другое!");
                return;
            }

            session.Name = name;
            session.GotoListState();
        }

        readonly Dictionary<Guid, RoomSettings> _roomSettings = new Dictionary<Guid, RoomSettings>();

        public void GetRooms(Session session, string value)
        {
            if (_roomSettings.ContainsKey(session.Id))
                return;

            RoomSettings __s = RoomSettings.TryCreate(value);
            if (__s == null)
                return;

            _roomSettings.Add(session.Id, __s);

        }

        public void CreateRoom(Session session, string value)
        {
            throw new NotImplementedException();
        }

        public void RemoveRoom(Session session, string value)
        {
            throw new NotImplementedException();
        }
    }

    class RoomSettings
    {
        public static RoomSettings TryCreate(string value)
        {
            throw new NotImplementedException();
        }
    }

    static class DefaultRouter
    {
        public readonly static string Name = "DefaultRouter";

        public static void OnMessage(Session session, GameWorld world, string message)
        {
            var __inbox = JsonConvert.DeserializeObject<InboxMessage>(message);
            if (__inbox.type != "join")
                return;

            Guid __sessionID;
            if (!Guid.TryParse(__inbox.value, out __sessionID))
                return;

            world.Join(session, __sessionID);
        }
    }

    static class WelcomeRouter
    {
        public readonly static string Name = "WelcomeRouter";

        public static void OnMessage(Session session, GameWorld world, string message)
        {
            var __inbox = JsonConvert.DeserializeObject<InboxMessage>(message);

            Guid __sessionID;
            if (!Guid.TryParse(__inbox.value, out __sessionID))
                return;

            if (__sessionID != session.Id)
                return;

            if (__inbox.type == "login")
                world.Login(session, __inbox.value);
        }
    }

    static class ListRouter
    {
        public readonly static string Name = "ListRouter";

        public static void OnMessage(Session session, GameWorld world, string message)
        {
            var __inbox = JsonConvert.DeserializeObject<InboxMessage>(message);

            Guid __sessionID;
            if (!Guid.TryParse(__inbox.value, out __sessionID))
                return;

            if (__sessionID != session.Id)
                return;

            if (__inbox.type == "getRooms")
                world.GetRooms(session, __inbox.value);

            if (__inbox.type == "CreateRoom")
                world.CreateRoom(session, __inbox.value);

            if (__inbox.type == "RemoveRoom")
                world.RemoveRoom(session, __inbox.value);
        }
    }

    class Session
    {
        private readonly GameWorld _world;
        private string _currentRouter = DefaultRouter.Name;

        public readonly List<IWebSocketConnection> Connections = new List<IWebSocketConnection>();
        public readonly Guid Id = Guid.NewGuid();
        public string Name;

        public Session(GameWorld world)
        {
            _world = world;
        }

        public void Join(IWebSocketConnection connection)
        {
            Connections.Add(connection);
            connection.OnMessage = OnMessage;
            connection.OnClose = () => OnClose(connection);
            connection.OnError = error => OnClose(connection);
        }

        private void OnMessage(string message)
        {
            Routers[_currentRouter](this, _world, message);
        }

        private void OnClose(IWebSocketConnection connection)
        {
            Connections.Remove(connection);
        }

        public static Dictionary<string, Action<Session, GameWorld, string>> Routers = new Dictionary<string, Action<Session, GameWorld, string>>
            {
                {DefaultRouter.Name, DefaultRouter.OnMessage},
                {WelcomeRouter.Name, WelcomeRouter.OnMessage},
                {ListRouter.Name, ListRouter.OnMessage}
            };

        private void Send(OutboxMessage message)
        {
            var __text = JsonConvert.SerializeObject(message);
            Connections.ForEach(x => x.Send(__text));
        }

        public void GotoWelcomeState()
        {
            _currentRouter = WelcomeRouter.Name;
        }

        public void ShowError(string errorMessage)
        {
            var __message = new OutboxMessage("Login", "ShowError", errorMessage);
            Send(__message);
        }

        public void GotoListState()
        {
            _currentRouter = ListRouter.Name;
        }


    }
}
