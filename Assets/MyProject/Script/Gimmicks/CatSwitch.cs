using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CatSwitch : MonoBehaviour
{
    [SerializeField] private GameObject targetObject = null;
    private void Start() {
        if(targetObject == null) {
            targetObject = GameObject.Find("FootSwitch");
            Debug.Log("ターゲットが空だったので、自動的に FootSwitch を探しました！");
        }
    }
    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player")) {
            Animator ani = other.GetComponent<Animator>();
            if (ani != null && ani.GetBool("IsCatMode")) {
                Debug.Log("猫がスイッチを押した！扉を開ける処理をここに書く");
                //transform.position += new Vector3(transform.position.x, -1f, transform.position.z);
                ActivateGimmick();
            }
        }
    }

    void ActivateGimmick() {
        if (targetObject != null) {
            // 相手が「IGimmick」という規格を持っているかチェック
            IGimmick gimmick = targetObject.GetComponent<IGimmick>();

            if(gimmick != null) {
                gimmick.OnActivate();   // 直接実行（これが一番速い！）
            }
        }
    }
}
