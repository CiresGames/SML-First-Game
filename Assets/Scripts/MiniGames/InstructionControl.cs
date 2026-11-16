using System.Collections;
using TMPro;
using UnityEngine;

public class InstructionControl : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI instructionLabel;
    public bool isDisplayingInstruction { get; private set; } 



    public void DisplayInstruction(string text, float duration)
    {
        StartCoroutine(DisplayInstructions(text, duration));
    }


    public IEnumerator DisplayInstructions(string text, float duration)
    {
        isDisplayingInstruction = true; 

        PlayFeedbacks(true);

        instructionLabel.text = text;

        yield return new WaitForSeconds(duration);

        PlayFeedbacks(false);

        isDisplayingInstruction = false;

    }

    public void PlayFeedbacks(bool flag)
    {
        instructionLabel.gameObject.SetActive(flag);

    }



}
