using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fleck;

namespace BridgeitServer
{
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
        public Dictionary<int, RoomSettingsDto> settings;

        public RoomSettingsOutboxMessage(string area, string type, IDictionary<int, RoomSettings> rooms)
            : base(area, type, null)
        {
            settings = rooms.ToDictionary(x => x.Key, v => RoomSettingsDto.Convert(v.Value));
        }

        public RoomSettingsOutboxMessage(string area, string type, int id, RoomSettings singleSettings)
            : base(area, type, null)
        {
            settings = new Dictionary<int, RoomSettingsDto> { { id, RoomSettingsDto.Convert(singleSettings) } };
        }
    }

    class BridgeitOutboxMessage : OutboxMessage
    {
        public BridgeitSettingsDTO settings;
        public BridgeitStateDTO state;

        public static BridgeitOutboxMessage Convert(string area, string type, BridgeitRoom room)
        {
            return new BridgeitOutboxMessage
            {
                area = area,
                type = type,
                settings = BridgeitSettingsDTO.Convert(room),
                state = BridgeitStateDTO.Convert(room)
            };
        }
    }

    class BridgeitSettingsDTO
    {
        public int bridgeitId;
        public int ownerId;
        public int opponentId;
        public string ownerName;
        public string opponentName;
        /// <summary>Время на ход в секундах</summary>
        public int stepTime;
        public int fieldSize;

        public static BridgeitSettingsDTO Convert(BridgeitRoom room)
        {
            return new BridgeitSettingsDTO
            {
                bridgeitId = room.Id,
                ownerId = room.OwnerId,
                opponentId = room.OppnentId,
                stepTime = room.StepTime,
                fieldSize = room.FieldSize,
                ownerName = room.OwnerName,
                opponentName = room.OppnentName
            };
        }
    }

    class BridgeitStateDTO
    {
        //Игровое поле, всегда квадратное
        public byte[,] field;
        public int timeout;
        /// <summary>Кто ходит, в случае окончания ид победителя или 0</summary>
        public int activeId;
        public int stepNo;

        //wait, game, completed
        public BridgeitRoomPhase phase;

        public static BridgeitStateDTO Convert(BridgeitRoom room)
        {
            return new BridgeitStateDTO
            {
                activeId = room.ActiveId,
                field = room.Field,
                timeout = room.GetTimeout(),
                stepNo = room.StepNo,
                phase = room.Phase
            };
        }
    }

    class RoomSettingsDto
    {
        public string name;
        public int id;
        public int fieldSize;

        public static RoomSettingsDto Convert(RoomSettings settings)
        {
            return settings == null ? null : new RoomSettingsDto { name = settings.Name, id = settings.Id, fieldSize = settings.Size };
        }

        public static RoomSettings Convert(RoomSettingsDto settings)
        {
            return new RoomSettings { Name = settings.name, Id = settings.id, Size = settings.fieldSize };
        }
    }

    class InboxMessage
    {
        public Guid sessionId;
        public string area;
        public string type;
        public string value;

        public InboxMessage()
        {
        }

        public InboxMessage(Guid session, string type, string value, string area)
        {
            this.sessionId = session;
            this.type = type;
            this.value = value;
            this.area = area;
        }
    }

    class RoomSettingsInboxMessage : InboxMessage
    {
        public int fieldSize;

        public RoomSettingsInboxMessage()
        {
        }

        public RoomSettingsInboxMessage(Guid session, string type, string value, string area, int fieldSize)
            :base(session, type, value, area)
        {
            this.fieldSize = fieldSize;
        }

    }

    class PlayeerActonDTO
    {
        public int x;
        public int y;
        public int stepNo;
    }
}
