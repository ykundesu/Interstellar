using Interstellar.Network;
using Interstellar.Routing;
using static Interstellar.VoiceChat.VCRoom;

namespace Interstellar.VoiceChat;

public class VCRoom : IConnectionContext
{
    private RoomConnection connection;
    private AudioManager audioManager;
    private Dictionary<int, AudioRoutingInstance> audioInstances = new();
    private readonly OnConnectClient onConnectClient;
    private readonly OnUpdateProfile onUpdateProfile;
    private bool loopBack = false;

    public delegate void OnConnectClient(int clientId, AudioRoutingInstance routing, bool isLocalClient);
    public delegate void OnUpdateProfile(int clientId, byte playerId, string playerName);
    public VCRoom(AbstractAudioRouter audioRouter, string roomCode, string region, string url, OnConnectClient onConnectClient, OnUpdateProfile onUpdateProfile)
    {
        this.connection = new RoomConnection(this, roomCode, region, url);
        this.audioManager = new AudioManager(audioRouter);
        this.onConnectClient = onConnectClient;
        this.onUpdateProfile = onUpdateProfile;
    }

    public void SetLoopBack(bool enable) => this.loopBack = enable;

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

    /// <summary>
    /// 音声の再生を開始します。
    /// </summary>
    /// <param name="deviceName"></param>
    public void SetSpeaker(string deviceName) => this.audioManager.Start(deviceName);

    private AudioRoutingInstance GetOrCreateAudioInstance(int clientId, bool asLocalClient)
    {
        if (!audioInstances.TryGetValue(clientId, out var instance))
        {
            instance = audioManager.Generate(clientId);
            onConnectClient?.Invoke(clientId, instance, asLocalClient);
            audioInstances[clientId] = instance;
        }
        return instance;
    }

    private bool TryGetAudioInstance(int clientId, out AudioRoutingInstance? instance) => audioInstances.TryGetValue(clientId, out instance);
    
    void IConnectionContext.OnAudioFrameReceived(int clientId, float[] samples, int length)
    {
        var instance = GetOrCreateAudioInstance(clientId, false);
        instance.AddSamples(samples, 0, length);
    }

    void IConnectionContext.OnClientDisconnected(int clientId)
    {
        if(TryGetAudioInstance(clientId, out var instance))
        {
            //instance.
            audioManager.Remove(clientId);
            audioInstances.Remove(clientId);
        }
    }

    void IConnectionContext.OnClientProfileUpdated(int clientId, string playerName, byte playerId)
    {
        onUpdateProfile?.Invoke(clientId, playerId, playerName);
    }

    void ISenderContext.OnAudioSent(float[] buffer, int offset, int count)
    {
        if (loopBack && connection.MyClientId != -1)
        {
            var instance = GetOrCreateAudioInstance(connection.MyClientId, true);
            instance.AddSamples(buffer, offset, count);
        }
    }

    public void Disconnect()
    {
        connection.Disconnect();
        audioManager.Stop();
    }
}
