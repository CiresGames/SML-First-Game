using System.Collections;
using UnityEngine;

public class MG_Test : MiniGameControl
{

    private void Update()
    {
        if (CheckGameSuccess() && isGameRunning)
        {
            Debug.Log("Mini Game Success!");
        }



    }




    public  override IEnumerator StartGame()
    {
        SetIsGameRunning(true);
        Debug.Log("Starting Test Mini Game");
        yield return new WaitForSeconds(GetDuration());
        SetIsGameRunning(false);
        Debug.Log("Test Mini Game Ended");
    }


    public override bool CheckGameSuccess()
    {
        if (GetInputScheme().asset.FindAction("TestAction").triggered)
        {
            Debug.Log("Test Action Triggered");
            return true;
        }

        else return false;
    }

}
