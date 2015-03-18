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
            var stateManager = new StateManager();

            server.Start(stateManager.ConfigureConnection);


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

    /// <summary>
    /// Отвечает за управление состояниями и подключениями клиентов
    /// </summary>
    internal class StateManager
    {
        public readonly GameWorld World = new GameWorld();
        public readonly SharedStateData SharedData = new SharedStateData();
        public readonly SingleThreadWorker<Action> SingleThread;

        private readonly List<SystemState> _states = new List<SystemState>();

        public StateManager()
        {
            SingleThread = new SingleThreadWorker<Action>(new SimpleSingleThread());
            SingleThread.Start();
        }

        public void Add(SystemState systemState)
        {
            _states.Add(systemState);
        }

        public void Remove(SystemState systemState)
        {
            _states.Remove(systemState);
        }

        public void ConfigureConnection(IWebSocketConnection connection)
        {
            var __proxy = new ConnectionProxy(connection, SingleThread);
            var __highMashine = new ConnectionStateMashine(SharedData, __proxy);
            __proxy.Handler = __highMashine;
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


    //Убер класс, умеет всё
    class ConnectionStateMashine : IConnectionHandler
    {
        public enum PossibleState { None, Anonim, Connected, Close }

        public PossibleState State;

        public readonly SharedStateData SharedData;

        public ConnectionStateMashine(SharedStateData sharedData, ConnectionProxy proxy)
        {
            SharedData = sharedData;
            _proxy = proxy;
        }

        #region обрабтка сообщений

        public void OnError(Exception e)
        {
            OnClose();
        }

        public void OnClose()
        {
            StateClose();
        }

        public void OnOpen()
        {
            if (State == PossibleState.None)
                StateAnonim();
        }

        public void OnMessage(string message)
        {
            if (State == PossibleState.Anonim)
                OnMessageAnonim(message);
            else if (State == PossibleState.Connected)
                OnMessageConnected(message);
        }

        #endregion

        #region Переключение состояний

        private void StateAnonim()
        {
            State = PossibleState.Anonim;
            SharedData.LiveConnection.Add(this);
        }

        private void StateClose()
        {
            if (State == PossibleState.Connected)
                LeaveStateConnected();

            State = PossibleState.Close;
            SharedData.LiveConnection.Remove(this);

            _proxy.Handler = null;
            _proxy = null;
        }

        private void StateConnected(string id)
        {
            State = PossibleState.Connected;

            Guid __id;
            if (Guid.TryParse(id, out __id) && SharedData.AbandonedLowSm.TryGetValue(__id, out _currentSessionMashine))
                SharedData.AbandonedLowSm.Remove(__id);
            else
            {
                _currentSessionMashine = new SessionStateMashine(SharedData);
                Send(new OutboxMessage("system", "setSessionId", _currentSessionMashine.Id.ToString()));
            }

            _currentSessionMashine.OnConnect(Send);
            Send(new OutboxMessage("system", "changeArea", _currentSessionMashine.Area));
        }

        private void LeaveStateConnected()
        {
            SharedData.AbandonedLowSm.Add(_currentSessionMashine.Id, _currentSessionMashine);
            _currentSessionMashine.OnDisconnect();
        }

        #endregion

        private SessionStateMashine _currentSessionMashine;
        private ConnectionProxy _proxy;

        private void OnMessageAnonim(string message)
        {
            var __inbox = JsonConvert.DeserializeObject<InboxMessage>(message);
            if (__inbox.type != "join")
                return;

            StateConnected(__inbox.value);
        }

        private void OnMessageConnected(string message)
        {
            //предварительаня обработка соощений
            var __inbox = JsonConvert.DeserializeObject<InboxMessage>(message);
            if (__inbox.session != _currentSessionMashine.Id)
                return; //TODO может перелогиниться?

            if (__inbox.area != "system")
                _currentSessionMashine.OnMessage(message);

            if (__inbox.type == "logout")
            {
                StateClose();
                Send(new OutboxMessage("system", "logout", null));
            }
        }

        private bool Send(OutboxMessage outbox)
        {
            if (State != PossibleState.Connected)
                return false;

            var __message = JsonConvert.SerializeObject(outbox);
            _proxy.Send(__message);
            return true;
        }
    }

    class SessionStateMashine
    {
        public readonly Guid Id = Guid.NewGuid();
        public string Area { get; private set; }
        public readonly SharedStateData SharedData;
        private Func<OutboxMessage, bool> _sendHandler;


        public SessionStateMashine(SharedStateData data)
        {
            SharedData = data;
            Area = "welcome";
        }

        public void OnMessage(string message)
        {

        }

        public void OnConnect(Func<OutboxMessage, bool> sendHandler)
        {
            _sendHandler = sendHandler;
        }

        public void OnDisconnect()
        {
            _sendHandler = null;
        }

    }

    class SharedStateData
    {
        public readonly Dictionary<Guid, SessionStateMashine> AbandonedLowSm = new Dictionary<Guid, SessionStateMashine>();
        public readonly List<ConnectionStateMashine> LiveConnection = new List<ConnectionStateMashine>();
    }


    //Концепция такая
    //Подключившийся пользователь сразу от рождения имеет сессию (он же подключен)
    //Но находится в состоянии "анонимное состояние (подключение)"
    //После того как пройдёт запрос на Join, пользователя переводят в системное состояние и
    //создают новое состояние в игре или прикрепляют к уже существующему
    sealed class ConnectionProxy
    {
        private readonly SingleThreadWorker<Action> _singleThread;

        public ConnectionProxy(IWebSocketConnection connection, SingleThreadWorker<Action> singleThread)
        {
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

        private void OnError(Exception e)
        {
            if (Handler != null)
                _singleThread.Put(() => Handler.OnError(e));
        }

        private void OnClose()
        {
            if (Handler != null)
                _singleThread.Put(() => Handler.OnClose());
        }

        private void OnOpen()
        {
            if (Handler != null)
                _singleThread.Put(() => Handler.OnOpen());
        }

        private void OnMessage(string message)
        {
            if (Handler != null)
                _singleThread.Put(() => Handler.OnMessage(message));
        }

        public IWebSocketConnection Connection { get; private set; }
        public IConnectionHandler Handler { get; set; }

    }

    interface IConnectionHandler
    {
        void OnError(Exception e);
        void OnClose();
        void OnOpen();
        void OnMessage(string message);
    }
}
