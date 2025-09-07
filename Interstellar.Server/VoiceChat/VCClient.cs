using Interstellar.Server.Services;
using SIPSorcery.Net;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interstellar.Server.VoiceChat;

internal class VCClient
{
    internal record Profile(string PlayerName, byte PlayerId);

    VCClientService service;
    VCRoom myRoom;
    Profile? profile = null;

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
        this.service.SendTracksMask(currentMask);
    }

    /// <summary>
    /// 部屋に自身の音声をブロードキャストします。
    /// </summary>
    /// <param name="durationRtpUnits"></param>
    /// <param name="encodedAudio"></param>
    public void BroadcastAudio(uint durationRtpUnits, byte[] encodedAudio)
    {
        myRoom.Broadcast(ClientId, durationRtpUnits, encodedAudio);
    }

    /// <summary>
    /// このクライアントに音声を送信します。
    /// </summary>
    /// <param name="id"></param>
    /// <param name="durationRtpUnits"></param>
    /// <param name="encodedAudio"></param>
    public void SendAudio(int id, uint durationRtpUnits, byte[] encodedAudio)
    {
        this.service.SendAudio(id, durationRtpUnits, encodedAudio);
    }

    public void UpdateProfile(string playerName, byte playerId)
    {
        this.profile = new Profile(playerName, playerId);
        
    }

    public bool TryGetProfile([MaybeNullWhen(false)]out string playerName, out byte playerId)
    {
        if(this.profile != null)
        {
            playerName = this.profile.PlayerName;
            playerId = this.profile.PlayerId;
            return true;
        }
        playerName = null;
        playerId = 0;
        return false;
    }

    /// <summary>
    /// クライアントとの通信を切断します。
    /// </summary>
    public void Close()
    {
        myRoom.Leave(this);
    }
}
