using Interstellar.NAudio.Provider;
using NAudio.Dsp;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interstellar.Routing.Router;

public class FilterRouter : AbstractAudioRouter
{
    public class FilteredProvider : ISampleProvider
    {
        private BiQuadFilter filter;
        private ISampleProvider source;
        WaveFormat ISampleProvider.WaveFormat => source.WaveFormat;

        int ISampleProvider.Read(float[] buffer, int offset, int count)
        {
            int read = source.Read(buffer, offset, count);
            for (int n = 0; n < read; n++)
            {
                buffer[offset + n] = filter.Transform(buffer[offset + n]);
            }
            return read;
        }

        internal FilteredProvider(ISampleProvider source, BiQuadFilter filter)
        {
            this.source = source;
            this.filter = filter;
        }
    }
    protected internal override bool ShouldBeGivenStereoInput => false;
    protected internal override bool IsEndpoint => false;
    private Func<BiQuadFilter> filterGenerator;
    
    private FilterRouter(Func<BiQuadFilter> filterGenerator, bool isGlobalRouter = false)
    {
        this.filterGenerator = filterGenerator;
        IsGlobalRouter = isGlobalRouter;
        Channels = 1;
    }

    /// <summary>
    /// 高周波数の音を除去し、低周波数の音を通過させるローパスフィルタを作成します。
    /// </summary>
    /// <param name="sampleRate"></param>
    /// <param name="cutoffFrequency"></param>
    /// <param name="qFactor"></param>
    /// <param name="isGlobalRouter"></param>
    /// <returns></returns>
    static public FilterRouter CreateLowPassFilter(float sampleRate, float cutoffFrequency, float qFactor, bool isGlobalRouter = false)
    {
        return new FilterRouter(() => BiQuadFilter.LowPassFilter(sampleRate, cutoffFrequency, qFactor), isGlobalRouter);
    }

    /// <summary>
    /// 低周波数の音を除去し、高周波数の音を通過させるハイパスフィルタを作成します。
    /// </summary>
    /// <param name="sampleRate"></param>
    /// <param name="cutoffFrequency"></param>
    /// <param name="qFactor"></param>
    /// <param name="isGlobalRouter"></param>
    /// <returns></returns>
    static public FilterRouter CreateHighPassFilter(float sampleRate, float cutoffFrequency, float qFactor, bool isGlobalRouter = false)
    {
        return new FilterRouter(() => BiQuadFilter.HighPassFilter(sampleRate, cutoffFrequency, qFactor), isGlobalRouter);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sampleRate"></param>
    /// <param name="centerFrequency"></param>
    /// <param name="qFactor"></param>
    /// <param name="isGlobalRouter"></param>
    /// <param name="constantPeakGain">trueにするとピークの音量が固定されます。falseにするとQ</param>
    /// <returns></returns>
    static public FilterRouter CreateBandPassFilter(float sampleRate, float centerFrequency, float qFactor, bool isGlobalRouter = false)
    {
        return new FilterRouter(() => BiQuadFilter.BandPassFilterConstantPeakGain(sampleRate, centerFrequency, qFactor), isGlobalRouter);
    }

    /// <summary>
    /// 特定の帯域の音を除去するノッチフィルタを作成します。
    /// </summary>
    /// <param name="sampleRate"></param>
    /// <param name="centerFrequency"></param>
    /// <param name="qFactor"></param>
    /// <param name="isGlobalRouter"></param>
    /// <returns></returns>
    static public FilterRouter CreateNotchFilter(float sampleRate, float centerFrequency, float qFactor, bool isGlobalRouter = false)
    {
        return new FilterRouter(() => BiQuadFilter.NotchFilter(sampleRate, centerFrequency, qFactor), isGlobalRouter);
    }



    internal override ISampleProvider GenerateProcessor(ISampleProvider source)
    {
        return new FilteredProvider(source, filterGenerator());
    }

}
