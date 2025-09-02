using Concentus;
using Interstellar.AudioInput;
using Interstellar.AudioPlayer.Provider;
using Interstellar.Messages;
using Interstellar.Messages.Messages;
using Interstellar.Messages.Variation;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using SIPSorcery.Net;
using System.Text;
using WebSocketSharp;

namespace Sandbox;

internal class Program
{
    internal const int WaveInDeviceId = 3; //使用する録音デバイスのIDを指定してください。
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

        WebSocket ws = new("ws://localhost:8000/vc");
        RTCPeerConnection pc = null!;
        MessageProcessor processor = null;
        ws.OnOpen += (sender, e) =>
        {
            float[] buffer = new float[48000];
            Dictionary<int, (IOpusDecoder, BufferedSampleProvider, Action)> decoders = new(32);
            void DecodeAndAddSample(int id, byte[] encodedAudio)
            {
                try
                {
                    if (!decoders.ContainsKey(id))
                    {
                        var provider = new BufferedSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 1));
                        var wave = provider.ToWaveProvider();
                        var device = new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).FirstOrDefault(device => device.FriendlyName == WaveOutDeviceName) ?? new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                        Console.WriteLine("Using output device: " + device.FriendlyName);
                        var waveOut = new WasapiOut(device, AudioClientShareMode.Shared, false, 200);
                        waveOut.Init(wave);
                        decoders[id] = (AudioHelpers.GetOpusDecoder(), provider, () =>
                        {
                            try
                            {
                                if (waveOut.PlaybackState != PlaybackState.Playing)
                                {
                                    waveOut.Play();
                                    Console.WriteLine("Started playback on device: " + device.FriendlyName);
                                }
                            }
                            catch (Exception excep)
                            {
                                Console.WriteLine(excep.ToString());
                            }
                        }
                        );
                    }
                    var tuple = decoders[id];
                    int length = tuple.Item1.Decode(encodedAudio, buffer, buffer.Length);
                    tuple.Item2.AddSamples(buffer, 0, length);
                    tuple.Item3.Invoke();
                }catch(Exception excep)
                {
                    Console.WriteLine(excep.ToString());
                }
            }

            pc = new(WebSocketHelpers.GetRTCConfiguration());
            pc.OnAudioFrameReceived += frame =>
            {
                Console.WriteLine("Received audio frame.");
                DecodeAndAddSample(frame.AudioFormat.FormatID, frame.EncodedAudio);
                Console.WriteLine("Pushed audio frame.");

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
                var source = new MicrophoneAudioSource(shareId.Id, Program.WaveInDeviceId); 
                source.BindToConnection(stream);
                break;
            case MessageTag.SdpOffer:
                Console.WriteLine("Processing SDP Offer message");
                var offer = SdpOfferMessage.DeserializeWithoutTag(bytes, out read);

                //トラックの更新
                foreach (var v in streams.Values) connection.removeTrack(v);
                for (int i = 0; i < 63; i++)
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