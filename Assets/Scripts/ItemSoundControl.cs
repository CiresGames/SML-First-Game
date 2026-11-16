using JetBrains.Annotations;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.UI;

public class ItemSoundControl : MonoBehaviour
{

    public SoundSO soundSo;
    [SerializeField] AudioSource player;
    [SerializeField] Image image;
    [SerializeField] GameObject playState; 
    [SerializeField] Button button; 


    public void InitializeSO(SoundSO soundSO)
    {
        image.sprite = soundSo.sprite;
        player.clip = soundSo.clip; 

    }

    private void Start()
    {
        //InitializeSO(soundSo);
        button.onClick.AddListener(PlayAudio);
        PlayIconDisplay(IsPlaying()); 
    }

    private bool IsPlaying()
    {
        bool isPlaying = player.isPlaying;
        return isPlaying; 
    }


    private void PlayIconDisplay(bool flag)
    {
        playState.SetActive(flag);

    }

    private void Update()
    {
        PlayIconDisplay(IsPlaying());
    }

    private void PlayAudio()
    {
        if (!IsPlaying())
            player.Play();
        else player.Stop(); 
    }





}
