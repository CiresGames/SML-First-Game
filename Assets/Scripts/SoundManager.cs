using UnityEngine;
using System.Collections.Generic;
public class SoundManager : MonoBehaviour
{
    [SerializeField] Transform content; 
    [SerializeField] ItemSoundControl soundItemPrefab;
    [SerializeField] List<SoundSO> soundList;


    public void CreateSoundItems(List<SoundSO> soundList)
    {
        foreach(SoundSO sound in soundList)
        {
            ItemSoundControl soundObject = Instantiate(soundItemPrefab, content);
            soundObject.soundSo = sound;
            soundObject.InitializeSO(sound);
        }
    }

    private void Start()
    {
        CreateSoundItems(soundList);

    }



}
