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
    }

    internal class GameServer : IConnectionHandler
    {
        public readonly SingleThreadWorker<Action> SingleThread;

        public readonly GameRepository Rep = new GameRepository();

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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public void OnMessage(Guid id, string message)
        {
            var __inbox = JsonConvert.DeserializeObject<InboxMessage>(message);
            if (__inbox.area == "system" && __inbox.type == "join")
            {
                if (!Rep.AnonimConnections.ContainsKey(id))
                    return;

                Guid sessionId;
                if(Guid.TryParse(__inbox.value, out sessionId) && Rep.LostSessions.ContainsKey(sessionId))
                {
                    //Восстанавливаем сессию
                    var connction = Rep.AnonimConnections[id];
                    Rep.AnonimConnections.Remove(id);
                    connction.Session = Rep.LostSessions[sessionId];
                    Rep.LostSessions.Remove(sessionId);
                    Rep.SessionConnections.Add(id, connction);
                }
                else
                {
                    //Создаём новую сессию

                }

            }
                
            if (__inbox.area == "system" && __inbox.type == "logout")
            {
                //Send(new OutboxMessage("system", "logout", null));
            }
        }
    }

    internal class GameSession
    {
        public readonly Guid Id = Guid.NewGuid();
    }

    internal class GameRepository
    {
        public readonly Dictionary<Guid, ConnectionProxy> AnonimConnections = new Dictionary<Guid, ConnectionProxy>();
        public readonly Dictionary<Guid, ConnectionProxy> SessionConnections = new Dictionary<Guid, ConnectionProxy>();

        //Будет специальный сервис, кторый будет убивать потерянные сессии по таймауту
        public readonly Dictionary<Guid, GameSession> LostSessions = new Dictionary<Guid, GameSession>();

    }
}
