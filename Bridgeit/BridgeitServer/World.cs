using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
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

        public int lastRoomId;
        public int GetNextRoomId()
        {
            return ++lastRoomId;
        }

        public GameServer()
        {
            SingleThread = new SingleThreadWorker<Action>(new SimpleSingleThread());
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
                    connection.Send(JsonConvert.SerializeObject(new OutboxMessage { area = "system", type = "setSessionId", value = connection.Session.Id.ToString() }));
                connection.Send(JsonConvert.SerializeObject(new OutboxMessage { area = "system", type = "changeArea", value = session.Area }));
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
                foreach (var anyConnection in Rep.SessionConnections.Values)
                    anyConnection.Send(outbox);

                return;
            }

            if (inbox.type == "removeRoom")
            {
                if (Rep.RoomsSettings.Remove(connection.Session.PlayerId))
                    foreach (var anyConnection in Rep.SessionConnections.Values)
                        anyConnection.Send(new RoomSettingsOutboxMessage("rooms", "updateRoomList", connection.Session.PlayerId, null));
            }

            if (inbox.type == "joinToRoom")
            {
                int opponentId;
                if (!int.TryParse(inbox.value, out opponentId))
                    return;

                var oppnentConnection = Rep.SessionConnections.Values.FirstOrDefault(x => x.Session.PlayerId == opponentId);
                if (oppnentConnection == null || oppnentConnection.Session.Area != "rooms")
                    return;

                RoomSettings settings;
                if (!Rep.RoomsSettings.TryGetValue(oppnentConnection.Session.PlayerId, out settings))
                    return;

                var room = new BridgeitRoom(GetNextRoomId(), settings, connection.Session.PlayerId, oppnentConnection.Session.PlayerId);
                Rep.Rooms.Add(room.Id, room);

                connection.Session.Area = "bridgeit";
                oppnentConnection.Session.Area = "bridgeit";

                connection.Send(new OutboxMessage { area = "system", type = "changeArea", value = "bridgeit" });
                oppnentConnection.Send(new OutboxMessage { area = "system", type = "changeArea", value = "bridgeit" });
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

                var mark = connection.Session.PlayerId == room.OwnerId ? 1 : 2;
                room.Field[action.y, action.x] = (byte)mark;

                //TODO проверить, не завершилась ли игра

                room.ActiveId = room.ActiveId == room.OwnerId ? room.OppnentId : room.OwnerId;
                room.StepNo++;
                room.LastActive = DateTime.Now;
                var roomId = room.Id;
                var stepNo = room.StepNo;
                room.Timeout(() => OnBrigeitGameTimeOut(roomId, stepNo));

                var ownerSession = Rep.SessionConnections.Values.FirstOrDefault(x => x.Session.PlayerId == room.OwnerId);
                var opponentSession = Rep.SessionConnections.Values.FirstOrDefault(x => x.Session.PlayerId == room.OppnentId);
                if (ownerSession != null)
                    ownerSession.Send(BridgeitOutboxMessage.Convert("bridgeit", "setRoomState", room));

                if (opponentSession != null)
                    opponentSession.Send(BridgeitOutboxMessage.Convert("bridgeit", "setRoomState", room));
            }
        }

        private void OnBrigeitGameTimeOut(int inRoomId, int inStepNo)
        {
            if (!Rep.Rooms.ContainsKey(inRoomId))
                return;

            var room = Rep.Rooms[inRoomId];
            if (room.StepNo != inStepNo)
                return;

            if (room.Phase == BridgeitRoomPhase.wait)
            {
                room.ActiveId = room.OwnerId;
                room.Phase = BridgeitRoomPhase.game;
            }

            var ownerSession = Rep.SessionConnections.Values.FirstOrDefault(x => x.Session.PlayerId == room.OwnerId);
            var opponentSession = Rep.SessionConnections.Values.FirstOrDefault(x => x.Session.PlayerId == room.OppnentId);
            if (ownerSession == null && opponentSession == null)
                Rep.Rooms.Remove(inRoomId); //TODO придумать другую процедура завершения игры

            //Просто передаём ход
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

    public enum BridgeitRoomPhase
    {
        wait,
        game,
        completed
    }

    internal class BridgeitRoom
    {
        public readonly int Id;
        public readonly int OwnerId;
        public readonly int OppnentId;

        /// <summary>Время на ход</summary>
        public readonly int WaitTime = 5;
        public readonly int StepTime;
        public readonly int FieldSize;

        public BridgeitRoomPhase Phase;

        //Кто сейчас ходит
        public int ActiveId;
        //Номер хода
        public int StepNo;

        //Время последнего действия
        public DateTime LastActive;
        public readonly byte[,] Field;


        public BridgeitRoom(int roomId, RoomSettings settings, int ownerId, int oppnentId)
        {
            Id = roomId;
            OwnerId = ownerId;
            OppnentId = oppnentId;
            StepTime = 30;
            FieldSize = settings.Size;
            Field = new byte[FieldSize * 2 - 1, FieldSize * 2 - 1];
            LastActive = DateTime.Now;

            var r = new Random();
            for (int y = 0; y < FieldSize * 2 - 1; ++y)
            {
                for (int x = 0; x < FieldSize * 2 - 1; ++x)
                {
                    if ((x + y) % 2 != 0)
                        continue;

                    //Field[y, x] = (byte)r.Next(3);
                }
            }
        }

        /// <summary>
        /// Миллисекунд до таймаута
        /// </summary>
        /// <returns></returns>
        public int GetTimeout()
        {
            var timeout = 0;
            if (Phase == BridgeitRoomPhase.wait)
                timeout = WaitTime;
            if (Phase == BridgeitRoomPhase.game)
                timeout = StepTime;

            var result = Math.Max(0, timeout * 1000 - (int)Math.Round((DateTime.Now - LastActive).TotalMilliseconds));
            return result;
        }

        private Timer timer;
        public void Timeout(Action timeoutAction)
        {
            if (timer != null)
                timer.Dispose();

            timer = new Timer(o => timeoutAction(), null, GetTimeout(), 0);
        }

        //Наиваная реализация, без оптимизаций
        public static bool CheckPath(BridgeitRoom room)
        {
            var candidats = new Stack<Ceil>();
            var field = room.Field;
            var maxSize = room.FieldSize * 2 - 1;
            for (int y = 0; y < maxSize; y += 2)
            {
                if (field[y, 0] == 1)
                {
                    candidats.Push(new Ceil(y, 0));
                    field[y, 0] = 3;
                }
            }

            while (candidats.Count > 0)
            {
                var ceil = candidats.Pop();
                //проверки в 8 сторон, это всегда так _весело_
                var directions = new Ceil[] {
                    new Ceil(ceil.Y - 2, ceil.X + 0),
                    new Ceil(ceil.Y - 1, ceil.X + 1),
                    new Ceil(ceil.Y + 0, ceil.X + 2),
                    new Ceil(ceil.Y + 1, ceil.X + 1),
                    new Ceil(ceil.Y + 2, ceil.X + 0),
                    new Ceil(ceil.Y + 1, ceil.X - 1),
                    new Ceil(ceil.Y + 0, ceil.X - 2),
                    new Ceil(ceil.Y - 1, ceil.X - 1)
                };

                for (int i = 0; i < directions.Length; ++i )
                {
                    var dir = directions[i];
                    if(dir.X >= 0 && dir.X < maxSize && dir.Y >= 0 && dir.Y < maxSize && field[dir.Y, dir.X] == 1) 
                    {
                        candidats.Push(dir);
                        field[dir.Y, dir.X] = 3;
                    }
                }
            }

            bool isSuccess = false;
            for(int y=0; y<maxSize; y+=2)
            {
                if(field[y, maxSize-1] == 3)
                {
                    isSuccess = true;
                    break;
                }
            }

            for (int y = 0; y < maxSize; ++y)
            {
                for (int x = 0; x < maxSize; ++x)
                {
                    if (field[y, x] == 3)
                        field[y, x] = 1;
                }
            }

            return isSuccess;
        }
    }

    public struct Ceil
    {
        public readonly int X;
        public readonly int Y;

        public Ceil(int y, int x)
        {
            Y = y;
            X = x;
        }
    }

    internal class GameRepository
    {
        public readonly Dictionary<Guid, ConnectionProxy> AnonimConnections = new Dictionary<Guid, ConnectionProxy>();
        public readonly Dictionary<Guid, ConnectionProxy> SessionConnections = new Dictionary<Guid, ConnectionProxy>();

        //Будет специальный сервис, кторый будет убивать потерянные сессии по таймауту
        public readonly Dictionary<Guid, PlayerSession> LostSessions = new Dictionary<Guid, PlayerSession>();

        //Все комнаты доступные для игры
        public IDictionary<int, RoomSettings> RoomsSettings = new Dictionary<int, RoomSettings>();

        //Комнаты в которых идёт игра
        public IDictionary<int, BridgeitRoom> Rooms = new Dictionary<int, BridgeitRoom>();
    }
}
