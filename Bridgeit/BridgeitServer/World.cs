using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fleck;

namespace BridgeitServer
{
    class Player
    {
        public readonly string Name;
        public readonly int Id;
        public Player(string name, int id)
        {
            Name = name;
            Id = id;
        }
    }

    class RoomSettings
    {
        public int Id;
        public string Name;
        public int Size;
    }

    internal class GameServer
    {
        public readonly SingleThreadWorker<Action> SingleThread;

        public GameServer()
        {
            SingleThread = new SingleThreadWorker<Action>(new SimpleSingleThread());
            SingleThread.Start();
        }
        
        public void ConfigureConnection(IWebSocketConnection connection)
        {
            var __proxy = new ConnectionProxy(connection, SingleThread);
            var __connectionHandler = new PlayerConnectionHandler(__proxy);
            __proxy.Handler = __connectionHandler;
        }
    }


    class PlayerConnectionHandler : IConnectionHandler
    {
        private ConnectionProxy _proxy;
        
        
        public enum PossibleState { None, Anonim, Connected, Close }

        public PossibleState State;

        public PlayerConnectionHandler(ConnectionProxy proxy)
        {
            _proxy = proxy;
        }

        #region обрабтка сообщений

        public void OnError(Exception e)
        {

        }

        public void OnClose()
        {

        }

        public void OnOpen()
        {

        }

        public void OnMessage(string message)
        {

        }

        #endregion


    }
}
