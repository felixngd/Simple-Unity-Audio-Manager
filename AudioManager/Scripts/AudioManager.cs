﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// AudioManager singleton that manages all audio in the game
/// </summary>
[System.Serializable]
public class AudioManager : MonoBehaviour {

    /// <summary>
    /// From 0 (least important) to 255 (most important)
    /// </summary>
    public enum Priority
    {
        Music = 0,
        Low = 64,
        Default = 128,
        High = 192,
        Spam = 256
    }

    public enum Pitch
    {
        None,
        VeryLow,
        Low,
        Medium,
        High
    }

    /// <summary>
    /// Defined as a class with predefined static values to use as an enum of floats
    /// </summary>
    public class Pitches
    {
        public static float None = 0;
        public static float VeryLow = 0.05f;
        public static float Low = 0.15f;
        public static float Medium = 0.25f;
        public static float High = 0.5f;
    }

    /// <summary>
    /// Number of Audio Sources to be created on start
    /// </summary>
    [SerializeField]
    [Tooltip("Number of Audio Sources to be created on start")]
    int audioSources = 16;

    [SerializeField]
    [Tooltip("If true, adds more sources if you exceed the starting count, keeping this disabled is recommended")]
    bool dynamicSourceAllocation;

    Dictionary<string, AudioClip> sounds = new Dictionary<string, AudioClip>();

    Dictionary<string, AudioClip[]> music = new Dictionary<string, AudioClip[]>();

    /// <summary>
    /// List of sources allocated to play looping sounds
    /// </summary>
    //[SerializeField]
    [Tooltip("List of sources allocated to play looping sounds")]
    List<AudioSource> loopingSources;

    //[SerializeField]
    [Tooltip("[DON'T TOUCH THIS], looping sound positions")]
    Transform[] sourcePositions;

    /// <summary>
    /// Limits the number of each sounds being played. If at 0 or no value, assume infinite
    /// </summary>
    //[SerializeField]
    [Tooltip("Limits the number of each sounds being played. If at 0 or no value, assume infinite")]
    int[] exclusiveList;

    AudioSource[] sources;

    /// <summary>
    /// One sources dedicated to playing music
    /// </summary>
    AudioSource musicSource;

    [SerializeField]
    [Range(0, 1)]
    float soundVolume;

    [SerializeField]
    [Range(0, 1)]
    float musicVolume;

    [SerializeField]
    [Tooltip("If true, enables 3D spatialized audio for sound effects, does not effect music")]
    bool spatialSound = true;

    public static AudioManager Singleton;

    /// <summary>
    /// Only used if you have super special music with an intro portion that plays only once
    /// </summary>
    bool queueIntro;

    /// <summary>
    /// Current music that's playing
    /// </summary>
    //[Tooltip("Current music that's playing")]
    [HideInInspector]
    public string currentTrack = "None";

    [Tooltip("The Audio Listener in your scene, will try to automatically locate on start")]
    [SerializeField]
    AudioListener listener;

    [SerializeField]
    GameObject sourcePrefab;

    bool doneLoading;

    string editorMessage = "";

    // Use this for initialization
    void Awake () {
        if (Singleton == null)
        {
            Singleton = this;
        }
        else if (Singleton != this)
            Destroy(gameObject);

        // AudioManager is important, keep it between scenes
        DontDestroyOnLoad(gameObject);

        sources = new AudioSource[audioSources];
        loopingSources = new List<AudioSource>();
        sourcePositions = new Transform[audioSources];
        GameObject sourceHolder = new GameObject("Sources");
        sourceHolder.transform.SetParent(transform);

        for (int i = 0; i < audioSources; i++)
        {
            sources[i] = Instantiate(sourcePrefab, sourceHolder.transform).GetComponent<AudioSource>();
        }

        // Subscribes itself to the sceneLoaded notifier
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Get a reference to all our audiosources on startup
        sources = sourceHolder.GetComponentsInChildren<AudioSource>();

        GameObject m = new GameObject("MusicSource");
        m.transform.parent = transform;
        m.AddComponent<AudioSource>();
        musicSource = m.GetComponent<AudioSource>();
        musicSource.priority = (int)Priority.Music;

        //Set sources properties based on current settings
        SetSoundVolume(soundVolume);
        SetMusicVolume(musicVolume);
        SetSpatialSound(spatialSound);

        // Find the listener if not manually set
        FindNewListener();

        print(currentTrack);
        PlayMusic(currentTrack, true);

        //AddOffsetToArrays();

        doneLoading = true;
    }

    void FindNewListener()
    {
        if (listener == null)
        {
            listener = Camera.main.GetComponent<AudioListener>();
            if (listener == null) // In the case that there still isn't an AudioListener
            {
                editorMessage = "AudioManager Warning: Scene is missing an AudioListener! Mark the listener with the \"Main Camera\" tag or set it manually!";
                print(editorMessage);
            }
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FindNewListener();
        StopSoundLoopAll(true);
    }

    /// <summary>
    /// Set whether or not sounds are 2D or 3D (spatial)
    /// </summary>
    /// <param name="b">Enable spatial sound if true</param>
    public void SetSpatialSound(bool b)
    {
        spatialSound = b;
        float val = (b) ? 1 : 0;
        foreach (AudioSource s in sources)
        {
            s.spatialBlend = val;
        }
    }

    /// <summary>
    /// Swaps the current music track with the new music track
    /// </summary>
    /// <param name="m">Index of the music</param>
    /// <param name="hasIntro">Does the clip have an intro portion that plays only once?</param>
    public void PlayMusic(string m, bool hasIntro = false)
    {
        if (m.Equals("None")) return;
        currentTrack = m;
        if (hasIntro && music[m][1] != null)
        {
            musicSource.clip = music[m][1];
            musicSource.loop = false;
        }
        else
        {
            musicSource.clip = music[m][0];
            musicSource.loop = true;
        }
        queueIntro = hasIntro;

        musicSource.Play();
    }

    /// <summary>
    /// Stop whatever is playing in musicSource
    /// </summary>
    public void StopMusic()
    {
        musicSource.Stop();
        currentTrack = "None";
    }

    private void Update()
    {
        if (queueIntro) //If we're playing the intro to a track
        {
            if (!musicSource.isPlaying) //Returns true the moment the intro ends
            {
                //Swap to the main loop
                musicSource.clip = music[currentTrack][0];
                musicSource.loop = true;
                musicSource.Play();
                queueIntro = false;
            }
        }

        if (spatialSound) // Only do this part if we have 3D sound enabled
        {
            for (int i = 0; i < audioSources; i++) // Search every sources
            {
                if (sourcePositions[i] != null) // If there's a designated location
                {
                    sources[i].transform.position = sourcePositions[i].transform.position;
                }
                if (!sources[i].isPlaying) // However if it's not playing a sound
                {
                    sourcePositions[i] = null; // Erase the designated transform so we don't check again
                }
            }
        }
    }

    /// <summary>
    /// Equivalent of PlayOneShot
    /// </summary>
    /// <param name="s"></param>
    /// <param name="trans">The transform of the sound's source</param>
    /// <param name="p">The priority of the sound</param>
    /// <param name="pitchShift">If not None, randomizes the pitch of the sound, use AudioManager.Pitches for presets</param>
    /// <param name="delay">Amount of seconds to wait before playing the sound</param>
    public void PlaySoundOnce(string s, Transform trans = null, Priority p = Priority.Default, float pitchShift = 0, float delay = 0)
    {
        AudioSource a = GetAvailableSource();

        if (trans != null)
        {
            sourcePositions[Array.IndexOf(sources, a)] = trans;
            if (spatialSound)
            {
                a.spatialBlend = 1;
            }
        }
        else
        {
            a.transform.position = listener.transform.position;
            if (spatialSound)
            {
                a.spatialBlend = 0;
            }
        }
        
        //This is the base unchanged pitch
        if (pitchShift > Pitches.None)
        {
            a.pitch = 1 + UnityEngine.Random.Range(-pitchShift, pitchShift);
        }
        else
        {
            a.pitch = 1;
        }

        //a.clip = clips[(int)s];
        a.clip = sounds[s];
        a.priority = (int)p;
        a.PlayDelayed(delay);
    }

    /// <summary>
    /// Equivalent of PlayOneShot
    /// </summary>
    /// <param name="s"></param>
    /// <param name="trans">The transform of the sound's source</param>
    /// <param name="p">The priority of the sound</param>
    /// <param name="pitchShift">If not None, randomizes the pitch of the sound, use AudioManager.Pitches for presets</param>
    /// <param name="delay">Amount of seconds to wait before playing the sound</param>
    public void PlaySoundOnce(AudioClip s, Transform trans = null, Priority p = Priority.Default, float pitchShift = 0, float delay = 0)
    {
        AudioSource a = GetAvailableSource();

        if (trans != null)
        {
            sourcePositions[Array.IndexOf(sources, a)] = trans;
            if (spatialSound)
            {
                a.spatialBlend = 1;
            }
        }
        else
        {
            a.transform.position = listener.transform.position;
            if (spatialSound)
            {
                a.spatialBlend = 0;
            }
        }

        //This is the base unchanged pitch
        if (pitchShift > Pitches.None)
        {
            a.pitch = 1 + UnityEngine.Random.Range(-pitchShift, pitchShift);
        }
        else
        {
            a.pitch = 1;
        }

        a.clip = s;
        a.priority = (int)p;
        a.PlayDelayed(delay);
    }

    /// <summary>
    /// Play a sound and loop it forever
    /// </summary>
    /// <param name="s"></param>
    /// <param name="trans">The transform of the sound's source</param>
    /// <param name="p">The priority of the sound</param>
    public void PlaySoundLoop(string s, Transform trans = null, Priority p = Priority.Default)
    {
        AudioSource a = GetAvailableSource();
        loopingSources.Add(a);
        if (trans != null)
        {
            sourcePositions[Array.IndexOf(sources, a)] = trans;
        }
        else
        {
            sourcePositions[Array.IndexOf(sources, a)] = listener.transform;
        }

        AudioSource source = loopingSources[loopingSources.Count - 1];
        //loopingSources[loopingSources.Count - 1].clip = clips[(int)s];
        source.clip = sounds[s];
        source.priority = (int)p;
        source.pitch = 1;
        source.Play();
        source.loop = true;
    }

    /// <summary>
    /// Play a sound and loop it forever
    /// </summary>
    /// <param name="s"></param>
    /// <param name="trans">The transform of the sound's source</param>
    /// <param name="p">The priority of the sound</param>
    public void PlaySoundLoop(AudioClip s, Transform trans = null, Priority p = Priority.Default)
    {
        AudioSource a = GetAvailableSource();
        loopingSources.Add(a);
        if (trans != null)
        {
            sourcePositions[Array.IndexOf(sources, a)] = trans;
        }
        else
        {
            sourcePositions[Array.IndexOf(sources, a)] = listener.transform;
        }
        AudioSource source = loopingSources[loopingSources.Count - 1];
        source.priority = (int)p;
        source.clip = s;
        source.pitch = 1;
        source.Play();
        source.loop = true;
    }

    /// <summary>
    /// Stops any sound playing through PlaySoundOnce() immediately 
    /// </summary>
    /// <param name="s">The sound to be stopped</param>
    /// <param name="t">For sources, helps with duplicate soundss</param>
    public void StopSound(string s, Transform t = null)
    {
        for (int i = 0; i < audioSources; i++)
        {
            if (sources[i].clip == sounds[s])
            {
                if (t != null)
                {
                    if (sourcePositions[i] != t) continue;
                }
                sources[i].Stop();
                return;
            }
        }
    }

    /// <summary>
    /// Stops a looping sound
    /// </summary>
    /// <param name="s"></param>
    /// <param name="stopInstantly">Stops sound instantly if true</param>
    public void StopSoundLoop(string s, bool stopInstantly = false, Transform t = null)
    {
        for (int i = 0; i < loopingSources.Count; i++)
        {
            if (loopingSources[i].clip == sounds[s])
            {
                for (int j = 0; j < sources.Length; j++) { // Thanks Connor Smiley 
                    if (sources[j] == loopingSources[i])
                    {
                        if (t != sources[j].transform)
                            continue;
                    }
                }
                if (stopInstantly) loopingSources[i].Stop();
                loopingSources[i].loop = false;
                loopingSources.RemoveAt(i);
                sourcePositions[i] = null;
                return;
            }
        }
        //Debug.LogError("AudioManager Error: Did not find specified loop to stop!");
    }

    /// <summary>
    /// Stops a looping sound
    /// </summary>
    /// <param name="s"></param>
    /// <param name="stopInstantly">Stops sound instantly if true</param>
    public void StopSoundLoop(AudioClip s, bool stopInstantly = false, Transform t = null)
    {
        for (int i = 0; i < loopingSources.Count; i++)
        {
            if (loopingSources[i].clip == s)
            {
                for (int j = 0; j < sources.Length; j++)
                { // Thanks Connor Smiley 
                    if (sources[j] == loopingSources[i])
                    {
                        if (t != sources[j].transform)
                            continue;
                    }
                }
                if (stopInstantly) loopingSources[i].Stop();
                loopingSources[i].loop = false;
                loopingSources.RemoveAt(i);
                sourcePositions[i] = null;
                return;
            }
        }
        //Debug.LogError("AudioManager Error: Did not find specified loop to stop!");
    }

    /// <summary>
    /// Stops all looping sounds
    /// </summary>
    /// <param name="stopPlaying">
    /// Stops sounds instantly if true, lets them finish if false
    /// </param>
    public void StopSoundLoopAll(bool stopPlaying = false)
    {
        if (loopingSources.Count > 0)
        {
            foreach (AudioSource a in loopingSources)
            {
                if (stopPlaying) a.Stop();
                a.loop = false;
                loopingSources.Remove(a);
            }
            if (sourcePositions != null)
                sourcePositions = new Transform[audioSources];
        }
    }

    /// <summary>
    /// Set's the volume of sounds and applies changes instantly across all sources
    /// </summary>
    /// <param name="v"></param>
    public void SetSoundVolume(float v)
    {
        soundVolume = v;
        ApplySoundVolume();
    }

    void ApplySoundVolume()
    {
        foreach (AudioSource s in sources)
        {
            s.volume = soundVolume;
        }
    }
    
    public void SetSoundVolume(UnityEngine.UI.Slider v)
    {
        soundVolume = v.value;
        foreach (AudioSource s in sources)
        {
            s.volume = soundVolume;
        }
    }
    
    public void SetMusicVolume(UnityEngine.UI.Slider v)
    {
        musicVolume = v.value;
        musicSource.volume = musicVolume;
    }

    /// <summary>
    /// A Monobehaviour function called when the script is loaded or a value is changed in the inspector (Called in the editor only).
    /// </summary>
    private void OnValidate()
    {
        if (Singleton == null)
        {
            Singleton = this;
        }
        GenerateAudioDictionarys();
        if (!doneLoading) return;
        //Updates volume
        ApplySoundVolume();
        SetSpatialSound(spatialSound);
        musicSource.volume = musicVolume;
    }

    /// <summary>
    /// Accomodates the "None" helper enum during runtime
    /// </summary>
    public void AddOffsetToArrays()
    {
        //if (clips[0] != null && clips.Count < (int)Sound.Count)
        //{
        //    clips.Insert(0, null);
        //}

        //if (tracks[0] != null && tracks.Count < (int)Music.Count)
        //{
        //    tracks.Insert(0, null);
        //}
    }

    /// <summary>
    /// Set's the volume of the music
    /// </summary>
    /// <param name="v"></param>
    public void SetMusicVolume(float v)
    {
        musicVolume = v;
        musicSource.volume = musicVolume;
    }

    /// <summary>
    /// Called externally to update slider values
    /// </summary>
    /// <param name="s"></param>
    public void UpdateSoundSlider(UnityEngine.UI.Slider s)
    {
        s.value = soundVolume;
    }

    /// <summary>
    /// Called externally to update slider values
    /// </summary>
    /// <param name="s"></param>
    public void UpdateMusicSlider(UnityEngine.UI.Slider s)
    {
        s.value = musicVolume;
    }

    /// <summary>
    /// Returns null if all sources are used
    /// </summary>
    /// <returns></returns>
    AudioSource GetAvailableSource()
    {
        foreach(AudioSource a in sources)
        {
            if (!a.isPlaying && !loopingSources.Contains(a))
            {
                return a;
            }
        }
        Debug.LogError("AudioManager Error: Ran out of Audio Sources!");
        return null;
    }

    public static AudioManager GetInstance()
    {
        return Singleton;
    }

    public float GetSoundVolume()
    {
        return soundVolume;
    }

    public float GetMusicVolume()
    {
        return musicVolume;
    }

    /// <summary>
    /// Returns true if a sound is currently being played
    /// </summary>
    /// <param name="s">The sound in question</param>
    /// <param name="trans">Specify is the sound is playing from that transform</param>
    /// <returns></returns>
    public bool IsSoundPlaying(string s, Transform trans = null)
    {
        for (int i = 0; i < audioSources; i++) // Loop through all sources
        {
            if (sources[i].clip == sounds[s] && sources[i].isPlaying) // If this source is playing the clip
            {
                if (trans != null)
                {
                    if (trans != sourcePositions[i]) // Continue if this isn't the specified source position
                    {
                        continue;
                    }
                }
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns true if a sound is currently being played by a looping sources, more efficient for looping sounds than IsSoundPlaying
    /// </summary>
    /// <param name="s">The sound in question</param>
    /// <returns>True or false you dingus</returns>
    public bool IsSoundLooping(string s) {
        foreach (AudioSource c in loopingSources)
        {
            if (c.clip == sounds[s])
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Converts a pitch enum to float value
    /// </summary>
    /// <param name="p"></param>
    /// <returns></returns>
    public static float UsePitch(Pitch p)
    {
        switch (p)
        {
            case Pitch.None:
                return Pitches.None;
            case Pitch.VeryLow:
                return Pitches.VeryLow;
            case Pitch.Low:
                return Pitches.Low;
            case Pitch.Medium:
                return Pitches.Medium;
            case Pitch.High:
                return Pitches.High;
        }
        return 0;
    }

    void GenerateAudioDictionarys()
    {
        // Create dictionary for sound
        if (transform.childCount == 0) return; // Probably script execution order issue
        if (transform.GetChild(0).childCount != sounds.Count) sounds.Clear();
        foreach (AudioFile a in transform.GetChild(0).GetComponentsInChildren<AudioFile>())
        {
            if (sounds.ContainsKey(a.name))
            {
                if (sounds[a.name].Equals(a.GetFile())) continue;
                else
                {
                    sounds[a.name] = a.GetFile();
                }
            }
            else
            {
                sounds.Add(a.name, a.GetFile());
            }
        }

        if (transform.GetChild(1).childCount != music.Count) music.Clear();
        // Create a dictionary for music
        foreach (AudioFileMusic a in transform.GetChild(1).GetComponentsInChildren<AudioFileMusic>())
        {
            if (music.ContainsKey(a.name))
            {
                if (music[a.name].Equals(a.GetFile())) continue;
                else
                {
                    music[a.name] = a.GetFile();
                }
            }
            else
            {
                music.Add(a.name, a.GetFile());
            }
        }
    }

    public string GetEditorMessage()
    {
        return editorMessage;
    }

    public Dictionary<string, AudioClip[]> GetMusicDictionary()
    {
        return music;
    }

    public Dictionary<string, AudioClip> GetSoundDictionary()
    {
        return sounds;
    }
}