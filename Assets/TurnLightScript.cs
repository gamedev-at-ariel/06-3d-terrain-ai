using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnLightScript : MonoBehaviour{

    public GameObject light;
    public GameObject bulb;

    private bool isOn = false;

    void OnTriggerStay(Collider other)
    {
        if (other.tag == "Player" && Input.GetKeyDown(KeyCode.E) && !isOn)
        {
            light.SetActive(true);
            isOn = true;
            bulb.SetActive(true);

            // Debug.Log("on");
        }
        else if (other.tag == "Player" && Input.GetKeyDown(KeyCode.E) && isOn)
        {
            light.SetActive(false);
            isOn = false;
            bulb.SetActive(false);

            //  Debug.Log("off");

        }
    }
}
