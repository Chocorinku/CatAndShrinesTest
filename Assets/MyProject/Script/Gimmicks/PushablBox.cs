using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PushablBox : MonoBehaviour
{
    Rigidbody rb;
    float nonPush = 1000f;
    public float isPush = 5f;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnCollisionStay(Collision collision) {
        if (collision.gameObject.CompareTag("Player")) {
            Animator ani = collision.gameObject.GetComponent<Animator>();
            bool isCat = ani.GetBool("IsCatMode");

            if (isCat) {
                rb.mass = nonPush;
            } else {
                rb.mass = isPush;
            }
        }
    }
    private void OnCollisionExit(Collision collision) {
        if (collision.gameObject.CompareTag("Player")) {
            rb.mass = nonPush;
        }
    }
}
