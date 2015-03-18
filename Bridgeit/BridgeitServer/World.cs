using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fleck;

namespace BridgeitServer
{
    //sealed class GameWorld
    //{
    //    private readonly Dictionary<Guid, ISystemState> _sessionWithPlayer = new Dictionary<Guid, ISystemState>();
    //    private readonly List<ISystemState> _connetionWithoutPlayer = new List<ISystemState>();
    //    private readonly Dictionary<Guid, Player> _playerWithoutConnection = new Dictionary<Guid, Player>();

    //    /// <summary>
    //    /// Пользователь подключился
    //    /// </summary>
    //    /// <param name="state"></param>
    //    public void Connect(ISystemState state)
    //    {
    //        _connetionWithoutPlayer.Add(state);
    //    }

    //    public void Disconnect(ISystemState state)
    //    {
    //        if (state.Player == null)
    //            _connetionWithoutPlayer.Remove(state);
    //        else
    //        {
    //            //Игрок есть, а подключения нет
    //            _playerWithoutConnection.Add(state.Player.Id, state.Player);
    //            _sessionWithPlayer.Remove(state.Player.Id);
    //        }
    //    }

    //    public void Join(ISystemState state, Guid sessionID)
    //    {
    //        if (_sessionWithPlayer.ContainsKey(sessionID))
    //        {
    //            state.ShowErrorAsync("Сначала старую вкладку закрой");
    //            return;
    //        }

    //        if (_playerWithoutConnection.ContainsKey(sessionID))
    //        {
    //            state.Player = _playerWithoutConnection[sessionID];
    //            _playerWithoutConnection.Remove(sessionID);
    //            _sessionWithPlayer.Add(state.Player.Id, state);
    //            state.GotoState(state.Player.State);
    //            return;
    //        }

    //        //Пусть логинится
    //        state.FailJoin();
    //    }

    //    public void Login(ISystemState systemState, string name)
    //    {
    //        if (_sessionWithPlayer.Values.Any(x => x.Player.Name == name) || _playerWithoutConnection.Values.Any(x => x.Name == name))
    //        {
    //            systemState.ShowErrorAsync("Имя занято, выбери другое!");
    //            return;
    //        }

    //        systemState.Player = new Player(Guid.NewGuid()) { State = PlayerState.RoomList };
    //        systemState.GotoState(systemState.Player.State);
    //    }

    //    public void Logout(ISystemState systemState)
    //    {
    //        if (systemState.Player == null)
    //            return;

    //        _sessionWithPlayer.Remove(systemState.Player.Id);
    //        systemState.Player = null;
    //        _connetionWithoutPlayer.Add(systemState);
    //        systemState.GotoWelcomeState();
    //    }

    //    readonly Dictionary<Guid, RoomSettings> _roomSettings = new Dictionary<Guid, RoomSettings>();
    //    readonly Dictionary<Guid, IRoomState> _roomListener = new Dictionary<Guid, IRoomState>();

    //    /// <summary>
    //    /// Подписка на обновление списка комнат
    //    /// </summary>
    //    public void EnterRoomsArea(IRoomState roomState)
    //    {
    //        _roomListener[roomState.Player.Id] = roomState;
    //        roomState.UpdateRoomListAsync(_roomSettings);
    //    }

    //    public void LeaveRoomArea(IRoomState roomState)
    //    {
    //        _roomSettings.Remove(roomState.Player.Id);
    //        _roomListener.Remove(roomState.Player.Id);
    //    }

    //    public void CreateRoom(IRoomState roomState, RoomSettings settings)
    //    {
    //        _roomSettings[roomState.Player.Id] = settings;
    //        foreach (var __roomState in _roomListener.Values)
    //            __roomState.UpdateRoomListAsync(new Dictionary<Guid, RoomSettings> { { roomState.Player.Id, settings } });
    //    }

    //    public void RemoveRoom(IRoomState roomState)
    //    {
    //        if (_roomSettings.Remove(roomState.Player.Id))
    //            foreach (var __roomState in _roomListener.Values)
    //                __roomState.UpdateRoomListAsync(new Dictionary<Guid, RoomSettings> { { roomState.Player.Id, null } });
    //    }

    //    public void PlayGame(IRoomState roomState, Guid opponentID)
    //    {
    //        if (roomState.Player.Id == opponentID)
    //            return;

    //        if (!_roomSettings.ContainsKey(opponentID))
    //            return;

    //        var __settings = _roomSettings[opponentID];
    //        IRoomState __opponentState = _roomListener[opponentID];

    //        var __game = new SimpleGame { Player1 = __opponentState.Player, Player2 = roomState.Player, Settings = __settings };
    //        _gameList.Add(__game);

    //        roomState.Player.State = PlayerState.Game;
    //        __opponentState.Player.State = PlayerState.Game;
    //        _sessionWithPlayer[roomState.Player.Id].GotoState(roomState.Player.State);
    //        _sessionWithPlayer[__opponentState.Player.Id].GotoState(__opponentState.Player.State);
    //    }

    //    private readonly List<SimpleGame> _gameList = new List<SimpleGame>();
    //    private readonly Dictionary<Guid, IGameState> _gameStates = new Dictionary<Guid, IGameState>();


    //    public void EnterTheGame(IGameState gameState)
    //    {
    //        _gameStates.Add(gameState.Player.Id, gameState);
    //    }

    //    public void LeaveTheGame(IGameState gameState)
    //    {
    //        if (!_gameStates.Remove(gameState.Player.Id))
    //            return;

    //        var __game = _gameList.FirstOrDefault(x => x.Player1 == gameState.Player || x.Player2 == gameState.Player);
    //        if (__game == null)
    //            return;

    //        if (__game.Player1 == gameState.Player)
    //            __game.Player1 = null;
    //        if (__game.Player2 == gameState.Player)
    //            __game.Player2 = null;

    //        //Все игроки ушли
    //        if (__game.Player1 == null && __game.Player2 == null)
    //            _gameList.Remove(__game);
    //        else
    //        {
    //            var __opponent = __game.Player1 ?? __game.Player2;
    //            GetGameState(_gameStates[__opponent.Id]);
    //        }
    //        gameState.Player.State = PlayerState.RoomList;
    //        _sessionWithPlayer[gameState.Player.Id].GotoState(gameState.Player.State);
    //    }

    //    public void GetGameState(IGameState gameState)
    //    {
    //        var __game = _gameList.FirstOrDefault(x => x.Player1 == gameState.Player || x.Player2 == gameState.Player);
    //        if (__game == null)
    //            return;

    //        var __gameState = new SimpleGameState
    //            {
    //                FirstPlayer = __game.Player1 == null ? null : __game.Player1.Name,
    //                SecondPlayer = __game.Player2 == null ? null : __game.Player2.Name
    //            };
    //        gameState.SendGameState(__gameState);
    //    }
    //}

    class SimpleGameState
    {
        public string FirstPlayer;
        public string SecondPlayer;
    }

    class SimpleGame
    {
        public IPlayer Player1;
        public IPlayer Player2;
        public RoomSettings Settings;
    }

    class Player
    {
        public readonly string Name;
        public Player(string name)
        {
            Name = name;
        }
    }

    interface ISystemState
    {
        Player Player { get; set; }
        void ShowErrorAsync(string message);

        void SetSessionId(Guid id);
        void FailJoin();
        void GotoState(PlayerState state);
        void GotoWelcomeState();
    }

    enum PlayerState
    {
        RoomList,
        Game
    }


    interface IRoomState
    {
        IPlayer Player { get; }
        void UpdateRoomListAsync(Dictionary<Guid, RoomSettings> settings);
    }

    interface IGameState
    {
        IPlayer Player { get; }
        void SendGameState(SimpleGameState state);
    }



    interface IPlayer
    {
        Guid Id { get; }
        string Name { get; }
        PlayerState State { get; set; }
    }

    class RoomSettings
    {
        public string Text;
        public int Size;
    }

}
