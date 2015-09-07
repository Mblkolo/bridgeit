using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public int bridgeitId;
        public int ownerId;
        public int opponentId;
        /// <summary>Время на ход в секундах</summary>
        public int stepTime;
        public int fieldSize;
        public BridgeitStateDTO state;
    }

    class BridgeitStateDTO
    {
        //Игровое поле, всегда квадратное
        public byte[,] field;
        public int timeout;
        /// <summary>Кто ходит</summary>
        public int activeId;
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
}
