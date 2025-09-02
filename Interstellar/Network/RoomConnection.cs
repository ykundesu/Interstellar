using Interstellar.API.VoiceChat;
using Interstellar.AudioInput;
using Interstellar.Messages;
using Interstellar.Messages.Messages;
using Interstellar.Messages.Variation;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;

namespace Interstellar.Network;

internal class RoomConnection
{
    private readonly string roomCode;
    private readonly string region;
    private readonly WebSocket socket;
    private readonly RTCPeerConnection connection;
    private MicrophoneAudioSource microphone;

    public RoomConnection(string roomCode, string region, string url, byte localPlayerId, string localPlayerName)
    {
        this.roomCode = roomCode;
        this.region = region;
        this.socket = new WebSocket(url);
        this.connection = new RTCPeerConnection(WebSocketHelpers.GetRTCConfiguration());
        Connect(localPlayerId, localPlayerName);
    }

    private void Connect(byte localPlayerId, string localPlayerName)
    {
        this.socket.OnOpen += (sender, e) =>
        {
            this.socket.SendMessages(new JoinMessage(roomCode, region), new ProfileMessage(localPlayerName, localPlayerId));
        };
        this.socket.Connect();
    }

    private void SendMessage(string tag, object message)
    {
        this.socket.Send(tag + System.Text.Json.JsonSerializer.Serialize(message));
    }

}
