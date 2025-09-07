using Concentus;
using Interstellar.Messages;
using Interstellar.NAudio.Provider;
using Interstellar.Routing.Node;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interstellar.Routing;

public class AudioRoutingInstance
{
    private List<AudioBuffer> buffers = [];
    private AudioRoutingInstanceNode[] nodes;
    private BufferedSampleProvider sourceProvider;
    private IOpusDecoder decoder = AudioHelpers.GetOpusDecoder();
    static private float[] decodeBuffer = new float[2048];
    internal AudioRoutingInstanceNode GetProperty(int propertyId) => nodes[propertyId];

    
    internal AudioRoutingInstance(List<AudioBuffer> buffers, AudioRoutingInstanceNode[] nodes, BufferedSampleProvider sourceProvider)
    {
        this.buffers = buffers;
        this.nodes = nodes;
        this.sourceProvider = sourceProvider;
    }

    public void AddSamples(byte[] samples, int offset, int count)
    {
        int sampleLength = decoder.Decode(samples.AsSpan(offset, count), decodeBuffer, decodeBuffer.Length);
        sourceProvider.AddSamples(decodeBuffer, 0, sampleLength);
    }
}
