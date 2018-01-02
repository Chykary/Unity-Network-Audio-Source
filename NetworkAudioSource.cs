using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;


/// <summary>
/// The Network AudioSource allows for easy and fast synchronisation of AudioSources across the network using Unity‘s NetworkMessages. It synchronises from and to both clients and servers.
/// Created by https://twitter.com/Chykary
/// </summary>
public class NetworkAudioSource : NetworkBehaviour
{
  public AudioSource NetworkedAudioSource;

  /// <summary>
  /// Set this to a value you are not using anywhere else. It should be higher than 47.
  /// </summary>
  private static readonly short NetworkAudioSourceMessageIdentifier = 50;

  /// <summary>
  /// Set this to the path within Resources/ that contains the clips
  /// </summary>
  private static readonly string AudioPath = "Audio/Sounds/";

  private static Dictionary<uint, NetworkAudioSource> NetworkedAudioSources;
  private static Dictionary<int, AudioClip> IdToAudioClip;
  private static Dictionary<AudioClip, int> AudioClipToId;
  private static bool initialised = false;
  private uint myIdentifier;

  private Coroutine loopCoroutine;
  private Dictionary<NetworkAudioSource, float> LinkedAudioSources;
  private float originalVolume;

  /// <summary>
  /// The volume of the audio source (0.0 to 1.0).
  /// </summary>
  public float Volume
  {
    get
    {
      return NetworkedAudioSource.volume;
    }
    set
    {
      NetworkAudioSourceMessage message = GetNewMessage();
      message.CallIdentifier = (byte)CallIdentifier.Volume;
      message.Payload = BitConverter.GetBytes(value);

      Send(message);
    }
  }


  /// <summary>
  /// The default AudioClip to play.
  /// </summary>
  public AudioClip Clip
  {
    get
    {
      return NetworkedAudioSource.clip;
    }
    set
    {
      NetworkAudioSourceMessage message = GetNewMessage();
      message.CallIdentifier = (byte)CallIdentifier.Clip;
      message.Payload = BitConverter.GetBytes(AudioClipToId[value]);

      Send(message);
    }
  }


  /// <summary>
  /// Sets the Doppler scale for this AudioSource.
  /// </summary>
  public float DopplerLevel
  {
    get
    {
      return NetworkedAudioSource.dopplerLevel;
    }
    set
    {
      NetworkAudioSourceMessage message = GetNewMessage();
      message.CallIdentifier = (byte)CallIdentifier.DopplerLevel;
      message.Payload = BitConverter.GetBytes(value);

      Send(message);
    }
  }


  /// <summary>
  /// Allows AudioSource to play even though AudioListener.pause is set to true. 
  /// This is useful for the menu element sounds or background music in pause menus.
  /// </summary>
  public bool IgnoreListenerPause
  {
    get
    {
      return NetworkedAudioSource.ignoreListenerPause;
    }
    set
    {
      NetworkAudioSourceMessage message = GetNewMessage();
      message.CallIdentifier = (byte)CallIdentifier.IgnoreListenerPause;
      message.Payload = BitConverter.GetBytes(value);

      Send(message);
    }
  }


  /// <summary>
  /// This makes the audio source not take into account the volume of the audio listener.
  /// </summary>
  public bool IgnoreListenerVolume
  {
    get
    {
      return NetworkedAudioSource.ignoreListenerVolume;
    }
    set
    {
      NetworkAudioSourceMessage message = GetNewMessage();
      message.CallIdentifier = (byte)CallIdentifier.IgnoreListenerVolume;
      message.Payload = BitConverter.GetBytes(value);

      Send(message);
    }
  }


  /// <summary>
  /// Is the clip playing right now (Read Only)?
  /// </summary>
  public bool IsPlaying
  {
    get
    {
      return NetworkedAudioSource.isPlaying;
    }
  }


  /// <summary>
  /// Is the audio clip looping?
  /// </summary>
  public bool Loop
  {
    get
    {
      return NetworkedAudioSource.loop;
    }
    set
    {
      NetworkAudioSourceMessage message = GetNewMessage();
      message.CallIdentifier = (byte)CallIdentifier.Loop;
      message.Payload = BitConverter.GetBytes(value);

      Send(message);
    }
  }


  /// <summary>
  /// The pitch of the audio source.
  /// </summary>
  public float Pitch
  {
    get
    {
      return NetworkedAudioSource.pitch;
    }
    set
    {
      NetworkAudioSourceMessage message = GetNewMessage();
      message.CallIdentifier = (byte)CallIdentifier.Pitch;
      message.Payload = BitConverter.GetBytes(value);

      Send(message);
    }
  }


  /// <summary>
  /// Playback position in seconds.
  /// </summary>
  public float Time
  {
    get
    {
      return NetworkedAudioSource.time;
    }
    set
    {
      NetworkAudioSourceMessage message = GetNewMessage();
      message.CallIdentifier = (byte)CallIdentifier.Time;
      message.Payload = BitConverter.GetBytes(value);

      Send(message);
    }
  }


  /// <summary>
  /// Playback position in PCM samples.
  /// </summary>
  public int TimeSamples
  {
    get
    {
      return NetworkedAudioSource.timeSamples;
    }
    set
    {
      NetworkAudioSourceMessage message = GetNewMessage();
      message.CallIdentifier = (byte)CallIdentifier.TimeSamples;
      message.Payload = BitConverter.GetBytes(value);

      Send(message);
    }
  }



  protected void Start()
  {
    if (NetworkedAudioSource == null)
    {
      Debug.Log("NetworkedAudioSource not assigned for GameObject " + gameObject.name);
      return;
    }

    Debug.Assert(NetworkAudioSourceMessageIdentifier > MsgType.Highest, "NetworkAudioSourceMessageIdentifier must be higher than internal range!");


    if (!initialised)
    {
      initialised = true;
      NetworkedAudioSources = new Dictionary<uint, NetworkAudioSource>();
      List<AudioClip> clips = Resources.LoadAll<AudioClip>(AudioPath).ToList();
      clips.Sort(delegate (AudioClip x, AudioClip y)
      {
        if (x.GetHashCode() > y.GetHashCode())
        {
          return 1;
        }
        else
        {
          return -1;
        }
      });

      IdToAudioClip = new Dictionary<int, AudioClip>();
      AudioClipToId = new Dictionary<AudioClip, int>();

      foreach (AudioClip audioClip in clips)
      {
        Debug.Assert(!AudioClipToId.ContainsKey(audioClip), "Hash Collision! You can fix this by renaming the audioclip with name " + audioClip.name);

        IdToAudioClip.Add(audioClip.name.GetHashCode(), audioClip);
        AudioClipToId.Add(audioClip, audioClip.name.GetHashCode());
      }

      NetworkServer.RegisterHandler(NetworkAudioSourceMessageIdentifier, OnReceiveNetworkAudioSourceMessage);
      NetworkManager.singleton.client.RegisterHandler(NetworkAudioSourceMessageIdentifier, OnReceiveNetworkAudioSourceMessage);
    }


    myIdentifier = GetComponent<NetworkIdentity>().netId.Value;
    NetworkedAudioSources.Add(myIdentifier, this);

    LinkedAudioSources = new Dictionary<NetworkAudioSource, float>();
    LinkedAudioSources.Add(this, 1f);

    originalVolume = Volume;
  }


  /// <summary>
  /// Pauses playing the clip.
  /// </summary>
  public void Pause()
  {
    NetworkAudioSourceMessage message = GetNewMessage();
    message.CallIdentifier = (byte)CallIdentifier.Pause;

    Send(message);
  }


  /// <summary>
  /// Plays the clip with an optional certain delay.
  /// </summary>
  /// <param name="delay">Delay in number of samples, assuming a 44100Hz sample rate (meaning that Play(44100) will delay the playing by exactly 1 sec).</param>
  public void Play(ulong delay = 0)
  {
    NetworkAudioSourceMessage message = GetNewMessage();
    message.CallIdentifier = (byte)CallIdentifier.Play;
    message.Payload = BitConverter.GetBytes(delay);

    Send(message);
  }


  /// <summary>
  /// Plays an AudioClip, and scales the AudioSource volume by volumeScale.
  /// </summary>
  /// <param name="clip">The clip being played.</param>
  /// <param name="volumeScale">The scale of the volume (0-1).</param>
  public void PlayOneShot(AudioClip clip, float volumeScale = 1.0F)
  {
    if (clip == null || !AudioClipToId.ContainsKey(clip))
    {
      Debug.Log("Clip not contained in NetworkAudioSource");
    }
    else
    {
      NetworkAudioSourceMessage message = GetNewMessage();
      message.CallIdentifier = (byte)CallIdentifier.PlayOneShot;
      int audioClipId = AudioClipToId[clip];
      byte[] clipByteArray = BitConverter.GetBytes(audioClipId);
      byte[] volumeScaleArray = BitConverter.GetBytes(volumeScale);
      message.Payload = new byte[8];
      Buffer.BlockCopy(clipByteArray, 0, message.Payload, 0, 4);
      Buffer.BlockCopy(volumeScaleArray, 0, message.Payload, 4, 4);

      Send(message);
    }
  }


  /// <summary>
  /// Plays the clip with a delay specified in seconds. Users are advised to use this function instead of the old Play(delay) function
  /// that took a delay specified in samples relative to a reference rate of 44.1 kHz as an argument.
  /// </summary>
  /// <param name="delay">Delay time specified in seconds.</param>
  public void PlayDelayed(float delay)
  {
    NetworkAudioSourceMessage message = GetNewMessage();
    message.CallIdentifier = (byte)CallIdentifier.PlayDelayed;
    message.Payload = BitConverter.GetBytes(delay);

    Send(message);
  }


  /// <summary>
  /// Plays the clip at a specific time on the absolute time-line that AudioSettings.dspTime reads from.
  /// </summary>
  /// <param name="time">Time in seconds on the absolute time-line that AudioSettings.dspTime refers to for when the sound should start playing.</param>
  public void PlayScheduled(double time)
  {
    NetworkAudioSourceMessage message = GetNewMessage();
    message.CallIdentifier = (byte)CallIdentifier.PlayScheduled;
    message.Payload = BitConverter.GetBytes(time);

    Send(message);
  }


  /// <summary>
  /// Stops playing the clip.
  /// </summary>
  public void Stop()
  {
    NetworkAudioSourceMessage message = GetNewMessage();
    message.CallIdentifier = (byte)CallIdentifier.Stop;

    Send(message);
  }


  /// <summary>
  /// Unpause the paused playback of this AudioSource.
  /// </summary>
  public void UnPause()
  {
    NetworkAudioSourceMessage message = GetNewMessage();
    message.CallIdentifier = (byte)CallIdentifier.UnPause;

    Send(message);
  }


  /// <summary>
  /// Fades out the AudioSource given a specified time.
  /// </summary>
  /// <param name="fadeTime">Time to fade out clip.</param>
  public void FadeOut(float fadeTime)
  {
    NetworkAudioSourceMessage message = GetNewMessage();
    message.CallIdentifier = (byte)CallIdentifier.FadeOut;
    message.Payload = BitConverter.GetBytes(fadeTime);

    Send(message);
  }


  /// <summary>
  /// Fades the AudioSource from the current volume to a specified target volume given a specified time.
  /// </summary>
  /// <param name="fadeTime">Target volume to fade in to.</param>
  /// <param name="targetVolume">Time to fade in clip.</param>
  public void FadeIn(float targetVolume, float fadeTime)
  {
    NetworkAudioSourceMessage message = GetNewMessage();
    message.CallIdentifier = (byte)CallIdentifier.FadeIn;

    byte[] arrTargetVol = BitConverter.GetBytes(targetVolume);
    byte[] arrFadeTime = BitConverter.GetBytes(fadeTime);
    message.Payload = new byte[8];
    Buffer.BlockCopy(arrTargetVol, 0, message.Payload, 0, 4);
    Buffer.BlockCopy(arrFadeTime, 0, message.Payload, 4, 4);

    Send(message);
  }


  private IEnumerator FadeOutRoutine(float fadeTime, float startVolume)
  {
    while (NetworkedAudioSource.volume > 0)
    {
      NetworkedAudioSource.volume -= startVolume * UnityEngine.Time.deltaTime / fadeTime;

      yield return null;
    }

    NetworkedAudioSource.Stop();
    NetworkedAudioSource.volume = startVolume;
  }


  private IEnumerator FadeInRoutine(float targetVolume, float fadeTime, float startVolume)
  {
    while (NetworkedAudioSource.volume < targetVolume)
    {
      NetworkedAudioSource.volume += (targetVolume - startVolume) * UnityEngine.Time.deltaTime / fadeTime;

      yield return null;
    }

    NetworkedAudioSource.volume = targetVolume;
  }

  public void PlayAndLoop(AudioClip clip, float volume)
  {
    Clip = clip;
    Loop = true;
    Volume = volume;
    Play();
  }

  /// <summary>
  /// Randomly and endlessly plays one of the provided clips with a random time distance specified.
  /// </summary>
  /// <param name="minTime">minimum time until next clip</param>
  /// <param name="maxTime">maximum time until next clip</param>
  /// <param name="clips">clips to choose from</param>
  /// <returns></returns>
  public void LoopRandomClips(float minTime, float maxTime, params AudioClip[] clips)
  {
    Debug.Assert(loopCoroutine == null, "Starting Coroutine, but another one is still running which can not be stopped anymore");
    loopCoroutine = StartCoroutine(LoopRandomClips(clips, minTime, maxTime));
  }


  /// <summary>
  /// Stops the playing of random and endless clips.
  /// </summary>
  public void StopLoopRandomClips()
  {
    if (loopCoroutine != null)
    {
      StopCoroutine(loopCoroutine);
      loopCoroutine = null;
    }
  }


  private IEnumerator LoopRandomClips(AudioClip[] clips, float minTime, float maxTime)
  {
    while(true)
    {
      PlayOneShot(clips[Random.Range(0, clips.Length)]);
      yield return new WaitForSeconds(Random.Range(minTime, maxTime));
    }
  }


  /// <summary>
  /// Adds a linked audioSource that plays sounds like this one, but with a damping factor. Can only be called on the server.
  /// </summary>
  /// <param name="audioSource">audioSource that should be linked</param>
  /// <param name="dampingFactor">damping when passing sounds. 1f = no damping, 0f = silence</param>
  /// <param name="bidirectionalLink"></param>
  [Server]
  public void AddLinkedSource(NetworkAudioSource audioSource, float dampingFactor, bool bidirectionalLink = false)
  {
    if (bidirectionalLink)
    {
      audioSource.AddLinkedSource(this, dampingFactor, false);
    }

    RpcAddLinkedSource(audioSource.myIdentifier, dampingFactor);
  }


  [ClientRpc]
  private void RpcAddLinkedSource(uint identifier, float dampingFactor)
  {
    Debug.Assert(identifier != 0, "NetworkAudioSource not initialised!");
    LinkedAudioSources.Add(NetworkedAudioSources[identifier], dampingFactor);
  }


  private void SetVolume(float volume, bool dampened)
  {
    originalVolume = volume;
    NetworkedAudioSource.volume = volume;
  }


  private NetworkAudioSourceMessage GetNewMessage()
  {
    NetworkAudioSourceMessage message = new NetworkAudioSourceMessage();
    message.NetworkAudioSourceIdentifier = myIdentifier;
    return message;
  }


  private void Send(NetworkAudioSourceMessage message)
  {
    if (Network.isServer)
    {
      NetworkServer.SendToAll(NetworkAudioSourceMessageIdentifier, message);
    }
    else
    {
      NetworkManager.singleton.client.Send(NetworkAudioSourceMessageIdentifier, message);
    }
  }


  private void OnReceiveNetworkAudioSourceMessage(NetworkMessage message)
  {
    NetworkAudioSourceMessage networkAudioSourceMessage = message.ReadMessage<NetworkAudioSourceMessage>();

    if (isServer && message.conn != NetworkManager.singleton.client.connection)
    {
      NetworkServer.SendToAll(NetworkAudioSourceMessageIdentifier, networkAudioSourceMessage);
    }
    else
    {
      NetworkAudioSource targetAudioSource = NetworkedAudioSources[networkAudioSourceMessage.NetworkAudioSourceIdentifier];

      foreach (KeyValuePair<NetworkAudioSource, float> audioSourceVolumePair in targetAudioSource.LinkedAudioSources)
      {
        NetworkAudioSource audioSource = audioSourceVolumePair.Key;
        float dampingFactor = audioSourceVolumePair.Value;

        switch (networkAudioSourceMessage.CallIdentifier)
        {
          case (byte)CallIdentifier.Pause:
            audioSource.NetworkedAudioSource.Pause();
            break;
          case (byte)CallIdentifier.Stop:
            audioSource.NetworkedAudioSource.Stop();
            break;
          case (byte)CallIdentifier.UnPause:
            audioSource.NetworkedAudioSource.UnPause();
            break;
          case (byte)CallIdentifier.Play:
            ulong playDelay = BitConverter.ToUInt64(networkAudioSourceMessage.Payload, 0);
            audioSource.SetVolume(originalVolume * dampingFactor, true);
            audioSource.NetworkedAudioSource.Play(playDelay);
            break;
          case (byte)CallIdentifier.PlayDelayed:
            float playDelayedDelay = BitConverter.ToSingle(networkAudioSourceMessage.Payload, 0);
            audioSource.NetworkedAudioSource.PlayDelayed(playDelayedDelay);
            break;
          case (byte)CallIdentifier.PlayScheduled:
            double playScheduledTime = BitConverter.ToDouble(networkAudioSourceMessage.Payload, 0);
            audioSource.NetworkedAudioSource.PlayScheduled(playScheduledTime);
            break;
          case (byte)CallIdentifier.PlayOneShot:
            int clipUid = BitConverter.ToInt32(networkAudioSourceMessage.Payload, 0);
            float volumeScale = BitConverter.ToSingle(networkAudioSourceMessage.Payload, 4) * dampingFactor;
            if (!IdToAudioClip.ContainsKey(clipUid))
            {
              Debug.Log("Clip not contained in NetworkAudioSource");
            }
            else
            {
              AudioClip clip = IdToAudioClip[clipUid];
              audioSource.NetworkedAudioSource.PlayOneShot(clip, volumeScale);
            }
            break;
          case (byte)CallIdentifier.Volume:
            uint volume = BitConverter.ToUInt32(networkAudioSourceMessage.Payload, 0);
            audioSource.SetVolume(volume, false);
            break;
          case (byte)CallIdentifier.FadeOut:
            float fadeTime = BitConverter.ToSingle(networkAudioSourceMessage.Payload, 0);
            audioSource.StartCoroutine(audioSource.FadeOutRoutine(fadeTime, originalVolume * dampingFactor));
            break;
          case (byte)CallIdentifier.FadeIn:
            float targetVol = BitConverter.ToSingle(networkAudioSourceMessage.Payload, 0);
            float fadeInTime = BitConverter.ToSingle(networkAudioSourceMessage.Payload, 4);
            audioSource.StartCoroutine(audioSource.FadeInRoutine(targetVol, fadeInTime, originalVolume * dampingFactor));
            break;
          case (byte)CallIdentifier.Clip:
            int clipId = BitConverter.ToInt32(networkAudioSourceMessage.Payload, 0);
            if (!IdToAudioClip.ContainsKey(clipId))
            {
              Debug.Log("Clip not contained in NetworkAudioSource");
            }
            else
            {
              AudioClip clip = IdToAudioClip[clipId];
              audioSource.NetworkedAudioSource.clip = clip;
            }
            break;
          case (byte)CallIdentifier.DopplerLevel:
            float dopplerLevel = BitConverter.ToSingle(networkAudioSourceMessage.Payload, 0);
            audioSource.NetworkedAudioSource.dopplerLevel = dopplerLevel;
            break;
          case (byte)CallIdentifier.IgnoreListenerPause:
            bool ignoreListenerPause = BitConverter.ToBoolean(networkAudioSourceMessage.Payload, 0);
            audioSource.NetworkedAudioSource.ignoreListenerPause = ignoreListenerPause;
            break;
          case (byte)CallIdentifier.IgnoreListenerVolume:
            bool ignoreListenerVolume = BitConverter.ToBoolean(networkAudioSourceMessage.Payload, 0);
            audioSource.NetworkedAudioSource.ignoreListenerVolume = ignoreListenerVolume;
            break;
          case (byte)CallIdentifier.Loop:
            bool loop = BitConverter.ToBoolean(networkAudioSourceMessage.Payload, 0);
            audioSource.NetworkedAudioSource.loop = loop;
            break;
          case (byte)CallIdentifier.Pitch:
            float pitch = BitConverter.ToSingle(networkAudioSourceMessage.Payload, 0);
            audioSource.NetworkedAudioSource.pitch = pitch;
            break;
          case (byte)CallIdentifier.Time:
            float time = BitConverter.ToSingle(networkAudioSourceMessage.Payload, 0);
            audioSource.NetworkedAudioSource.time = time;
            break;
          case (byte)CallIdentifier.TimeSamples:
            int timeSamples = BitConverter.ToInt32(networkAudioSourceMessage.Payload, 0);
            audioSource.NetworkedAudioSource.timeSamples = timeSamples;
            break;
        }
      }
    }
  }

  private class NetworkAudioSourceMessage : MessageBase
  {
    public uint NetworkAudioSourceIdentifier;
    public byte CallIdentifier;
    public byte[] Payload;
  }


  private enum CallIdentifier : byte
  {
    Pause, Stop, UnPause, Play, PlayDelayed, PlayScheduled, PlayOneShot, Volume, FadeOut, FadeIn, Clip, DopplerLevel, IgnoreListenerPause, IgnoreListenerVolume,
    Loop, Pitch, Time, TimeSamples
  }
}