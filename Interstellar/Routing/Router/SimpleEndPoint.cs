using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interstellar.Routing.Router;

public class SimpleEndPoint : AbstractAudioRouter
{
    public SimpleEndPoint()
    {
        IsGlobalRouter = true;
    }

    protected internal override bool ShouldBeGivenStereoInput => false;
    protected internal override bool IsEndpoint => true;
    internal override ISampleProvider GenerateProcessor(ISampleProvider source)
    {
        return source;
    }

}
