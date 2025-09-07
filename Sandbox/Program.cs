using Concentus;
using Interstellar.AudioInput;
using Interstellar.Messages;
using Interstellar.Messages.Messages;
using Interstellar.Messages.Variation;
using Interstellar.NAudio.Provider;
using Interstellar.Routing;
using Interstellar.Routing.Router;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using SIPSorcery.Net;
using System.Text;
using WebSocketSharp;

namespace Sandbox;

internal class Program
{
    internal const int WaveInDeviceId = 2; //使用する録音デバイスのIDを指定してください。
    internal const string WaveOutDeviceName = ""; //使用する再生デバイスの名前を指定してください。

    static void PrintWaveInDevices()
    {
        var count = WaveInEvent.DeviceCount;
        for (int i = 0; i < count; i++)
        {
            Console.WriteLine("WaveIn-" + i + ": " + (WaveInEvent.GetCapabilities(i).ProductName));
        }
    }

    static void PrintWaveOutDevices()
    {
        int num = 0;
        foreach (var device in new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            Console.WriteLine("WaveOut-" + num + ": " + device.DeviceFriendlyName);
        }
    }

    static void Main(string[] args)
    {
        PrintWaveInDevices();
        PrintWaveOutDevices();

        SimpleRouter source = new();
        source.Connect(new SimpleEndPoint());
        AudioManager manager = new(source);
        manager.Start(WaveOutDeviceName);
        
        WebSocket ws = new("ws://localhost:8000/vc");
        RTCPeerConnection pc = null!;
        MessageProcessor processor = null;
        ws.OnOpen += (sender, e) =>
        {
            Dictionary<int, AudioRoutingInstance> decoders = new(32);


            //var decoder = BiQuadFilter.LowPassFilter(48000, 1600, 0.5f);

            void DecodeAndAddSample(int id, byte[] encodedAudio)
            {
                try
                {
                    if (!decoders.TryGetValue(id, out var instance))
                    {
                        instance = manager.Generate(id);
                        decoders[id] = instance;
                    }
                    instance.AddSamples(encodedAudio, 0, encodedAudio.Length);
                }catch(Exception excep)
                {
                    Console.WriteLine(excep.ToString());
                }
            }

            pc = new(WebSocketHelpers.GetRTCConfiguration());
            pc.OnAudioFrameReceived += frame =>
            {
                DecodeAndAddSample(frame.AudioFormat.FormatID, frame.EncodedAudio);
            };
            pc.OnRtpPacketReceived += (_, mediaType, packet) =>
            {
                Console.WriteLine("Receive rtp packet: " + mediaType);
            };
            pc.onicecandidate += (candidate) =>
            {
                Console.WriteLine("ICE candidate generated.");
                ws.SendMessage(new IceCandMessage(candidate.candidate, candidate.sdpMid, candidate.sdpMLineIndex, candidate.usernameFragment));
            };
            processor = new(pc, ws);

            Console.WriteLine("Connected to server");
            ws.SendMessage(new JoinMessage("ABCDE", "ievji3"));
        };
        ws.OnMessage += (sender, e) =>
        {
            MessagePacker.UnpackMessages(e.RawData, processor);
        };

        
        ws.Connect();

        Console.ReadKey();
    }
}

internal class MessageProcessor : IMessageProcessor
{
    private readonly RTCPeerConnection connection;
    private readonly WebSocket webSocket;
    private readonly Dictionary<int, MediaStreamTrack> streams = new(32);

    public MessageProcessor(RTCPeerConnection connection, WebSocket webSocket)
    {
        this.connection = connection;
        this.webSocket = webSocket;
    }
    int IMessageProcessor.Process(MessageTag tag, ReadOnlySpan<byte> bytes)
    {
        int read = -1;
        switch (tag)
        {
            case MessageTag.ShareId:
                var shareId = ShareIdMessage.DeserializeWithoutTag(bytes, out read);
                Console.WriteLine("Received Share ID: " + shareId.Id);
                var myTrack = new MediaStreamTrack(AudioHelpers.GetOpusFormat(shareId.Id), MediaStreamStatusEnum.SendOnly);
                connection.addTrack(myTrack);
                var stream = connection.AudioStreamList.Find(a => a.GetSendingFormat().ID == shareId.Id);
                var source = new MicrophoneAudioSource(Program.WaveInDeviceId); 
                source.BindToConnection(stream);
                break;
            case MessageTag.SdpOffer:
                Console.WriteLine("Processing SDP Offer message");
                var offer = SdpOfferMessage.DeserializeWithoutTag(bytes, out read);

                //トラックの更新
                foreach (var v in streams.Values) connection.removeTrack(v);
                for (int i = 0; i < AudioHelpers.MaxTracks; i++)
                {
                    if ((offer.Mask & (1L << i)) != 0)
                    {
                        var format = AudioHelpers.GetOpusFormat(i);
                        var track = new MediaStreamTrack(format, MediaStreamStatusEnum.RecvOnly);
                        connection.addTrack(track);
                        Console.WriteLine("Added track: " + i);
                    }
                }

                connection.setRemoteDescription(new RTCSessionDescriptionInit { sdp = offer.Sdp, type = RTCSdpType.offer });
                var answer = connection.createAnswer(null);
                connection.setLocalDescription(answer).Wait();
                webSocket.SendMessage(new SdpAnswerMessage(answer.sdp));
                Console.WriteLine("Sent SDP Answer message");
                break;
            case MessageTag.AddIceCand:
                Console.WriteLine("Processing ICE Candidate message");
                var candidate = IceCandMessage.DeserializeWithoutTag(bytes, out read);
                connection.addIceCandidate(new RTCIceCandidateInit()
                {
                    candidate = candidate.Candidate,
                    sdpMid = candidate.SdpMid,
                    sdpMLineIndex = (ushort)candidate.SdpMLineIndex,
                    usernameFragment = candidate.UsernameFragment
                });
                break;
        }
        return read;
    }

}