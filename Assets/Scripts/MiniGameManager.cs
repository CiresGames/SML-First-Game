using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MiniGameManager : MonoBehaviour
{

    [SerializeField] List<MiniGameControl> miniGames;
    private int currentMiniGameIndex;

    [SerializeField] private InstructionControl instructionControl;

   
    
    public void StartMiniGame(MiniGameControl miniGame)
    {
        currentMiniGameIndex = miniGames.IndexOf(miniGame);
        if (currentMiniGameIndex != -1)
        {

        }

        else
        {
            Debug.LogError("Mini-game not found in the list.");
        }
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
