using Interstellar.Messages;
using Interstellar.Messages.Messages;
using Interstellar.Messages.Variation;
using Interstellar.Server.VoiceChat;
using SIPSorcery.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Interstellar.Server.Services;

internal class VCClientService : WebSocketBehavior, IMessageProcessor
{
    private RTCPeerConnection connection;
    private Dictionary<int, MediaStreamTrack> streamTracks = new(32);
    private Dictionary<int, AudioStream> audioStreams = new(32);
    private VCClient? client = null;
    private bool IsJoined => client != null;

    public VCClientService()
    {
        connection = new(WebSocketHelpers.GetRTCConfiguration());
        connection.OnAudioFrameReceived += frame =>
        {
            var durationRtpUnits = RtpTimestampExtensions.ToRtpUnits(frame.DurationMilliSeconds, AudioHelpers.ClockRate);
            client?.BroadcastAudio(durationRtpUnits, frame.EncodedAudio);
        };

        connection.onicecandidate += (candidate) =>
        {
            Console.WriteLine("Client " + this.ID + " ICE candidate generated.");
            this.Send(MessagePacker.PackMessage(new IceCandMessage(candidate.candidate, candidate.sdpMid, candidate.sdpMLineIndex, candidate.usernameFragment)).ToArray());
        };
    }

    protected override void OnOpen()
    {
        Console.WriteLine("Client " + this.ID + " connected.");
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        if (e.IsBinary)
        {
            try
            {
                MessagePacker.UnpackMessages(e.RawData, this);
            }catch(InvalidDataException ex)
            {
                Console.WriteLine("Error processing message from client " + this.ID + ": " + ex.Message);
            }
        }

    }

    protected override void OnClose(CloseEventArgs e)
    {
        Console.WriteLine("Client " + this.ID + " disconnected.");
        client?.Close();
    }

    int IMessageProcessor.Process(MessageTag tag, ReadOnlySpan<byte> bytes)
    {
        int read = -1;
        switch (tag)
        {
            case MessageTag.Join:
                Console.WriteLine("Client " + this.ID + " requested to join a room.");
                JoinRoom(JoinMessage.DeserializeWithoutTag(bytes, out read));
                break;
            case MessageTag.SdpAnswer:
                Console.WriteLine("Client " + this.ID + " sent SDP answer.");
                AcceptSdpAnswer(SdpAnswerMessage.DeserializeWithoutTag(bytes, out read));
                break;
            case MessageTag.AddIceCand:
                Console.WriteLine("Client " + this.ID + " sent ICE candidate.");
                AddIceCandidate(IceCandMessage.DeserializeWithoutTag(bytes, out read));
                break;
        }
        return read;
    }

    private void JoinRoom(JoinMessage message)
    {
        if (!IsJoined)
        {
            Console.WriteLine("Client " + this.ID + " joined room " + message.RoomCode + " in region " + message.Region);
            VCRoom room = RoomManager.GetRoom(message.Region, message.RoomCode);
            client = room.Join(this);

            //受け取りトラックを追加する。
            var format = AudioHelpers.GetOpusFormat(client.ClientId);
            var stream = new MediaStreamTrack(format, MediaStreamStatusEnum.RecvOnly);
            connection.addTrack(stream);

            this.Send(MessagePacker.PackMessages([new ShareIdMessage(client.ClientId), UpdateTracks(room.CurrentVoiceMask)]).ToArray());
        }
    }

    public void SendUpdate(long mask)
    {
        if (IsJoined)
        {
            this.Send(MessagePacker.PackMessage(UpdateTracks(mask)).ToArray());
        }
    }

    private void AcceptSdpAnswer(SdpAnswerMessage message)
    {
        connection.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = message.Sdp });
    }

    private void AddIceCandidate(IceCandMessage message)
    {
        connection.addIceCandidate(new RTCIceCandidateInit
        {
            candidate = message.Candidate,
            sdpMid = message.SdpMid,
            sdpMLineIndex = (ushort)message.SdpMLineIndex,
            usernameFragment = message.UsernameFragment
        });
    }

    private SdpOfferMessage UpdateTracks(long mask)
    {
        int myId = client!.ClientId;
        for(int i = 0; i < 63; i++)
        {
            if(i == myId) continue;
            bool shouldHave = (mask & (1L << i)) != 0;
            bool have = streamTracks.TryGetValue(i, out var existed);
            if(shouldHave && !have)
            {
                var format = AudioHelpers.GetOpusFormat(i);
                var stream = new MediaStreamTrack(format, MediaStreamStatusEnum.SendOnly);
                streamTracks.Add(i, stream);
                connection.addTrack(stream);
                Console.WriteLine("Added track for client " + i);
            }
            else if(!shouldHave && have)
            {
                connection.removeTrack(existed);
                streamTracks.Remove(i);
                Console.WriteLine("Removed track for client " + i);
            }
        }

        //AudioStreamを更新
        audioStreams.Clear();
        foreach(var audioStream in connection.AudioStreamList) audioStreams[audioStream.GetSendingFormat().ID] = audioStream;
        

        var offer = connection.createOffer(null);
        connection.setLocalDescription(offer).Wait();
        return new SdpOfferMessage(offer.sdp, mask);
    }

    public void SendAudio(int id, uint durationRtpUnits, byte[] encodedAudio)
    {
        if (audioStreams.TryGetValue(id, out var stream))
        {
            stream.SendAudio(durationRtpUnits, encodedAudio);
            Console.WriteLine("Sent audio to client " + this.ID + " from client " + id + " (" + encodedAudio.Length + " bytes)");
        }
    }
}
