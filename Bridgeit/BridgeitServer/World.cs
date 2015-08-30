using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fleck;
using Newtonsoft.Json;

namespace BridgeitServer
{
    class Player
    {
        public readonly string Name;
        public readonly int Id;
        public Player(string name, int id)
        {
            Name = name;
            Id = id;
        }
    }

    class RoomSettings
    {
        public int Id;
        public string Name;
        public int Size;
        public bool IsDisabled;
    }

    internal class GameServer : IConnectionHandler
    {
        public readonly SingleThreadWorker<Action> SingleThread;

        public readonly GameRepository Rep = new GameRepository();

        public int lastPlayerId;
        public int GetNextPlayerId()
        {
            return ++lastPlayerId;
        }

        public GameServer()
        {
            SingleThread = new SingleThreadWorker<Action>(new SimpleSingleThread());
            SingleThread.Start();
        }
        
        public void ConfigureConnection(IWebSocketConnection connection)
        {
            var __proxy = new ConnectionProxy(connection, SingleThread);
            __proxy.Handler = this;
            Rep.AnonimConnections.Add(__proxy.Id, __proxy);
        }


        public void OnError(Guid id, Exception e)
        {
            //TODO фигачить в лог
            //throw new NotImplementedException();
        }

        public void OnClose(Guid id)
        {
            //На анонимов строго пофиг
            if (Rep.AnonimConnections.Remove(id))
                return;

            //А вот теперь нужно найти сессию и сообщить, что пользователь отключен
            //А сессия должна быть обязательно
            if (Rep.SessionConnections.ContainsKey(id))
            {
                var connect = Rep.SessionConnections[id];
                Rep.LostSessions.Add(connect.Id, connect.Session);
                Rep.SessionConnections.Remove(id);
                return;
            }
        }

        public void OnOpen(Guid id)
        {
        }

        public void OnMessage(Guid connectionId, string message)
        {
            var __inbox = JsonConvert.DeserializeObject<InboxMessage>(message);
            if (__inbox.area == "system")
                SystemArea(connectionId, message, __inbox);

            if (__inbox.area == "welcome")
                WelcomeArea(connectionId, message, __inbox);

            if (__inbox.area == "rooms")
                RoomsArea(connectionId, message, __inbox);

        }

        public void SystemArea(Guid connectionId, string message, InboxMessage inbox)
        {
            if (inbox.area != "system")
                return;


            if (inbox.type == "join")
            {
                if (!Rep.AnonimConnections.ContainsKey(connectionId))
                    return;

                GameSession session;
                Guid sessionId;
                if (Guid.TryParse(inbox.value, out sessionId) && Rep.LostSessions.ContainsKey(sessionId))
                {
                    //Восстанавливаем сессию
                    session = Rep.LostSessions[sessionId];
                    Rep.LostSessions.Remove(sessionId);
                }
                else
                {
                    //Создаём новую сессию
                    session = new GameSession { Area = "welcome" };
                }
                var connection = Rep.AnonimConnections[connectionId];
                Rep.AnonimConnections.Remove(connectionId);
                connection.Session = session;
                Rep.SessionConnections.Add(connectionId, connection);

                connection.Send(JsonConvert.SerializeObject(new OutboxMessage { area = "system", type = "setSessionId", value = connection.Id.ToString() }));
                connection.Send(JsonConvert.SerializeObject(new OutboxMessage { area = "system", type = "changeArea", value = session.Area }));
            }

            if (inbox.type == "logout")
            {
                if (!Rep.SessionConnections.ContainsKey(connectionId))
                    return;

                var connection = Rep.SessionConnections[connectionId];
                Rep.SessionConnections.Remove(connectionId);
                Rep.LostSessions.Add(connection.Id, connection.Session);
                connection.Session = null;

                //TODO отключить таки игрока

                connection.Send(new OutboxMessage("system", "logout", null));
            }

        }

        public void WelcomeArea(Guid connectionId, string message, InboxMessage inbox)
        {
            if (inbox.area != "welcome")
                return;

            if (!Rep.SessionConnections.ContainsKey(connectionId))
                return;

            if(inbox.type == "login")
            {
                var __userName = inbox.value;
                //1. Проверить пользователей с таким имененем
                if (Rep.SessionConnections.Values.Any(x => x.Session.PlayerName == __userName) ||
                    Rep.LostSessions.Values.Any(x => x.PlayerName == __userName))
                {
                    //TODO сообщить об ошибке что такое имя уже занято
                    return;
                }

                var connection = Rep.SessionConnections[connectionId];
                connection.Session.PlayerName = __userName;
                connection.Session.PlayerId = GetNextPlayerId();
                connection.Session.Area = "rooms";

                connection.Send(new OutboxMessage { area = "system", type = "setPlayerId", value = connection.Session.PlayerId.ToString() });
                connection.Send(new OutboxMessage { area = "system", type = "changeArea", value = "rooms" });
            }
        }

        public void RoomsArea(Guid connectionId, string message, InboxMessage inbox)
        {
            if (inbox.area != "rooms")
                return;

            if (!Rep.SessionConnections.ContainsKey(connectionId))
                return;

            var connection = Rep.SessionConnections[connectionId];
            if (connection.Session.Area != "rooms")
                return;

            if(inbox.type == "getAllRooms")
            {
                var settings = Rep.RoomsSettings.Where(x => !x.Value.IsDisabled).ToDictionary(x => x.Key, x => x.Value);
                var outbox = new RoomSettingsOutboxMessage("rooms", "updateRoomList", settings);
                connection.Send(outbox);
                return;
            }

            if (inbox.type == "createRoom")
            {
                var roomSettings = JsonConvert.DeserializeObject<RoomSettingsInboxMessage>(message);
                var __newSettings = new RoomSettings { Id = connection.Session.PlayerId, Size = roomSettings.fieldSize, Name = connection.Session.PlayerName };

                Rep.RoomsSettings[connection.Session.PlayerId] = __newSettings;
                var outbox = new RoomSettingsOutboxMessage("rooms", "updateRoomList", __newSettings.Id, __newSettings);
                foreach(var anyConnection in Rep.SessionConnections.Values)
                    anyConnection.Send(outbox);

                return;
            }

            if (inbox.type == "removeRoom")
            {
                if (Rep.RoomsSettings.Remove(connection.Session.PlayerId))
                    foreach (var anyConnection in Rep.SessionConnections.Values)
                        anyConnection.Send(new RoomSettingsOutboxMessage("rooms", "updateRoomList", connection.Session.PlayerId, null));
            }

            if(inbox.type == "joinGame")
            {

            }
        }
    }

    internal class GameSession
    {
        //public readonly Guid Id = Guid.NewGuid();

        public string PlayerName;
        public int PlayerId;
        public string Area;
    }

    internal class GameRepository
    {
        public readonly Dictionary<Guid, ConnectionProxy> AnonimConnections = new Dictionary<Guid, ConnectionProxy>();
        public readonly Dictionary<Guid, ConnectionProxy> SessionConnections = new Dictionary<Guid, ConnectionProxy>();

        //Будет специальный сервис, кторый будет убивать потерянные сессии по таймауту
        public readonly Dictionary<Guid, GameSession> LostSessions = new Dictionary<Guid, GameSession>();

        //Все комнаты доступные для игры
        public IDictionary<int, RoomSettings> RoomsSettings = new Dictionary<int, RoomSettings>();
    }
}
