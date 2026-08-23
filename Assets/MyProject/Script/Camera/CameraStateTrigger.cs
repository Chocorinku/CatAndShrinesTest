using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Invector.vCamera; // Invectorのカメラ機能を使うために必要

public class CameraStateTrigger : MonoBehaviour
{
    // シーン上のメインカメラ（vThirdPersonCamera）をアサイン
    public vThirdPersonCamera tpCamera;

    private void OnTriggerEnter(Collider other) {
        if ((tpCamera != null)&& other.tag == "Player") {
            tpCamera.SaidLockTrigger = true;
            Debug.Log("player入った");
        }
    }
    private void OnTriggerExit(Collider other) {
        if ((tpCamera != null) && other.tag == "Player") {
            tpCamera.SaidLockTrigger = false;
            //tpCamera.ChangeState("SaidLock", false);
            Debug.Log("playerでた");
        }
    }
}
