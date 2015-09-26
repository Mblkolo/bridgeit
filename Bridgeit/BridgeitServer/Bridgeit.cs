using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fleck;

namespace BridgeitServer
{
    public enum BridgeitRoomPhase
    {
        wait,
        game,
        completed
    }

    internal class BridgeitRoom
    {
        public readonly int Id;
        public readonly int OwnerId;
        public readonly int OppnentId;

        /// <summary>Время на ход</summary>
        public readonly int WaitTime = 5;
        public readonly int StepTime;
        public readonly int FieldSize;

        public BridgeitRoomPhase Phase;

        //Кто сейчас ходит
        public int ActiveId;
        //Номер хода
        public int StepNo;

        //Время последнего действия
        public DateTime LastActive;
        public readonly byte[,] Field;


        public BridgeitRoom(int roomId, RoomSettings settings, int ownerId, int oppnentId)
        {
            Id = roomId;
            OwnerId = ownerId;
            OppnentId = oppnentId;
            StepTime = 30;
            FieldSize = settings.Size;
            Field = new byte[FieldSize * 2 - 1, FieldSize * 2 - 1];
            LastActive = DateTime.Now;

            var r = new Random();
            for (int y = 0; y < FieldSize * 2 - 1; ++y)
            {
                for (int x = 0; x < FieldSize * 2 - 1; ++x)
                {
                    if ((x + y) % 2 != 0)
                        continue;

                    //Field[y, x] = (byte)r.Next(3);
                }
            }
        }

        /// <summary>
        /// Миллисекунд до таймаута
        /// </summary>
        /// <returns></returns>
        public int GetTimeout()
        {
            var timeout = 0;
            if (Phase == BridgeitRoomPhase.wait)
                timeout = WaitTime;
            if (Phase == BridgeitRoomPhase.game)
                timeout = StepTime;

            var result = Math.Max(0, timeout * 1000 - (int)Math.Round((DateTime.Now - LastActive).TotalMilliseconds));
            return result;
        }

        private Timer timer;
        public void Timeout(Action timeoutAction)
        {
            if (timer != null)
                timer.Dispose();

            timer = new Timer(o => timeoutAction(), null, GetTimeout(), 0);
        }

        public static bool CheckHorizontalPath(BridgeitRoom room)
        {
            int maxSize = room.FieldSize * 2 - 1;
            var fiels = room.Field;
            Func<int, int, byte, byte, bool> trySet = (y, x, oldV, newV) => {
                if(fiels[y,x] == oldV)
                {
                    fiels[y,x] = newV;
                    return true;
                }
                return false;
            };
            return CheckPath(maxSize, trySet, 1);
        }

        public static bool CheckVerticalPath(BridgeitRoom room)
        {
            int maxSize = room.FieldSize * 2 - 1;
            var fiels = room.Field;
            Func<int, int, byte, byte, bool> trySet = (y, x, oldV, newV) =>
            {
                if (fiels[y, x] == oldV)
                {
                    fiels[y, x] = newV;
                    return true;
                }
                return false;
            };
            return CheckPath(maxSize, trySet, 2);
        }

        //Наиваная реализация, без оптимизаций
        public static bool CheckPath(int maxSize, Func<int, int, byte, byte, bool> trySetVaue, byte value)
        {
            var candidats = new Stack<Ceil>();
            byte antivalue = (byte)-value;
            for (int y = 0; y < maxSize; y += 2)
            {
                if (trySetVaue(y, 0, value, antivalue))
                {
                    candidats.Push(new Ceil(y, 0));
                }
            }

            var directions = new Ceil[8];
            while (candidats.Count > 0)
            {
                var ceil = candidats.Pop();
                //проверки в 8 сторон, это всегда так _весело_
                directions[0]= new Ceil(ceil.Y + 0, ceil.X - 2);
                directions[1]= new Ceil(ceil.Y + 1, ceil.X - 1);
                directions[2]= new Ceil(ceil.Y - 1, ceil.X - 1);
                directions[3]= new Ceil(ceil.Y + 2, ceil.X + 0);
                directions[4]= new Ceil(ceil.Y - 2, ceil.X + 0);
                directions[5]= new Ceil(ceil.Y + 1, ceil.X + 1);
                directions[6]= new Ceil(ceil.Y - 1, ceil.X + 1);
                directions[7]= new Ceil(ceil.Y + 0, ceil.X + 2);

                for (int i = 0; i < directions.Length; ++i)
                {
                    var dir = directions[i];
                    if (dir.X >= 0 && dir.X < maxSize && dir.Y >= 0 && dir.Y < maxSize)
                        if(trySetVaue(dir.Y, dir.X, value, antivalue))
                        {
                            candidats.Push(dir);
                            if (dir.X == maxSize - 1)
                                candidats.Clear();
                        }
                }
            }

            bool isSuccess = false;
            for (int y = 0; y < maxSize; ++y)
                for (int x = 0; x < maxSize; ++x)
                    if (trySetVaue(y, x, antivalue, value))
                        isSuccess |= (x == maxSize - 1);

            return isSuccess;
        }
    }

    public struct Ceil
    {
        public readonly int X;
        public readonly int Y;

        public Ceil(int y, int x)
        {
            Y = y;
            X = x;
        }
    }
}
