using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fleck;

namespace BridgeitServer
{
    sealed class SystemState : ISystemState
    {
        private readonly IWebSocketConnection _socket;
        private StateManager _stateManager;


        public SystemState(IWebSocketConnection socket, StateManager stateManager)
        {
            _socket = socket;
            _stateManager = stateManager;
        }

        public void Init()
        {
            _stateManager.Add(this);
            _socket.OnOpen = () => _stateManager.World.Connect(this);
            _socket.OnClose = OnClose;
            _socket.OnError = x => OnClose();
            _socket.OnMessage = OnMessage;
        }

        private void OnClose()
        {
            _stateManager.World.Disconnect(this);
            _stateManager.Remove(this);
        }

        private void OnMessage(string message)
        {
            //Маршрутизация
        }

        public Player Player { get; set; }

        public void ShowError(string message)
        {
            throw new NotImplementedException();
        }

        public void ShowErrorAsync(string message)
        {
            throw new NotImplementedException();
        }

        public void SetSessionId(Guid id)
        {
            throw new NotImplementedException();
        }

        public void FailJoin()
        {
            throw new NotImplementedException();
        }

        public void GotoState(PlayerState state)
        {
            throw new NotImplementedException();
        }
    }


}
