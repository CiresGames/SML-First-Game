using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MiniGameManager : MonoBehaviour
{

    [SerializeField] List<MiniGameControl> miniGames;
    private int currentMiniGameIndex;


    public void InitializeMiniGame()
    {
        MiniGameControl currentMiniGame = GetCurrentMiniGame();
        currentMiniGame.gameObject.SetActive(true);

    }  
    

    
    public MiniGameControl GetCurrentMiniGame()
    {
        return miniGames[currentMiniGameIndex];
    }

    public MiniGameControl GetNextMiniGame()
    {
        return miniGames[(currentMiniGameIndex + 1) % miniGames.Count];
    }

    public MiniGameControl GetPreviousMiniGame()
    {
        return miniGames[(currentMiniGameIndex - 1 + miniGames.Count) % miniGames.Count];
    }

}
