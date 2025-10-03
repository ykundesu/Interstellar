using Interstellar.Messages.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interstellar.Messages.Variation;

public class CustomMessage : IMessage
{
    public byte[] Data;

    public CustomMessage(byte[] data)
    {
        Data = data;    
    }

    int IMessage.Serialize(Span<byte> bytes)
    {
        int length = 0;
        length += IMessage.SerializeTag(ref bytes, MessageTag.Custom);
        length += IMessage.SerializeBytes(ref bytes, Data);
        return length;
    }
}

