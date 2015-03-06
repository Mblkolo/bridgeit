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
        private readonly Dictionary<Guid, ISystemState> _sessionWithPlayer = new Dictionary<Guid, ISystemState>();
        private readonly List<ISystemState> _connetionWithoutPlayer = new List<ISystemState>();
        private readonly Dictionary<Guid, Player> _playerWithoutConnection = new Dictionary<Guid, Player>();

        /// <summary>
        /// Пользователь подключился
        /// </summary>
        /// <param name="state"></param>
        public void Connect(ISystemState state)
        {
            _connetionWithoutPlayer.Add(state);
        }

        public void Disconnect(ISystemState state)
        {
            if (state.Player == null)
                _connetionWithoutPlayer.Remove(state);
            else
            {
                //Игрок есть, а подключения нет
                _playerWithoutConnection.Add(state.Player.Id, state.Player);
                _sessionWithPlayer.Remove(state.Player.Id);
            }
        }

        public void Join(ISystemState state, Guid sessionID)
        {
            if (_sessionWithPlayer.ContainsKey(sessionID))
            {
                state.ShowError("Сначала старую вкладку закрой");
                return;
            }

            if (_playerWithoutConnection.ContainsKey(sessionID))
            {
                state.Player = _playerWithoutConnection[sessionID];
                _playerWithoutConnection.Remove(sessionID);
                _sessionWithPlayer.Add(state.Player.Id, state);
                state.GotoState(state.Player.State);
                return;
            }

            //Пусть логинится
            state.FailJoin();
        }

        public void Login(ISystemState systemState, string name)
        {
            if (_sessionWithPlayer.Values.Any(x => x.Player.Name == name) || _playerWithoutConnection.Values.Any(x => x.Name == name))
            {
                systemState.ShowErrorAsync("Имя занято, выбери другое!");
                return;
            }

            systemState.Player = new Player(Guid.NewGuid()) { State = PalyerState.RoomList };
            systemState.GotoState(systemState.Player.State);
        }

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

        public void LeaveRoomArea(IRoomState roomState)
        {
            _roomSettings.Remove(roomState.Player.Id);
            _roomListener.Remove(roomState.Player.Id);
        }

        public void CreateRoom(IRoomState roomState, RoomSettings settings)
        {
            _roomSettings[roomState.Player.Id] = settings;
            foreach (var __roomState in _roomListener.Values)
                __roomState.UpdateRoomListAsync(roomState.Player.Id, settings);
        }

        public void RemoveRoom(IRoomState roomState)
        {
            if (_roomSettings.Remove(roomState.Player.Id))
                foreach (var __roomState in _roomListener.Values)
                    __roomState.UpdateRoomListAsync(roomState.Player.Id, null);
        }

        public void PlayGame(IRoomState roomState, Guid opponentID)
        {
            if (roomState.Player.Id == opponentID)
                return;

            if (!_roomSettings.ContainsKey(opponentID))
                return;

            var __settings = _roomSettings[opponentID];
            IRoomState __opponentState = _roomListener[opponentID];
            var __game = new Game { Player1 = __opponentState.Player, Player2 = roomState.Player, Settings = __settings };

        }


    }

    class Game
    {
        public IPlayer Player1;
        public IPlayer Player2;
        public RoomSettings Settings;

    }

    class Player
    {
        public PalyerState State;
        public string Name;
        public readonly Guid Id;

        public Player(Guid id)
        {
            Id = id;
        }
    }

    interface ISystemState
    {
        Player Player { get; set; }
        void ShowError(string message);
        void ShowErrorAsync(string message);

        void SetSessionId(Guid id);
        void FailJoin();
        void GotoState(PalyerState state);
    }

    enum PalyerState
    {
        RoomList,
        Game
    }


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
