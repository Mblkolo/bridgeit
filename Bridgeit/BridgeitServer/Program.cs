using Fleck;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Converters;

namespace BridgeitServer
{
    class Program
    {
        static void Main(string[] args)
        {
            JsonConvert.DefaultSettings = (() =>
            {
                var settings = new JsonSerializerSettings();
                settings.Converters.Add(new StringEnumConverter { CamelCaseText = true });
                return settings;
            });


            FleckLog.Level = LogLevel.Debug;
            var server = new WebSocketServer("ws://0.0.0.0:8181");

            var gameServer = new GameServer();
            server.Start(gameServer.ConfigureConnection);


            var input = Console.ReadLine();
            while (input != "exit")
            {
                input = Console.ReadLine();
            }
        }
    }

    class SimpleSingleThread : ISingleThreadHandler<Action>
    {
        public void Handle(Action item)
        {
            item();
        }

        public void OnStop()
        {
            IsStopped = true;
        }

        public bool IsStopped { get; private set; }

        public Action<Action> Send { get; set; }
    }


    interface IConnectionProxy
    {
        Guid Id { get; }

        PlayerSession Session { get; set; }

        void Send(OutboxMessage message);
    }

    //Концепция такая
    //Подключившийся пользователь сразу от рождения имеет сессию (он же подключен)
    //Но находится в состоянии "анонимное состояние (подключение)"
    //После того как пройдёт запрос на Join, пользователя переводят в системное состояние и
    //создают новое состояние в игре или прикрепляют к уже существующему
    sealed class ConnectionProxy : IConnectionProxy
    {
        private readonly SingleThreadWorker<Action> _singleThread;

        public ConnectionProxy(IWebSocketConnection connection, SingleThreadWorker<Action> singleThread)
        {
            Id = Guid.NewGuid();
            _singleThread = singleThread;
            Connection = connection;
            connection.OnMessage = OnMessage;
            connection.OnOpen = OnOpen;
            connection.OnClose = OnClose;
            connection.OnError = OnError;
        }

        public void Send(string message)
        {
            Connection.Send(message);
        }

        public void Send(OutboxMessage message)
        {
            Connection.Send(JsonConvert.SerializeObject(message));
        }

        private void OnError(Exception e)
        {
            if (Handler != null)
                _singleThread.Put(() => Handler.OnError(Id, e));
        }

        private void OnClose()
        {
            if (Handler != null)
                _singleThread.Put(() => Handler.OnClose(Id));
        }

        private void OnOpen()
        {
            if (Handler != null)
                _singleThread.Put(() => Handler.OnOpen(Id));
        }

        private void OnMessage(string message)
        {
            if (Handler != null)
                _singleThread.Put(() => Handler.OnMessage(Id, message));
        }

        public IWebSocketConnection Connection { get; private set; }
        public IConnectionHandler Handler { get; set; }
        public Guid Id {get; private set;}

        public PlayerSession Session { get; set; }

        public ConnectionProxy()
        {
            Id = Guid.NewGuid();
        }

    }

    interface IConnectionHandler
    {
        void OnError(Guid id, Exception e);
        void OnClose(Guid id);
        void OnOpen(Guid id);
        void OnMessage(Guid id, string message);
    }
}
