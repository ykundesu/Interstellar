using Interstellar.Server.Services;
using SIPSorcery.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interstellar.Server.VoiceChat;

internal class VCRoom
{
    string myKey;
    Dictionary<byte, VCClient> fastClients = new();

    public VCRoom(string key)
    {
        myKey = key;
    }

    private byte AvailableId()
    {
        byte id = 0;
        while(fastClients.ContainsKey(id)) id++;
        return id;
    }

    public VCClient Join(VCClientService service)
    {
        var client = new VCClient(service, AvailableId(), this);
        fastClients.Add(client.ClientId, client);
        
        //入室を通知する。
        long currentMask = CurrentVoiceMask;
        foreach (var c in fastClients.Values)
        {
            if(c.ClientId != client.ClientId) c.OnJoinOrLeaveAnyone(currentMask);
        }

        return client;
    }

    public void Leave(VCClient client)
    {
        if(fastClients.Remove(client.ClientId))
        {
            //退室を通知する。
            long currentMask = CurrentVoiceMask;
            foreach (var c in fastClients.Values)
            {
                c.OnJoinOrLeaveAnyone(currentMask);
                c.NoticeLeaveClient(client.ClientId);
            }
        }

        if(fastClients.Count == 0) RoomManager.RemoveRoom(myKey);
    }

    public long CurrentVoiceMask { get
        {
            long mask = 0;
            foreach(var client in fastClients.Values)
            {
                mask |= (1L << client.ClientId);
            }
            return mask;
        } 
    }

    public void Broadcast(byte id, uint durationRtpUnits, byte[] encodedAudio)
    {
        foreach(var client in fastClients.Values)
        {
            if(client.ClientId != id) client.SendAudio(id, durationRtpUnits, encodedAudio);
        }
    }

    public void BroadcastRawMessage(byte id, byte[] rawMessage)
    {
        foreach (var client in fastClients.Values)
        {
            if (client.ClientId != id) client.Send(rawMessage);
        }
    }

    public void BroadcastProfile(byte id, string playerName, byte playerId)
    {
        foreach (var client in fastClients.Values)
        {
            if (client.ClientId != id) client.SendProfile(id, playerName, playerId);
        }
    }

    public IEnumerable<VCClient> Clients => fastClients.Values;
}
