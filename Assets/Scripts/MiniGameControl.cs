using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public abstract class MiniGameControl : MonoBehaviour
{

    [SerializeField] private string miniGameName; 
    [SerializeField] private string instruction; 
    [SerializeField] private float duration;
    [SerializeField] private InputActionMap inputScheme; 
    public bool isGameRunning { get; private set; }



    public virtual void InitializeGame()
    {
        isGameRunning = false;
        StartCoroutine(StartGame());

    }


    public abstract IEnumerator StartGame();


    public abstract bool CheckGameSuccess();


    #region Getter Methods
    public string GetMiniGameName()
    {
        return miniGameName;
    }
    public string GetInstruction()
    {
        return instruction;
    }

    public float GetDuration()
    {
        return duration;
    }
    public InputActionMap GetInputScheme()
    {
        return inputScheme;
    }

    public void SetIsGameRunning(bool value)
    {
        isGameRunning = value;
    }

    #endregion

}
