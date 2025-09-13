using Concentus;
using Interstellar.AudioInput;
using Interstellar.Messages;
using Interstellar.Messages.Messages;
using Interstellar.Messages.Variation;
using Interstellar.NAudio.Provider;
using Interstellar.Routing;
using Interstellar.Routing.Router;
using Interstellar.VoiceChat;
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
    internal const string WaveOutDeviceName = "Arctis Nova 3P Wireless"; //使用する再生デバイスの名前を指定してください。

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
        VCRoom room = new(source, "ABCDE", "testRegion", "ws://localhost:8000/vc", 0, "Dolly");
        room.SetMicrophone(WaveInDeviceId);
        room.SetSpeaker(WaveOutDeviceName);

        Console.ReadKey();
    }
}