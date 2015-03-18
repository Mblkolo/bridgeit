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
        //public readonly GameWorld World = new GameWorld();
        public readonly SharedStateData SharedData = new SharedStateData();
        public readonly SingleThreadWorker<Action> SingleThread;

        //private readonly List<SystemState> _states = new List<SystemState>();

        public StateManager()
        {
            SingleThread = new SingleThreadWorker<Action>(new SimpleSingleThread());
            SingleThread.Start();
        }

        //public void Add(SystemState systemState)
        //{
        //    _states.Add(systemState);
        //}

        //public void Remove(SystemState systemState)
        //{
        //    _states.Remove(systemState);
        //}

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
            if (Guid.TryParse(id, out __id) && SharedData.AbandonedLowFsm.TryGetValue(__id, out _currentSessionMashine))
                SharedData.AbandonedLowFsm.Remove(__id);
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
            SharedData.AbandonedLowFsm.Add(_currentSessionMashine.Id, _currentSessionMashine);
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
                //TODO сделать настоящий разлогин, с уничтожением сессии
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

        private Player _player;

        public SessionStateMashine(SharedStateData data)
        {
            SharedData = data;
            Area = "welcome";
        }

        public void OnMessage(string message)
        {
            if (_sendHandler == null)
                return;

            var __inbox = JsonConvert.DeserializeObject<InboxMessage>(message);
            if (__inbox.area != Area || __inbox.session != Id)
                return;

            if (Area == "welcome")
                OnWelcomeAreaMessage(__inbox, message);
            else if (Area == "rooms")
                OnRoomsAreaMessage(__inbox, message);
        }

        private void OnWelcomeAreaMessage(InboxMessage inbox, string message)
        {
            if (inbox.type == "login" && _player == null)
            {
                if (!string.IsNullOrWhiteSpace(inbox.value))
                {
                    if (SharedData.Players.Values.Any(x => x.Name == inbox.value))
                        Send("showError", "Имя занято, выбери другое");
                    else
                    {
                        _player = new Player(inbox.value);
                        AreaRooms();
                    }
                }
            }
        }

        private void OnRoomsAreaMessage(InboxMessage inbox, string message)
        {
            if (inbox.type == "createRoom")
            {
                var __inbox = JsonConvert.DeserializeObject<RoomSettingsInboxMessage>(message);
                if (__inbox.fieldSize < 3 || __inbox.fieldSize < 10)
                    return; //TODO выругаться

                if (!SharedData.RoomsSettings.ContainsKey(_player.Name))
                    return;

                var __newSettings = new RoomSettings { Size = __inbox.fieldSize };
                SharedData.RoomsSettings.Add(_player.Name, __newSettings);

                var __updateData = new Dictionary<string, RoomSettings> { { _player.Name, __newSettings } };
                foreach (var __listener in SharedData.RoomsListeners.Values)
                    __listener.UpdateRoomList(__updateData);
            }
            else if (inbox.type == "removeRoom")
            {
                if (SharedData.RoomsSettings.Remove(_player.Name))
                {
                    var __updateData = new Dictionary<string, RoomSettings> { { _player.Name, null } };
                    foreach (var __listener in SharedData.RoomsListeners.Values)
                        __listener.UpdateRoomList(__updateData);
                }
            }
        }

        private void Send(string type, string value)
        {
            Send(new OutboxMessage("welcome", type, value));
        }

        private void Send(OutboxMessage outbox)
        {
            if (_sendHandler != null)
                _sendHandler(outbox);
        }

        public void OnConnect(Func<OutboxMessage, bool> sendHandler)
        {
            _sendHandler = sendHandler;
        }

        public void OnDisconnect()
        {
            _sendHandler = null;
        }

        #region переключение областей
        private void AreaRooms()
        {
            //Вход в список комнат
            Area = "rooms";

            //Дейсвтия по входу в комнату
            var __roomListener = new RoomsAreaListener(this);
            SharedData.RoomsListeners.Add(_player.Name, __roomListener);

            Send(new OutboxMessage("system", "changeArea", Area));
            __roomListener.UpdateRoomList(SharedData.RoomsSettings);
        }
        #endregion

        private class RoomsAreaListener : IRoomsAreaListener
        {
            private readonly SessionStateMashine _sessionFsm;
            public RoomsAreaListener(SessionStateMashine sessionFsm)
            {
                _sessionFsm = sessionFsm;
            }

            public void UpdateRoomList(Dictionary<string, RoomSettings> roomsSettings)
            {
                var __outbox = new RoomSettingsOutboxMessage("rooms", "updateRoomList", roomsSettings);
                _sessionFsm.Send(__outbox);
            }
        }
    }


    class SharedStateData
    {
        public readonly Dictionary<Guid, SessionStateMashine> AbandonedLowFsm = new Dictionary<Guid, SessionStateMashine>();
        public readonly List<ConnectionStateMashine> LiveConnection = new List<ConnectionStateMashine>();
        public readonly Dictionary<string, Player> Players = new Dictionary<string, Player>();

        public readonly Dictionary<string, RoomSettings> RoomsSettings = new Dictionary<string, RoomSettings>();
        public readonly Dictionary<string, IRoomsAreaListener> RoomsListeners = new Dictionary<string, IRoomsAreaListener>();
    }

    interface IRoomsAreaListener
    {
        void UpdateRoomList(Dictionary<string, RoomSettings> roomsSettings);
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
