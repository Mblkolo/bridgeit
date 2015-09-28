using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BridgeitServer;

namespace BridgeitTest
{
    [TestClass]
    public class BridgeitRoomCheckPathTest
    {
        PlayerSession player1;
        PlayerSession player2;

        [TestInitialize]
        public void SetUp()
        {
            player1 = new PlayerSession { Area = "bridgeit", PlayerId = 1, PlayerName = "owner" };
            player2 = new PlayerSession { Area = "bridgeit", PlayerId = 2, PlayerName = "oppenent" };
        }

        [TestMethod]
        public void OneCeilNotExistPath()
        {
            var room = new BridgeitRoom(1, new RoomSettings { Id = 1, Name = "Name", Size = 1 }, player1, player2);

            var pathExist = BridgeitRoom.CheckHorizontalPath(room);

            Assert.IsFalse(pathExist);
        }

        [TestMethod]
        public void OneCeilExistHorizontalPath()
        {
            var room = new BridgeitRoom(1, new RoomSettings { Id = 1, Name = "Name", Size = 1 }, player1, player2);
            room.Field[0, 0] = 1;

            var pathExist = BridgeitRoom.CheckHorizontalPath(room);

            Assert.IsTrue(pathExist);
            Assert.AreEqual(room.Field[0, 0], 1);
        }

        [TestMethod]
        public void OneCeilExistVerticalPath()
        {
            var room = new BridgeitRoom(1, new RoomSettings { Id = 1, Name = "Name", Size = 1 }, player1, player2);
            room.Field[0, 0] = 2;

            var pathExist = BridgeitRoom.CheckVerticalPath(room);

            Assert.IsTrue(pathExist);
            Assert.AreEqual(room.Field[0, 0], 2);
        }

        [TestMethod]
        public void TwoCeilNotExistPath()
        {
            var room = new BridgeitRoom(1, new RoomSettings { Id = 1, Name = "Name", Size = 2 }, player1, player2);
            room.Field[0, 0] = 1;
            room.Field[2, 2] = 1;
            room.Field[0, 2] = 2;
            room.Field[2, 0] = 2;

            var hPathExist = BridgeitRoom.CheckHorizontalPath(room);
            var vPathExist = BridgeitRoom.CheckVerticalPath(room);

            Assert.IsFalse(hPathExist);
            Assert.IsFalse(vPathExist);
            Assert.AreEqual(room.Field[0, 0], 1);
            Assert.AreEqual(room.Field[2, 2], 1);
            Assert.AreEqual(room.Field[0, 2], 2);
            Assert.AreEqual(room.Field[2, 0], 2);
        }

        [TestMethod]
        public void TwoCeilHorizontalExistPath()
        {
            var room = new BridgeitRoom(1, new RoomSettings { Id = 1, Name = "Name", Size = 2 }, player1, player2);
            room.Field[2, 0] = 1;
            room.Field[1, 1] = 1;
            room.Field[0, 2] = 1;

            var pathExist = BridgeitRoom.CheckHorizontalPath(room);

            Assert.IsTrue(pathExist);
            Assert.AreEqual(room.Field[2, 0], 1);
            Assert.AreEqual(room.Field[1, 1], 1);
            Assert.AreEqual(room.Field[0, 2], 1);
        }

        [TestMethod]
        public void TwoCeilVerticalExistPath()
        {
            var room = new BridgeitRoom(1, new RoomSettings { Id = 1, Name = "Name", Size = 2 }, player1, player2);
            room.Field[2, 0] = 2;
            room.Field[1, 1] = 2;
            room.Field[0, 2] = 2;

            var pathExist = BridgeitRoom.CheckVerticalPath(room);

            Assert.IsTrue(pathExist);
            Assert.AreEqual(room.Field[2, 0], 2);
            Assert.AreEqual(room.Field[1, 1], 2);
            Assert.AreEqual(room.Field[0, 2], 2);
        }

        [TestMethod]
        public void BigSizePath()
        {
            var room = new BridgeitRoom(1, new RoomSettings { Id = 1, Name = "Name", Size = 20 }, player1, player2);
            for (int y = 0; y < room.FieldSize * 2 - 1; ++y)
            {
                for (int x = 0; x < room.FieldSize * 2 - 1; ++x)
                {
                    if ((x + y) % 2 != 0)
                        continue;

                    room.Field[y, x] = 1;
                }
            }

            for (int i = 0; i < 1000; ++i)
                Assert.IsTrue(BridgeitRoom.CheckHorizontalPath(room));
        }
    }
}
