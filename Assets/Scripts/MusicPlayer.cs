using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    public static MusicPlayer Instance;

    private AudioSource _audio;

    private void Awake()
    {
        // Singleton: if a MusicPlayer already exists, kill this duplicate
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _audio = GetComponent<AudioSource>();

        // Only start playing if it's not already playing
        if (_audio != null && !_audio.isPlaying)
        {
            _audio.loop = true;      // if you want looping
            _audio.Play();
        }
    }
}