using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fleck;
using Newtonsoft.Json;

namespace BridgeitServer
{
    sealed class GameWorld
    {
        private readonly Dictionary<Guid, ISystemState> _systemStates = new Dictionary<Guid, ISystemState>();
        private readonly List<ISystemState> _undefinedSession = new List<ISystemState>();

        /// <summary>
        /// Пользователь подключился
        /// </summary>
        /// <param name="state"></param>
        public void Connect(ISystemState state)
        {
            _undefinedSession.Add(state);
        }

        public void Disconnect(ISystemState state)
        {
            if (state.SesionId == Guid.Empty)
                _undefinedSession.Remove(state);
            else
                _systemStates.Remove(state.SesionId);
        }
        //
        public void Join(ISystemState state, Guid sessionID)
        {
            ISystemState __alreadySystemState;
            if (_systemStates.TryGetValue(sessionID, out __alreadySystemState))
            {
                __alreadySystemState.Join(state);
                //TODO сообщить о текущем состоянии?
            }
            else
            {
                state.SesionId = sessionID;
                _systemStates.Add(state.SesionId, state);
            }
        }

        //public void Login(IWelcomeState welcomeState, string name)
        //{
        //    if (_sessions.Values.Any(x => x.Name == name))
        //    {
        //        welcomeState.ShowErrorAsync("Имя занято, выбери другое!");
        //        return;
        //    }

        //    welcomeState.Session.Name = name;
        //    welcomeState.GotoListStateAsync();
        //}

        readonly Dictionary<Guid, RoomSettings> _roomSettings = new Dictionary<Guid, RoomSettings>();
        readonly Dictionary<Guid, IRoomState> _roomListener = new Dictionary<Guid, IRoomState>();

        /// <summary>
        /// Подписка на обновление списка комнат
        /// </summary>
        public void EnterRoomsArea(IRoomState roomState)
        {
            _roomListener[roomState.Player.Id] = roomState;
            roomState.SendRoomListAsync(_roomSettings.Values);
        }

        public void CreateRoom(IRoomState roomState, string value)
        {
            RoomSettings __s = RoomSettings.TryCreate(value);
            if (__s == null)
                return;

            _roomSettings[roomState.Player.Id] = __s;
            foreach (var __roomState in _roomListener.Values)
                __roomState.UpdateRoomListAsync(roomState.Player.Id, __s);
        }

        public void RemoveRoom(IRoomState roomState)
        {
            if (_roomSettings.Remove(roomState.Player.Id))
                foreach (var __roomState in _roomListener.Values)
                    __roomState.UpdateRoomListAsync(roomState.Player.Id, null);
        }

        public void LeaveRoomArea(IRoomState roomState)
        {
            _roomSettings.Remove(roomState.Player.Id);
            _roomListener.Remove(roomState.Player.Id);
        }
    }

    interface ISystemState
    {
        Guid SesionId { get; set; }
        void Join(ISystemState state);
    }

    //interface IWelcomeState
    //{
    //    ISession Session { get; }
    //    void ShowErrorAsync(string erroMessage);
    //    void GotoListStateAsync();
    //}

    interface IRoomState
    {
        IPlayer Player { get; }
        void SendRoomListAsync(IEnumerable<RoomSettings> settings);
        void UpdateRoomListAsync(Guid id, RoomSettings roomSettings);
    }

    interface IPlayer
    {
        string Name { get; }
        Guid Id { get; }
    }

    class RoomSettings
    {
        public static RoomSettings TryCreate(string value)
        {
            throw new NotImplementedException();
        }
    }



    class Session
    {
        private readonly GameWorld _world;

        public readonly List<IWebSocketConnection> Connections = new List<IWebSocketConnection>();
        public readonly Guid Id = Guid.NewGuid();
        public string Name;

        public Session(GameWorld world)
        {
            _world = world;
        }

        public void Join(IWebSocketConnection connection)
        {
            Connections.Add(connection);
            connection.OnMessage = OnMessage;
            connection.OnClose = () => OnClose(connection);
            connection.OnError = error => OnClose(connection);
        }

        private void OnMessage(string message)
        {
            //Routers[_currentRouter](this, _world, message);
        }

        private void OnClose(IWebSocketConnection connection)
        {
            Connections.Remove(connection);
        }
    }
}
