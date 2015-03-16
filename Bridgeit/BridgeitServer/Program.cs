﻿using Fleck;
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

        private readonly List<SystemState> _states = new List<SystemState>();
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
            new HighStateMashine(SharedData, connection);
        }
    }


    //Убер класс, умеет всё
    class HighStateMashine : IConnectionHandler
    {
        public enum PossibleState { None, Anonim, Connected, Close }

        public PossibleState State;

        public readonly SharedStateData SharedData;

        public HighStateMashine(SharedStateData sharedData, IWebSocketConnection connection)
        {
            SharedData = sharedData;
            _proxy = new ConnectionProxy(connection) { Handler = this };
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
            if (Guid.TryParse(id, out __id) && SharedData.AbandonedLowSm.TryGetValue(__id, out _currentLowMashine))
                SharedData.AbandonedLowSm.Remove(__id);
            else
            {
                _currentLowMashine = new LowStateMashine(SharedData);
                Send(new OutboxMessage("system", "setSessionId", _currentLowMashine.Id.ToString()));
            }

            _currentLowMashine.OnConnect(Send);
            Send(new OutboxMessage("system", "changeArea", _currentLowMashine.Area));
        }

        private void LeaveStateConnected()
        {
            SharedData.AbandonedLowSm.Add(_currentLowMashine.Id, _currentLowMashine);
            _currentLowMashine.OnDisconnect();
        }

        #endregion

        private LowStateMashine _currentLowMashine;
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
            if (__inbox.session != _currentLowMashine.Id)
                return; //TODO может перелогиниться?

            if (__inbox.area != "system")
                _currentLowMashine.OnMessage(message);

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

    class LowStateMashine
    {
        public readonly Guid Id = Guid.NewGuid();
        public string Area { get; private set; }
        public readonly SharedStateData SharedData;
        private Func<OutboxMessage, bool> _sendHandler;


        public LowStateMashine(SharedStateData data)
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
        public readonly Dictionary<Guid, LowStateMashine> AbandonedLowSm = new Dictionary<Guid, LowStateMashine>();
        public readonly List<HighStateMashine> LiveConnection = new List<HighStateMashine>();
    }


    //Концепция такая
    //Подключившийся пользователь сразу от рождения имеет сессию (он же подключен)
    //Но находится в состоянии "анонимное состояние (подключение)"
    //После того как пройдёт запрос на Join, пользователя переводят в системное состояние и
    //создают новое состояние в игре или прикрепляют к уже существующему
    sealed class ConnectionProxy
    {
        public ConnectionProxy(IWebSocketConnection connection)
        {
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
                Handler.OnError(e);
        }

        private void OnClose()
        {
            if (Handler != null)
                Handler.OnClose();
        }

        private void OnOpen()
        {
            if (Handler != null)
                Handler.OnOpen();
        }

        private void OnMessage(string message)
        {
            if (Handler != null)
                Handler.OnMessage(message);
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
