using Interstellar.Messages;
using Interstellar.Network;
using Interstellar.Routing;
using NAudio.Wave;
using static Interstellar.VoiceChat.VCRoom;

namespace Interstellar.VoiceChat;

public class VCRoom : IConnectionContext, IHasAudioPropertyNode, IMicrophoneContext, ISpeakerContext
{
    private RoomConnection connection;
    private AudioManager audioManager;
    private Dictionary<int, AudioRoutingInstance> audioInstances = new();
    private readonly OnConnectClient onConnectClient;
    private readonly OnUpdateProfile onUpdateProfile;
    private bool loopBack = false;

    public delegate void OnConnectClient(int clientId, AudioRoutingInstance routing, bool isLocalClient);
    public delegate void OnUpdateProfile(int clientId, byte playerId, string playerName);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="audioRouter"></param>
    /// <param name="roomCode"></param>
    /// <param name="region"></param>
    /// <param name="url"></param>
    /// <param name="onConnectClient"></param>
    /// <param name="onUpdateProfile">プロフィールが更新されたときに呼び出されます。過去に共有されたProfileであっても、onConnectClientで接続を通知された後に呼び出されることが保証されています。</param>
    public VCRoom(AbstractAudioRouter audioRouter, string roomCode, string region, string url, OnConnectClient onConnectClient, OnUpdateProfile onUpdateProfile, int bufferMaxLength = 4096, int bufferLength = 2048)
    {
        this.connection = new RoomConnection(this, roomCode, region, url);
        this.audioManager = new AudioManager(audioRouter, bufferLength, bufferMaxLength);
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
    /// 音声を送信します。
    /// </summary>
    /// <param name="samples"></param>
    /// <param name="length"></param>
    void IMicrophoneContext.SendAudio(float[] samples, int samplesLength, double samplesMilliseconds)
    {
        this.connection.SendAudio(samples, samplesLength, samplesMilliseconds);
        OnAudioSent(samples, samplesLength);
    }

    ISampleProvider? ISpeakerContext.GetEndpoint() => audioManager.Endpoint;

    IMicrophone? microphone = null;
    public void SetMicrophone(IMicrophone? micrphone)
    {
        this.microphone?.Close();
        micrphone?.Initialize(this);
        this.microphone = micrphone;
    }

    ISpeaker? speaker = null;
    public void SetSpeaker(ISpeaker? speaker)
    {
        this.speaker?.Close();
        speaker?.Initialize(this);
        this.speaker = speaker;
    }

    private AudioRoutingInstance GetOrCreateAudioInstance(int clientId, bool asLocalClient)
    {
        if (!audioInstances.TryGetValue(clientId, out var instance))
        {
            instance = audioManager.Generate(clientId);
            onConnectClient?.Invoke(clientId, instance, asLocalClient);
            if(pooledProfile.TryGetValue(clientId, out var profile))
            {
                onUpdateProfile?.Invoke(clientId, profile.id, profile.name);
                pooledProfile.Remove(clientId);
            }
            audioInstances[clientId] = instance;
        }
        return instance;
    }

    private bool TryGetAudioInstance(int clientId, out AudioRoutingInstance? instance) => audioInstances.TryGetValue(clientId, out instance);

    AudioRoutingInstanceNode IHasAudioPropertyNode.GetProperty(int propertyId) => (audioManager as IHasAudioPropertyNode).GetProperty(propertyId);

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

    Dictionary<int, (string name, byte id)> pooledProfile = [];
    void IConnectionContext.OnClientProfileUpdated(int clientId, string playerName, byte playerId)
    {
        if (TryGetAudioInstance(clientId, out _))
        {
            onUpdateProfile?.Invoke(clientId, playerId, playerName);
        }
        else
        {
            pooledProfile[clientId] = (playerName, playerId);
        }
    }

    void OnAudioSent(float[] buffer, int count)
    {
        if (loopBack && connection.MyClientId != -1)
        {
            var instance = GetOrCreateAudioInstance(connection.MyClientId, true);
            instance.AddSamples(buffer, 0, count);
        }
    }

    public void Disconnect()
    {
        connection.Disconnect();
        SetMicrophone(null);
        SetSpeaker(null);
    }

    public int SampleRate => AudioHelpers.ClockRate;
}
