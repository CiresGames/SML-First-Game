using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.UI;

public class ItemSoundControl : MonoBehaviour
{

    [SerializeField] SoundSO soundSo;
    [SerializeField] MMF_Player player;
    [SerializeField] Image image; 


    private void InitializeSO(SoundSO soundSO)
    {
        image.sprite = soundSo.sprite;
        player.GetFeedbackOfType<MMF_Sound>().Sfx = soundSo.clip; 

    }

    private void Start()
    {
        InitializeSO(soundSo); 
    }




}
