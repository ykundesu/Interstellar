using Concentus;
using Interstellar.Messages;
using Interstellar.Network;
using NAudio.Wave;
using Org.BouncyCastle.Utilities.Encoders;
using SIPSorcery.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interstellar.VoiceChat;

internal interface IMicrophoneContext
{
    /// <summary>
    /// オーディオデータを送信する際に呼び出されます。
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="offset"></param>
    /// <param name="count"></param>
    //void OnAudioSent(float[] buffer, int offset, int count);

    void SendAudio(float[] samples, int samplesLength, double samplesMilliseconds);
}

public interface IMicrophone
{
    internal void Initialize(IMicrophoneContext microphoneContext);

    /// <summary>
    /// 録音を終了する際に呼び出されます。
    /// </summary>
    internal void Close();
}

public class ManualMicrophone : IMicrophone
{
    IMicrophoneContext? context = null;
    void IMicrophone.Initialize(IMicrophoneContext microphoneContext) => context = microphoneContext;
    void IMicrophone.Close() => context = null;
    

    private const int AudioLength1 = (int)(AudioHelpers.ClockRate * 0.020f); //20ms(50FPS)
    private const int AudioLength2 = (int)(AudioHelpers.ClockRate * 0.040f); //40ms(25FPS)
    private float[] cachedAudio = new float[AudioLength2];
    private int cachedLength = 0;
    private float[] sampleBuffer = new float[AudioLength2];

    public void PushAudioData(float[] audioData)
    {
        double bufferMilliseconds;
        int buffers;
        if (cachedLength + audioData.Length >= AudioLength2)
        {
            bufferMilliseconds = 40.0;
            buffers = AudioLength2;

            //最長のパターン。古いデータを捨てて直近の40msを送信する。
            if (AudioLength2 > audioData.Length)
            {
                int cLength = AudioLength2 - audioData.Length;
                cachedAudio.AsSpan(cachedLength - cLength, cLength).CopyTo(sampleBuffer);
                audioData.CopyTo(sampleBuffer.AsSpan(cLength, audioData.Length));
            }
            else
            {
                audioData.AsSpan(audioData.Length - AudioLength2, AudioLength2).CopyTo(sampleBuffer);
            }
            cachedLength = 0;
        }
        else if (cachedLength + audioData.Length >= AudioLength1)
        {
            bufferMilliseconds = 20.0;
            buffers = AudioLength1;
            //中間のパターン。最も古い20msを送信する。
            if (AudioLength1 < cachedLength)
            {
                cachedAudio.AsSpan(0, AudioLength1).CopyTo(sampleBuffer);
                cachedAudio.AsSpan(AudioLength1, cachedLength - AudioLength1).CopyTo(cachedAudio);
                cachedLength -= AudioLength1;
            }
            else
            {
                cachedAudio.AsSpan(0, cachedLength).CopyTo(sampleBuffer);
                int cLength = AudioLength1 - cachedLength;
                audioData.AsSpan(0, cLength).CopyTo(sampleBuffer.AsSpan(cachedLength, cLength));
                int leftLength = audioData.Length - cLength;
                audioData.AsSpan(cLength, leftLength).CopyTo(cachedAudio);
                cachedLength = leftLength;
            }
        }
        else
        {
            //最短のパターン。キャッシュしておいて、次回以降に送信する。
            audioData.CopyTo(cachedAudio.AsSpan(cachedLength, audioData.Length));
            cachedLength += audioData.Length;
            return;
        }

        context?.SendAudio(sampleBuffer, buffers, bufferMilliseconds);
    }
}

public class WindowsMicrophone : IMicrophone
{
    IMicrophoneContext? context = null;
    void IMicrophone.Initialize(IMicrophoneContext microphoneContext)
    {
        context = microphoneContext;

        waveIn = new WaveInEvent() { BufferMilliseconds = 20, NumberOfBuffers = 4 };
        waveIn.DeviceNumber = deviceNum;
        waveIn.WaveFormat = new WaveFormat(48000, 16, 1);
        waveIn.DataAvailable += SendAudio;
        waveIn.StartRecording();
    }

    void IMicrophone.Close()
    {
        context = null;
        waveIn.StopRecording();
    }

    WaveInEvent waveIn;
    float[] sampleBuffer = null!;
    int deviceNum;
    public WindowsMicrophone(string deviceName)
    {
        var count = WaveInEvent.DeviceCount;
        for (int i = 0; i < count; i++)
        {
            if (WaveInEvent.GetCapabilities(i).ProductName == deviceName)
            {
                this.deviceNum = i;
                return;
            }
        }
        this.deviceNum = 0;
    }


    void SendAudio(object? sender, WaveInEventArgs e)
    {
        var samples = e.BytesRecorded / 2;
        if (sampleBuffer == null || sampleBuffer.Length != samples) sampleBuffer = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            sampleBuffer[i] = BitConverter.ToInt16(e.Buffer, i * 2) / 32768f;
        }

        context?.SendAudio(sampleBuffer, samples, waveIn.BufferMilliseconds);
    }
}