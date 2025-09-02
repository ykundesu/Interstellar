using Interstellar.API.VoiceChat;
using Interstellar.API.VoiceChat.Strategy;
using Interstellar.Network;
using SIPSorcery.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interstellar.VoiceChat;

internal class VCRoomImpl<Player> : IVCRoom<Player>
{
    private RoomConnection connection;
    private IRoomStrategy<Player> roomStrategy;
    public VCRoomImpl(string roomCode, string region, string url, IRoomStrategy<Player> roomStrategy)
    {
        //this.connection = new RoomConnection(roomCode, region, url);
        this.roomStrategy = roomStrategy;
    }

    IEnumerable<IVoiceStream> IVCRoom<Player>.AllVoiceStreams => throw new NotImplementedException();

    void IVCRoom<Player>.Close()
    {
        throw new NotImplementedException();
    }

    bool IVCRoom<Player>.TryGetPlayer(Predicate<Player> predicate, out IVCClient<Player> player)
    {
        throw new NotImplementedException();
    }
}
