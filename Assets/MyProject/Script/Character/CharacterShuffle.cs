using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Invector.vCharacterController;

public class CharacterShuffle : MonoBehaviour {

    public GameObject HumanModel;
    public GameObject AnimalModel;
    public Avatar humanAva;
    public Avatar catAva;
    CapsuleCollider capCol;

    internal float colliderRadius, colliderHeight;
    internal Vector3 colliderCenter;
    public float crouchHeightReduction = 1.5f;


    [Header("演出用の設定")]
    public GameObject ChangeEffect; // 変身用のパーティクル（プレハブ）
    public float effectDelay = 0.4f; // アニメ開始からエフェクトを出すまでの待ち時間
    public float switchDelay = 1.2f; // アニメ開始からモデルを切り替えるまでの待ち時間
    

    int modeCount = 0;
    Animator ani;
    vThirdPersonController cc;
    vThirdPersonInput tpInput;
    bool isTransforming = false;

    void Start() {
        ani = this.GetComponent<Animator>();
        capCol = this.GetComponent<CapsuleCollider>();
        cc = GetComponent<vThirdPersonController>();
        tpInput = GetComponent<vThirdPersonInput>(); // 入力を管理するコンポーネント
        colliderCenter = capCol.center;
        colliderRadius = capCol.radius;
        colliderHeight = capCol.height;
    }

    void Update() {
        foge();
    }
    void foge() {     //てきとうな関数
        if (Input.anyKeyDown) {
            {/*
            foreach (KeyCode code in Enum.GetValues(typeof(KeyCode))) {
                if (Input.GetKeyDown(code)) {
                    //処理を書く
                    if (Input.GetKey(KeyCode.JoystickButton4) || Input.GetKey(KeyCode.Q)) {
                        ModeChange();
                    }
                        Debug.Log(code);
                    break;　　　//配列をループで呼ばれているから処理が行われたらbreakでループを抜ける。
                }
            }
            */
            }
            if (Input.GetKeyDown(KeyCode.JoystickButton4) || Input.GetKeyDown(KeyCode.Q)) {
                if (!isTransforming && ani.GetBool("IsGrounded")) {
                    // 直接ModeChangeを呼ばず、コルーチンを開始する
                    StartCoroutine(ChangeSequence());
                }
            }
        }
    }

    // 変身の一連の流れを管理するコルーチン
    IEnumerator ChangeSequence() {
        isTransforming = true;
        modeCount++;
       
        if (cc != null) {
            cc.lockMovement = true;
            cc.lockRotation = true;
        }
        if (tpInput != null) {
            // 【修正案】入力を受け付けるコンポーネント自体をOFFにする
            //tpInput.enabled = false;
            tpInput.lockInput = true;
        }


        // 1. 変身アニメーションのトリガーを引く
        // Animator側で「Transforming」などのトリガーを作っておくとスムーズです
        ani.SetTrigger("Transform");

        // 2. エフェクト発生まで少し待機（例：溜めの動作中）
        yield return new WaitForSeconds(effectDelay);

        // 3. エフェクトを生成
        if (ChangeEffect != null) {
            Instantiate(ChangeEffect, transform.position + Vector3.up, Quaternion.identity);
        }

        // 4. モデルが隠れるタイミングまで待機
        yield return new WaitForSeconds(switchDelay);

        cc.isCat = modeCount % 2 == 1;

        // 5. 実際にモデルとアバターを切り替える
        if (!cc.isCat) {
            HumanModel.SetActive(true);
            AnimalModel.SetActive(false);
            ani.avatar = humanAva;
            ani.SetBool("IsCatMode", false);
        } else {
            HumanModel.SetActive(false);
            AnimalModel.SetActive(true);
            ani.avatar = catAva;
            ani.SetBool("IsCatMode", true);
        }

        // --- 最後に追加：ロックを解除 ---
        // 変身ポーズが終わるタイミングで戻す
        if (cc != null) {
            cc.lockMovement = false;
            cc.lockRotation = false;
        }
        if (tpInput != null) {
            // 【修正案】入力をONに戻す
            //tpInput.enabled = true;
            tpInput.lockInput = false;
        }
        isTransforming = false;
    }
}
