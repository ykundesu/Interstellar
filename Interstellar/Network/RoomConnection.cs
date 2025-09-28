﻿using Concentus;
using Interstellar.AudioInput;
using Interstellar.Messages;
using Interstellar.Messages.Messages;
using Interstellar.Messages.Variation;
using SIPSorcery.Net;
using WebSocketSharp;

namespace Interstellar.Network;

internal interface IConnectionContext
{
    /// <summary>
    /// 音声フレームを受け取ったときに呼び出されます。
    /// </summary>
    /// <param name="clientId">音声フレームの送り主。</param>
    /// <param name="samples">音声データを含む配列。このメソッドの呼び出し後、配列は再利用されます。別の用途に使わないでください。</param>
    /// <param name="length">音声データの長さ。</param>
    void OnAudioFrameReceived(int clientId, float[] samples, int length);

    /// <summary>
    /// クライアントが切断したときに呼び出されます。
    /// </summary>
    /// <param name="clientId"></param>
    void OnClientDisconnected(int clientId); 

    /// <summary>
    /// クライアントのプロフィールが更新されたときに呼び出されます。
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="playerName"></param>
    /// <param name="playerId"></param>
    void OnClientProfileUpdated(int clientId, string playerName, byte playerId);
}

/// <summary>
/// サーバーと接続し、音声や情報の送受信を担います。
/// </summary>
internal class RoomConnection : IMessageProcessor
{
    private readonly string roomCode;
    private readonly string region;
    private readonly WebSocket socket;
    private RTCPeerConnection? connection = null;
    private MicrophoneAudioSource? microphone;
    private ProfileMessage? profileMessage = null;
    private int? myClientId = null;

    private AudioStream? localAudioStream;

    IConnectionContext context;
    public RoomConnection(IConnectionContext context, string roomCode, string region, string url)
    {
        this.context = context;
        this.roomCode = roomCode;
        this.region = region;

        this.socket = new WebSocket(url);
        this.socket.OnMessage += (sender, e) =>
        {
            if (e.IsBinary) MessagePacker.UnpackMessages(e.RawData, this);
        };
        Connect();
    }

    /// <summary>
    /// ゲーム内のプレイヤー情報を更新します。
    /// </summary>
    /// <param name="playerName"></param>
    /// <param name="playerId"></param>
    public void UpdateProfile(string playerName, byte playerId)
    {
        var message = new ProfileMessage(playerName, playerId);
        profileMessage = message;
        TrySendProfile();
    }

    private void TrySendProfile()
    {
        if (profileMessage != null && socket.IsAlive)
        {
            this.socket.SendMessage(profileMessage);
            profileMessage = null;
        }
    }

    private void Connect()
    {
        this.socket.OnOpen += (sender, e) =>
        {
            SetUpRTCConnection();
            this.socket.SendMessage(new JoinMessage(this.roomCode, this.region));
        };
        this.socket.Connect();
    }

    private void SetUpRTCConnection()
    {
        //オーディオフレームを受け渡す関数
        float[] buffer = new float[2048];
        Dictionary<int, IOpusDecoder> decoders = new(64);
        void DecodeAndAddSample(int id, byte[] encodedAudio)
        {
            try
            {
                if (!decoders.ContainsKey(id)) decoders[id] = AudioHelpers.GetOpusDecoder();

                var decoder = decoders[id];
                int length = decoder.Decode(encodedAudio, buffer, buffer.Length);
                context.OnAudioFrameReceived(id, buffer, length);
            }
            catch (Exception excep)
            {
                Console.WriteLine(excep.ToString());
            }
        }


        this.socket.SendMessages(new JoinMessage(roomCode, region));
        TrySendProfile();
        this.connection = new RTCPeerConnection(WebSocketHelpers.GetRTCConfiguration());
        this.connection.OnAudioFrameReceived += frame =>
        {
            DecodeAndAddSample(frame.AudioFormat.FormatID, frame.EncodedAudio);
        };
        this.connection.onicecandidate += (candidate) =>
        {
            this.socket.SendMessage(new IceCandMessage(candidate.candidate, candidate.sdpMid, candidate.sdpMLineIndex, candidate.usernameFragment));
        };
    }

    public void SetMicrophone(int deviceId)
    {
        if (this.microphone != null) this.microphone.Close();
        this.microphone = new MicrophoneAudioSource(deviceId);
        ReflectIdToMicrophone();
    }

    private void ReflectIdToMicrophone()
    {
        if(localAudioStream != null) this.microphone?.BindToConnection(localAudioStream);
    }

    MediaStreamTrack[] lastTracks = [];

    int IMessageProcessor.Process(MessageTag tag, ReadOnlySpan<byte> bytes)
    {
        int read = -1;
        switch (tag)
        {
            case MessageTag.ShareId:
                OnReceiveMyClientId(ShareIdMessage.DeserializeWithoutTag(bytes, out read));
                break;
            case MessageTag.SdpOffer:
                OnReceiveSdpOffer(SdpOfferMessage.DeserializeWithoutTag(bytes, out read));
                break;
            case MessageTag.AddIceCand:
                OnReceiveIceCandMessage(IceCandMessage.DeserializeWithoutTag(bytes, out read));
                break;
            case MessageTag.ShareProfile:
                var profile = ShareProfileMessage.DeserializeWithoutTag(bytes, out read);
                context.OnClientProfileUpdated(profile.PlayerId, profile.PlayerName, profile.PlayerId);
                break;
        }
        return read;
    }

    private void OnReceiveMyClientId(ShareIdMessage message)
    {
        int id = message.Id;
        myClientId = id;
        var localTrack = new MediaStreamTrack(AudioHelpers.GetOpusFormat(id), MediaStreamStatusEnum.SendOnly);
        connection!.addTrack(localTrack);
        localAudioStream = connection.AudioStreamList.Find(a => a.GetSendingFormat().ID == id);
        
        ReflectIdToMicrophone();
    }

    private void OnReceiveSdpOffer(SdpOfferMessage message)
    {
        //トラックの削除と更新
        foreach (var track in lastTracks) connection!.removeTrack(track);
        List<MediaStreamTrack> tracks = [];
        long mask = message.Mask;
        for (int i = 0; i < AudioHelpers.MaxTracks; i++)
        {
            if ((mask & (1L << i)) == 0) continue;

            var format = AudioHelpers.GetOpusFormat(i);
            var track = new MediaStreamTrack(format, MediaStreamStatusEnum.RecvOnly);
            connection!.addTrack(track);
            tracks.Add(track);
        }
        lastTracks = tracks.ToArray();

        //SDPの処理
        connection!.setRemoteDescription(new RTCSessionDescriptionInit { sdp = message.Sdp, type = RTCSdpType.offer });
        var answer = connection.createAnswer(null);
        connection.setLocalDescription(answer).Wait();
        socket.SendMessage(new SdpAnswerMessage(answer.sdp));
    }

    private void OnReceiveIceCandMessage(IceCandMessage message)
    {
        connection!.addIceCandidate(new RTCIceCandidateInit
        {
            candidate = message.Candidate,
            sdpMid = message.SdpMid,
            sdpMLineIndex = (ushort)message.SdpMLineIndex,
            usernameFragment = message.UsernameFragment
        });
    }
}
