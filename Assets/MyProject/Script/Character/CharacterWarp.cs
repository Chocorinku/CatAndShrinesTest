using Invector.vCamera;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterWarp : MonoBehaviour {

    public GameObject PlayerPrefab;
    //public GameObject ThirdPersonCamera;
    Transform rootTransform;
    public vThirdPersonCamera vT;
    
    void Start() {
        //vT = FindObjectOfType<vThirdPersonCamera>();
    }

    void Update() {
        foge();
    }

    void foge() {   　　//てきとうな関数
        GameObject obj = transform.root.gameObject;
        rootTransform = obj.transform;
        if (Input.anyKeyDown) {
            foreach (KeyCode code in Enum.GetValues(typeof(KeyCode))) {
                if (Input.GetKeyDown(code)) {
                    //処理を書く
                    if (Input.GetKey(KeyCode.JoystickButton5)) {
                        Debug.Log("uniko");
                        transform.root.GetComponent<Collider>().enabled = false;
                        PlayerPrefab.SetActive(true);
                        PlayerPrefab.GetComponent<Collider>().enabled = true;
                        PlayerPrefab.transform.position = rootTransform.position;
                        PlayerPrefab.transform.rotation = rootTransform.rotation;
                        obj.SetActive(false);

                        vT.mainTarget = PlayerPrefab.transform;
                    }
                    Debug.Log(code);
                    break;　　　//配列をループで呼ばれているから処理が行われたらbreakでル　　　　　　　　　　　　　　　　　　　　ープを抜ける。
                }
            }
        }
    }
}
