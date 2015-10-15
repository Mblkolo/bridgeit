using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Fleck;
using Newtonsoft.Json;

namespace BridgeitServer
{
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

        public int lastRoomId;
        public int GetNextRoomId()
        {
            return ++lastRoomId;
        }

        public GameServer(bool withSingleThread = true)
        {
            SingleThread = new SingleThreadWorker<Action>(new SimpleSingleThread());
            if (withSingleThread)
                SingleThread.Start();
        }

        public void ConfigureConnection(IWebSocketConnection connection)
        {
            var __proxy = new ConnectionProxy(connection, SingleThread) { Handler = this };
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
                Rep.LostSessions.Add(connect.Session.Id, connect.Session);
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

            if (__inbox.area == "bridgeit")
                BridgeitArea(connectionId, message, __inbox);

        }

        public void SystemArea(Guid connectionId, string message, InboxMessage inbox)
        {
            if (inbox.area != "system")
                return;


            if (inbox.type == "join")
            {
                if (!Rep.AnonimConnections.ContainsKey(connectionId))
                    return;

                PlayerSession session;
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
                    session = new PlayerSession { Area = "welcome" };
                }
                var connection = Rep.AnonimConnections[connectionId];
                Rep.AnonimConnections.Remove(connectionId);
                connection.Session = session;
                Rep.SessionConnections.Add(connectionId, connection);

                if (session.Id != sessionId)
                    connection.Send(new OutboxMessage { area = "system", type = "setSessionId", value = connection.Session.Id.ToString() });
                connection.Send(new OutboxMessage { area = "system", type = "changeArea", value = session.Area });
            }

            if (inbox.type == "logout")
            {
                if (!Rep.SessionConnections.ContainsKey(connectionId))
                    return;

                //Всё, нет у него больше сессии
                var connection = Rep.SessionConnections[connectionId];
                connection.Session = null;
                Rep.SessionConnections.Remove(connectionId);


                //Rep.LostSessions.Add(connection.Se Id, connection.Session);


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

            if (inbox.type == "login")
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

                connection.Send(new OutboxMessage { area = "system", type = "setPlayerId", value = connection.Session.PlayerId.ToString(CultureInfo.InvariantCulture) });
                connection.Send(new OutboxMessage { area = "system", type = "setPlayerName", value = connection.Session.PlayerName });
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

            if (inbox.type == "getAllRooms")
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
                foreach (var anyConnection in Rep.SessionConnections.Values.Where(x => x.Session.Area == "rooms"))
                    anyConnection.Send(outbox);

                return;
            }

            if (inbox.type == "removeRoom")
            {
                if (Rep.RoomsSettings.Remove(connection.Session.PlayerId))
                {
                    var removeMessage = new RoomSettingsOutboxMessage("rooms", "updateRoomList", connection.Session.PlayerId, null);
                    foreach (var anyConnection in Rep.SessionConnections.Values.Where(x => x.Session.Area == "rooms"))
                        anyConnection.Send(removeMessage);
                }
            }

            if (inbox.type == "joinToRoom")
            {
                int ownerId;
                if (!int.TryParse(inbox.value, out ownerId))
                    return;

                var ownerConnection = Rep.SessionConnections.Values.FirstOrDefault(x => x.Session.PlayerId == ownerId);
                if (ownerConnection == null || ownerConnection.Session.Area != "rooms")
                    return;

                RoomSettings settings;
                if (!Rep.RoomsSettings.TryGetValue(ownerConnection.Session.PlayerId, out settings))
                    return;

                Rep.RoomsSettings.Remove(ownerConnection.Session.PlayerId);

                var room = new BridgeitRoom(GetNextRoomId(), settings, ownerConnection.Session, connection.Session);
                Rep.Rooms.Add(room.Id, room);

                connection.Session.Area = "bridgeit";
                ownerConnection.Session.Area = "bridgeit";

                connection.Send(new OutboxMessage { area = "system", type = "changeArea", value = "bridgeit" });
                ownerConnection.Send(new OutboxMessage { area = "system", type = "changeArea", value = "bridgeit" });


                var roomsForRemove = new Dictionary<int, RoomSettings>() { { connection.Session.PlayerId, null } };
                RoomSettings opponentSettings;
                if (Rep.RoomsSettings.TryGetValue(connection.Session.PlayerId, out opponentSettings))
                    roomsForRemove.Add(connection.Session.PlayerId, null);

                var removeRoomMessage = new RoomSettingsOutboxMessage("rooms", "updateRoomList", connection.Session.PlayerId, null);
                foreach (var anyConnection in Rep.SessionConnections.Values.Where(x => x.Session.Area == "rooms"))
                    anyConnection.Send(removeRoomMessage);

                return;
            }
        }

        public void BridgeitArea(Guid connectionId, string message, InboxMessage inbox)
        {
            if (inbox.area != "bridgeit")
                return;

            if (!Rep.SessionConnections.ContainsKey(connectionId))
                return;

            var connection = Rep.SessionConnections[connectionId];
            if (connection.Session.Area != "bridgeit")
                return;

            if (inbox.type == "getRoomState")
            {
                var room = Rep.Rooms.Values.FirstOrDefault(x => x.OwnerId == connection.Session.PlayerId || x.OppnentId == connection.Session.PlayerId);
                if (room == null)
                {
                    //Пошёл отсюда, давай давай!
                    connection.Session.Area = "rooms";
                    connection.Send(new OutboxMessage { area = "system", type = "changeArea", value = connection.Session.Area });
                    return;
                }

                connection.Send(BridgeitOutboxMessage.Convert("bridgeit", "setRoomSettings", room));

                //Замыкания опасны
                var roomId = room.Id;
                var stepNo = room.StepNo;
                room.Timeout(() => SingleThread.Put(() => OnBrigeitGameTimeOut(roomId, stepNo)));
                return;
            }

            if (inbox.type == "executeAction")
            {
                var room = Rep.Rooms.Values.FirstOrDefault(x => x.OwnerId == connection.Session.PlayerId || x.OppnentId == connection.Session.PlayerId);
                if (room == null || room.Phase != BridgeitRoomPhase.game)
                {
                    return;
                }

                var action = JsonConvert.DeserializeObject<PlayeerActonDTO>(inbox.value);
                if (action.stepNo != room.StepNo)
                    return;

                if (action.x < 0 || action.x >= room.FieldSize * 2 - 1)
                    return;

                if (action.y < 0 || action.y >= room.FieldSize * 2 - 1)
                    return;

                if ((action.x + action.y) % 2 != 0)
                    return;


                if (room.Field[action.y, action.x] != 0)
                    return;

                bool isFinish = false;
                if (connection.Session.PlayerId == room.OwnerId)
                {
                    room.Field[action.y, action.x] = 1;
                    isFinish = BridgeitRoom.CheckHorizontalPath(room);
                }
                else
                {
                    room.Field[action.y, action.x] = 2;
                    isFinish = BridgeitRoom.CheckVerticalPath(room);
                }

                if (isFinish)
                {
                    room.Phase = BridgeitRoomPhase.completed;
                    room.StepNo++;
                    room.LastActive = DateTime.Now;
                }
                else
                {
                    room.ActiveId = room.ActiveId == room.OwnerId ? room.OppnentId : room.OwnerId;
                    room.StepNo++;
                    room.LastActive = DateTime.Now;
                    var roomId = room.Id;
                    var stepNo = room.StepNo;
                    room.Timeout(() => OnBrigeitGameTimeOut(roomId, stepNo));
                }

                var ownerSession = Rep.SessionConnections.Values.FirstOrDefault(x => x.Session.PlayerId == room.OwnerId);
                var opponentSession = Rep.SessionConnections.Values.FirstOrDefault(x => x.Session.PlayerId == room.OppnentId);
                if (ownerSession != null)
                    ownerSession.Send(BridgeitOutboxMessage.Convert("bridgeit", "setRoomState", room));

                if (opponentSession != null)
                    opponentSession.Send(BridgeitOutboxMessage.Convert("bridgeit", "setRoomState", room));

                return;
            }

            if (inbox.type == "leaveGame")
            {
                var room = Rep.Rooms.Values.FirstOrDefault(x => x.OwnerId == connection.Session.PlayerId || x.OppnentId == connection.Session.PlayerId);
                if (room == null || room.Phase != BridgeitRoomPhase.completed)
                    return;

                //Проверяем что все покинили игру
                var anotherPlayerId = connection.Session.PlayerId == room.OwnerId ? room.OppnentId : room.OwnerId;
                var anotherSession = Rep.LostSessions.Values.FirstOrDefault(x => x.PlayerId == anotherPlayerId);
                if (anotherSession == null)
                {
                    var anotherConnection = Rep.SessionConnections.Values.FirstOrDefault(x => x.Session.PlayerId == anotherPlayerId);
                    if (anotherConnection != null)
                        anotherSession = anotherConnection.Session;
                }

                if (anotherSession == null || anotherSession.Area != "bridgeit")
                {
                    //TODO прибить таймер
                    Rep.Rooms.Remove(room.Id);
                }

                connection.Session.Area = "rooms";
                connection.Send(new OutboxMessage { area = "system", type = "changeArea", value = connection.Session.Area });
            }
        }

        private void OnBrigeitGameTimeOut(int inRoomId, int inStepNo)
        {
            if (!Rep.Rooms.ContainsKey(inRoomId))
                return;

            var room = Rep.Rooms[inRoomId];
            if (room.StepNo != inStepNo)
                return;

            var ownerSession = Rep.SessionConnections.Values.FirstOrDefault(x => x.Session.PlayerId == room.OwnerId);
            var opponentSession = Rep.SessionConnections.Values.FirstOrDefault(x => x.Session.PlayerId == room.OppnentId);
            if (ownerSession == null && opponentSession == null)
                Rep.Rooms.Remove(inRoomId); //TODO придумать другую процедура завершения игры

            //Просто передаём ход
            if (room.Phase == BridgeitRoomPhase.wait)
            {
                room.ActiveId = room.OwnerId;
                room.Phase = BridgeitRoomPhase.game;
            }
            else if (room.Phase == BridgeitRoomPhase.game)
                room.ActiveId = room.ActiveId == room.OwnerId ? room.OppnentId : room.OwnerId;

            room.StepNo++;
            room.LastActive = DateTime.Now;
            var roomId = room.Id;
            var stepNo = room.StepNo;
            room.Timeout(() => OnBrigeitGameTimeOut(roomId, stepNo));

            if (ownerSession != null)
                ownerSession.Send(BridgeitOutboxMessage.Convert("bridgeit", "setRoomState", room));

            if (opponentSession != null)
                opponentSession.Send(BridgeitOutboxMessage.Convert("bridgeit", "setRoomState", room));
        }
    }

    internal class PlayerSession
    {
        public readonly Guid Id = Guid.NewGuid();
        public string PlayerName;
        public int PlayerId;
        public string Area;
    }

    internal class GameRepository
    {
        public readonly Dictionary<Guid, IConnectionProxy> AnonimConnections = new Dictionary<Guid, IConnectionProxy>();
        public readonly Dictionary<Guid, IConnectionProxy> SessionConnections = new Dictionary<Guid, IConnectionProxy>();

        //Будет специальный сервис, кторый будет убивать потерянные сессии по таймауту
        public readonly Dictionary<Guid, PlayerSession> LostSessions = new Dictionary<Guid, PlayerSession>();

        //Все комнаты доступные для игры
        public IDictionary<int, RoomSettings> RoomsSettings = new Dictionary<int, RoomSettings>();

        //Комнаты в которых идёт игра
        public IDictionary<int, BridgeitRoom> Rooms = new Dictionary<int, BridgeitRoom>();
    }
}
