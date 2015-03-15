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
            new ConnectionProxy(connection);
        }
    }

    class AnonumouseHandler : IConnectionHandler
    {


        
        public void OnError(Exception e)
        {
            OnClose();
        }

        public void OnClose()
        {
            //Подключение закрыто, а значит ЭТОТ объект больше не нужен
            //Нужен способ сообщить о том, что нужно выйти из этого состояния
            throw new NotImplementedException();
        }

        public void OnOpen()
        {
            //Ничего не делаем
        }

        public void OnMessage(string message)
        {
            var __inbox = JsonConvert.DeserializeObject<InboxMessage>(message);
            if (__inbox.type != "join")
                return;

            //Пытаемся подключить на новых условиях 

        }
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
