using Interstellar.Server.Services;
using SIPSorcery.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interstellar.Server.VoiceChat;

internal class VCClient
{
    VCClientService service;
    VCRoom myRoom;
    public byte ClientId { get; }

    public VCClient(VCClientService service, byte clientId, VCRoom room)
    {
        this.service = service;
        this.ClientId = clientId;
        this.myRoom = room;
    }

    /// <summary>
    /// 自分以外の誰かが入退室したときに呼び出されます。
    /// </summary>
    /// <param name="currentMask"></param>
    public void OnJoinOrLeaveAnyone(long currentMask) {
        this.service.SendUpdate(currentMask);
    }

    public void BroadcastAudio(uint durationRtpUnits, byte[] encodedAudio)
    {
        myRoom.Broadcast(ClientId, durationRtpUnits, encodedAudio);
    }

    public void SendAudio(int id, uint durationRtpUnits, byte[] encodedAudio)
    {
        this.service.SendAudio(id, durationRtpUnits, encodedAudio);
    }

    public void Close()
    {
        myRoom.Leave(this);
    }
}
