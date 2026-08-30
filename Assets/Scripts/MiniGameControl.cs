using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

public class MiniGameControl : MonoBehaviour
{

    public string miniGameName { get; private set; }
    public string instruction { get; private set; }
    public float duration { get; private set; }
    public float instructionDuration { get; private set; }
    public InputActionMap inputScheme { get; private set; }
    public bool isGameRunning { get; private set; }
    public bool isGameSuccess { get; private set; }


    public virtual void StartMiniGame()
    {
        RunGame();
    }


    public virtual IEnumerator RunGame()
    {
        Debug.Log($"Starting mini-game: {miniGameName}");
        yield return null; 
    }

   
    public virtual IEnumerator DisplayInstructions(TextMeshProUGUI label, float duration)
    {
        label.gameObject.SetActive(true);
        label.text = instruction;
        
        yield return new WaitForSeconds(duration);
        
        label.gameObject.SetActive(false);
    
    }


    


}
