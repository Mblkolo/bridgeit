﻿using BridgeitServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BridgeitTest
{
    [TestClass]
    public class GameServerRoomsAreaTest
    {
        class FakeConnectionProxy : IConnectionProxy
        {
            public FakeConnectionProxy()
            {
                Id = Guid.NewGuid();
            }

            public Guid Id { get; private set; }

            public PlayerSession Session { get; set; }

            public void Send(string message)
            {
            }

            public void Send(OutboxMessage message)
            {
            }
        }

        [TestMethod]
        public void RemoveRoomSettingsAfterJoin()
        {
            var __server = new GameServer();
            int ownerPlayerId = 1;

            var __ownerConnection = new FakeConnectionProxy { Session = new PlayerSession { Area = "rooms", PlayerId = ownerPlayerId, PlayerName = "owner" } };
            var __oppenentConnection = new FakeConnectionProxy { Session = new PlayerSession { Area = "rooms", PlayerId = 2, PlayerName = "oppenent" } };
            __server.Rep.SessionConnections.Add(__ownerConnection.Id, __ownerConnection);
            __server.Rep.SessionConnections.Add(__oppenentConnection.Id, __oppenentConnection);
            __server.Rep.RoomsSettings.Add(1, new RoomSettings { Id = 1, Name = "O?", Size = 10 });

            var __inboxMessage = new InboxMessage(__oppenentConnection.Id, "joinToRoom", ownerPlayerId.ToString(), "rooms");
            __server.RoomsArea(__oppenentConnection.Id, "TODO заменить на сериализацию __inboxMessage", __inboxMessage);

            Assert.AreEqual(__server.Rep.RoomsSettings.Count, 0);
            Assert.AreEqual(__server.Rep.SessionConnections[__ownerConnection.Id].Session.Area, "bridgeit");
            Assert.AreEqual(__server.Rep.SessionConnections[__oppenentConnection.Id].Session.Area, "bridgeit");
            Assert.AreEqual(__server.Rep.Rooms.Count, 1);
            Assert.AreEqual(__server.Rep.Rooms.Values.First().FieldSize, 10);
        }
    }
}
