using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interstellar.NAudio.Provider;

internal class StereoSampleProvider : ISampleProvider
{
    private readonly ISampleProvider sourceProvider;
    private float[] lastBuffer = null!;
    private int lastBufferCount = 0;
    private float[] tempBuffer = null!;
    public WaveFormat WaveFormat { get; }

    public float Pan { get; set; } = 0.0f; // -1.0 (左) から 1.0 (右)
    private int lastLDelay = 0;
    private int lastRDelay = 0;
    public StereoSampleProvider(ISampleProvider sourceProvider)
    {
        this.sourceProvider = sourceProvider;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sourceProvider.WaveFormat.SampleRate, 2);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (tempBuffer == null || tempBuffer.Length < count / 2) tempBuffer = new float[count / 2];
        int tempLength = sourceProvider.Read(tempBuffer, 0, count / 2);
        
        int monoCount = count / 2;
        int lDelay = (int)((Pan < 0 ? 0 : Pan) * 70);
        int rDelay = (int)((Pan < 0 ? -Pan : 0) * 70);
        int lCount = monoCount - lDelay + lastLDelay;
        int rCount = monoCount - rDelay + lastRDelay;

        for (int i = 0; i < monoCount; i++)
        {
            int lIndex = i * lCount / monoCount;//lastBufferに含まれる遅延分を含めた添え字
            if (lIndex < lastLDelay)
                buffer[offset + i * 2] = lastBuffer[lastBufferCount - lastLDelay + lIndex];
            else
                buffer[offset + i * 2] = tempBuffer[lIndex - lastLDelay];

            int rIndex = i * rCount / monoCount;//lastBufferに含まれる遅延分を含めた添え字
            if (lIndex < lastRDelay)
                buffer[offset + i * 2 + 1] = lastBuffer[lastBufferCount - lastRDelay + rIndex];
            else
                buffer[offset + i * 2 + 1] = tempBuffer[rIndex - lastRDelay];
        }

        lastLDelay = lDelay;
        lastRDelay = rDelay;

        //配列を入れ替える
        var temp = lastBuffer;
        lastBuffer = tempBuffer;
        tempBuffer = temp;
        lastBufferCount = tempLength;

        return count;
    }
}