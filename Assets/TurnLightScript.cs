using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnLightScript : MonoBehaviour{
    
    public GameObject light;
    private bool isOn = false;

    void OnTriggerStay(Collider other)
    {
        if (other.tag == "Player" && Input.GetKeyDown(KeyCode.E) && !isOn)
        {
            light.SetActive(true);
            isOn = true;
           // Debug.Log("on");
        }
        else if (other.tag == "Player" && Input.GetKeyDown(KeyCode.E) && isOn)
        {
            light.SetActive(false);
            isOn = false;
          //  Debug.Log("off");

        }
    }
}
