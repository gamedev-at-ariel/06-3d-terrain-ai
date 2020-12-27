using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrenadeScript : MonoBehaviour
{
    [SerializeField] GameObject explosionEffect;
    [SerializeField] float delay = 3f;
    [SerializeField] float explosionForce = 10f;
    [SerializeField] float radius = 20f;

    private float startingTime;
    // Start is called before the first frame update
    void Start()
    {
        startingTime = Time.deltaTime;
    }

    void Update()
    {
        startingTime += Time.deltaTime;
        if(startingTime >= delay)
        {
            Explode();
        }
    }

    // Update is called once per frame

    private void Explode()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, radius);

        foreach(Collider near in colliders)
        {
            if(near.tag == "Enemy") {
                Debug.Log(near.name+" is DEAD!");
                Destroy(near.gameObject);
            }
        }
        //Instantiate(explosionEffect,transform.position,transform.rotation);
        Destroy(gameObject);
    }
}
