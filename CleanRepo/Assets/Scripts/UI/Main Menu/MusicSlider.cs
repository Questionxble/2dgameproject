using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class MusicVolumeControl : MonoBehaviour
{
    public AudioMixer mixer;
    public Slider volumeSlider;

    void Start()
    {

        // Initialize Volume to be 0 upon load
        mixer.SetFloat("MusicVolume", -1000);

        volumeSlider.onValueChanged.AddListener(SetVolume);
    }

    void SetVolume(float value)
    {
        // Convert slider (0–1) to decibels
        mixer.SetFloat("MusicVolume", value == 0 ? -1000 : Mathf.Log10(value) * 20);
    }
}
