using Interstellar.Network;

namespace Interstellar.VoiceChat;

public class VCRoom : IConnectionContext
{
    private RoomConnection connection;

    internal VCRoom(string roomCode, string region, string url, byte localPlayerId, string localPlayerName)
    {
        this.connection = new RoomConnection(this, roomCode, region, url, localPlayerId, localPlayerName);
    }

    /// <summary>
    /// 自身のプロフィールを更新します。
    /// ゲーム終了後、ロビーに戻ったときなどに呼び出してください。
    /// </summary>
    /// <param name="playerName">プレイヤー名</param>
    /// <param name="playerId">プレイヤーID</param>
    public void UpdateProfile(string playerName, byte playerId) => this.connection.UpdateProfile(playerName, playerId);

    /// <summary>
    /// 使用するマイクをデバイスIDで指定します。
    /// このメソッドを呼び出すまで音声は送信されません。
    /// </summary>
    /// <param name="deviceId"></param>
    public void SetMicrophone(int deviceId) => this.connection.SetMicrophone(deviceId);

    void IConnectionContext.OnAudioFrameReceived(int clientId, float[] bytes, int length)
    {
    }
}
