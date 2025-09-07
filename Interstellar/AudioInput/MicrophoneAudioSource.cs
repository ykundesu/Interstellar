using Concentus;
using Interstellar.Messages;
using NAudio.Wave;
using SIPSorcery.Net;

namespace Interstellar.AudioInput;

public class MicrophoneAudioSource
{
    WaveInEvent waveIn;
    IOpusEncoder encoder = AudioHelpers.GetOpusEncoder();
    AudioStream? audioStream;
    float[] sampleBuffer = null;
    byte[] encodedBuffer = new byte[4096];
    public MicrophoneAudioSource(int deviceNum)
    {
        waveIn = new WaveInEvent() { BufferMilliseconds = 20, NumberOfBuffers = 4 };
        waveIn.DeviceNumber = deviceNum;
        waveIn.WaveFormat = new WaveFormat(48000, 16, 1);
        waveIn.DataAvailable += SendAudio;
        waveIn.StartRecording();
    }

    public void BindToConnection(AudioStream audioStream)
    {
        this.audioStream = audioStream;
    }

    void SendAudio(object? sender, WaveInEventArgs e)
    {
        var samples = e.BytesRecorded / 2;
        if(sampleBuffer == null || sampleBuffer.Length != samples) sampleBuffer = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            sampleBuffer[i] = BitConverter.ToInt16(e.Buffer, i * 2) / 32768f;
        }

        var durationRtpUnits = waveIn.BufferMilliseconds.ToRtpUnits(AudioHelpers.ClockRate);

        int encodedLength = encoder.Encode(sampleBuffer, samples, encodedBuffer, encodedBuffer.Length);
        audioStream?.SendAudio(durationRtpUnits, new ArraySegment<byte>(encodedBuffer, 0, encodedLength));
    }

    public void Close()
    {
        waveIn.StopRecording();
    }
}


