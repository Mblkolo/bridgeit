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
        //public string Text;
        public int Size;
    }

}
