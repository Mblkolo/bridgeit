using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BridgeitServer;

namespace BridgeitTest
{
    [TestClass]
    public class BridgeitRoomCheckPathTest
    {
        [TestMethod]
        public void OneCeilExistPath()
        {
            var room = new BridgeitRoom(1, new RoomSettings { Id = 1, Name = "Name", Size = 1 }, 1, 2);
            room.Field[0, 0] = 1;

            var pathExist = BridgeitRoom.CheckHorisonatalPath(room);

            Assert.IsTrue(pathExist);
            Assert.AreEqual(room.Field[0, 0], 1);
        }

        [TestMethod]
        public void OneCeilNotExistPath()
        {
            var room = new BridgeitRoom(1, new RoomSettings { Id = 1, Name = "Name", Size = 1 }, 1, 2);

            var pathExist = BridgeitRoom.CheckHorisonatalPath(room);

            Assert.IsFalse(pathExist);
        }

        [TestMethod]
        public void TwoCeilExistPath()
        {
            var room = new BridgeitRoom(1, new RoomSettings { Id = 1, Name = "Name", Size = 2 }, 1, 2);
            room.Field[2, 0] = 1;
            room.Field[1, 1] = 1;
            room.Field[0, 2] = 1;

            var pathExist = BridgeitRoom.CheckHorisonatalPath(room);

            Assert.IsTrue(pathExist);
        }

        [TestMethod]
        public void TwoCeilNotExistPath()
        {
            var room = new BridgeitRoom(1, new RoomSettings { Id = 1, Name = "Name", Size = 2 }, 1, 2);
            room.Field[0, 0] = 1;
            room.Field[2, 2] = 1;

            var pathExist = BridgeitRoom.CheckHorisonatalPath(room);

            Assert.IsFalse(pathExist);
        }

        [TestMethod]
        public void BigSizePath()
        {
            var room = new BridgeitRoom(1, new RoomSettings { Id = 1, Name = "Name", Size = 20 }, 1, 2);
            for (int y = 0; y < room.FieldSize * 2 - 1; ++y)
            {
                for (int x = 0; x < room.FieldSize * 2 - 1; ++x)
                {
                    if ((x + y) % 2 != 0)
                        continue;

                    room.Field[y, x] = 1;
                }
            }

            for(int i=0; i<1000; ++i)
                Assert.IsTrue(BridgeitRoom.CheckHorisonatalPath(room)); 
        }
    }
}
