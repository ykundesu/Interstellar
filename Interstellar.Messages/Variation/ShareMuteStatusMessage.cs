using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interstellar.Messages.Variation;

public class ShareMuteStatusMessage : IMessage
{
    public byte AudioId { get; }
    public bool IsMute { get; }

    public ShareMuteStatusMessage(byte audioId, bool isMute)
    {
        this.AudioId = audioId;
        this.IsMute = isMute;
    }

    int IMessage.Serialize(Span<byte> bytes)
    {
        int length = 0;
        length += IMessage.SerializeTag(ref bytes, MessageTag.ShareMuteStatus);
        length += IMessage.SerializeByte(ref bytes, AudioId);
        length += IMessage.SerializeBoolean(ref bytes, IsMute);
        return length;
    }

    static public ShareMuteStatusMessage DeserializeWithoutTag(ReadOnlySpan<byte> bytes, out int read)
    {
        read = 0;
        read += IMessage.DeserializeByte(ref bytes, out var audioId);
        read += IMessage.DeserializeBoolean(ref bytes, out var isMute);
        return new(audioId, isMute);
    }
}
