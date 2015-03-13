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
        private readonly StateManager _stateManager;
        private IInboxState _currentState;

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
            var __inbox = Newtonsoft.Json.JsonConvert.DeserializeObject<InboxMessage>(message);
            if (__inbox.type != "system")
            {
                if (Player == null || __inbox.session != Player.Id)
                    return;

                if (_currentState.StateName != __inbox.type)
                    return;

                _currentState.OnMessage(message);
                return;
            }

            switch (__inbox.value)
            {
                case "Join":
                    _stateManager.World.Join(this, __inbox.session);
                    break;

                case "Login":
                    _stateManager.World.Login(this, __inbox.value);
                    break;
            }
        }

        public Player Player { get; set; }

        public void ShowErrorAsync(string message)
        {
            Send("ShowError", message);
        }

        public void SetSessionId(Guid id)
        {
            Send("SetSessionId", id.ToString());
        }

        public void FailJoin()
        {
            Send("FailJoin", "");
        }

        public void GotoState(PlayerState state)
        {
            if (state == PlayerState.RoomList)
                _currentState = new RoomListState(_stateManager.World, Player, _socket);

            //TODO всё сложнее
            Send("GotoState", state.ToString());
        }

        void Send(string type, string message)
        {
            var __out = new OutboxMessage("system", type, message);
            var __outMessage = Newtonsoft.Json.JsonConvert.SerializeObject(__out);
            _socket.Send(__outMessage);
        }
    }

    interface IInboxState
    {
        void OnEnter();
        void OnMessage(string message);
        void OnLeave();
        string StateName { get; }
    }

    class RoomListState : IRoomState, IInboxState
    {
        private readonly GameWorld _world;
        private readonly IWebSocketConnection _socket;

        public RoomListState(GameWorld world, IPlayer player, IWebSocketConnection socket)
        {
            _world = world;
            Player = player;
            _socket = socket;
        }

        #region IRoomState

        public IPlayer Player { get; private set; }
        public void UpdateRoomListAsync(Dictionary<Guid, RoomSettings> settings)
        {
            var __dto = new RoomSettingsOutboxMessage(Name, "updateRoomList", settings);
            var __outMessage = Newtonsoft.Json.JsonConvert.SerializeObject(__dto);
            _socket.Send(__outMessage);
        }

        #endregion

        #region IInboxState

        public void OnEnter()
        {
            _world.EnterRoomsArea(this);
        }

        public void OnMessage(string message)
        {
            var __inbox = Newtonsoft.Json.JsonConvert.DeserializeObject<InboxMessage>(message);
            switch (__inbox.type)
            {
                case "createRoom":
                    var __dto = Newtonsoft.Json.JsonConvert.DeserializeObject<RoomSettingsDto>(message);
                    var __settings = RoomSettingsDto.Convert(__dto);
                    _world.CreateRoom(this, __settings);
                    break;

                case "removeRoom":
                    _world.RemoveRoom(this);
                    break;

                case "playGame":
                    Guid __opponentId;
                    if (Guid.TryParse(__inbox.value, out __opponentId))
                        _world.PlayGame(this, __opponentId);
                    break;
            }
        }

        public void OnLeave()
        {
            _world.LeaveRoomArea(this);
        }

        public string StateName { get { return Name; } }
        public static readonly string Name = "roomList";

        #endregion
    }

    class GameSate : IGameState, IInboxState
    {
        private readonly GameWorld _world;
        private readonly IWebSocketConnection _socket;

        public GameSate(GameWorld world, IPlayer player, IWebSocketConnection socket)
        {
            _world = world;
            Player = player;
            _socket = socket;
        }

        #region IGameState

        public IPlayer Player { get; private set; }

        public void SendGameState(SimpleGameState state)
        {
            throw new NotImplementedException();
        }

        #endregion


        #region IGameState

        public void OnEnter()
        {
            _world.EnterTheGame(this);
        }

        public void OnMessage(string message)
        {
            throw new NotImplementedException();
        }

        public void OnLeave()
        {
            _world.LeaveTheGame(this);
        }

        public string StateName { get { return Name; } }

        public static readonly string Name = "game";

        #endregion
    }

    class OutboxMessage
    {
        public string area;
        public string type;
        public string value;

        public OutboxMessage()
        {
        }

        public OutboxMessage(string area, string type, string value)
        {
            this.area = area;
            this.type = type;
            this.value = value;
        }
    }

    class RoomSettingsOutboxMessage : OutboxMessage
    {
        public Dictionary<Guid, RoomSettingsDto> settings;

        public RoomSettingsOutboxMessage(string area, string type, IDictionary<Guid, RoomSettings> rooms)
            : base(area, type, null)
        {
            settings = rooms.ToDictionary(x => x.Key, v => RoomSettingsDto.Convert(v.Value));
        }
    }

    class RoomSettingsDto
    {
        public string text;

        public static RoomSettingsDto Convert(RoomSettings settings)
        {
            return new RoomSettingsDto { text = settings.Text };
        }

        public static RoomSettings Convert(RoomSettingsDto settings)
        {
            return new RoomSettings { Text = settings.text };
        }
    }

    class InboxMessage
    {
        public Guid session;
        public string type;
        public string value;
    }


}
