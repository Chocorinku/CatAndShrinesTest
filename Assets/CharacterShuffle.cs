using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterShuffle : MonoBehaviour {

    public GameObject HumanModel;
    public GameObject AnimalModel;
    public Avatar humanAva;
    public Avatar catAva;
    CapsuleCollider capCol;

    int modeCount = 0;
    Animator ani;

    void Start() {
        ani = this.GetComponent<Animator>();
        capCol = this.GetComponent<CapsuleCollider>();
    }

    void Update() {
        foge();
    }
    void foge() {     //てきとうな関数
        if (Input.anyKeyDown) {
            foreach (KeyCode code in Enum.GetValues(typeof(KeyCode))) {
                if (Input.GetKeyDown(code)) {
                    //処理を書く
                    if (Input.GetKey(KeyCode.JoystickButton4) || Input.GetKey(KeyCode.Q)) {
                        ModeChange();
                    }
                        Debug.Log(code);
                    break;　　　//配列をループで呼ばれているから処理が行われたらbreakでル　　　　　　　　　　　　　　　　　　　　ープを抜ける。
                }
            }
        }
    }

    public bool aniMode;
    void ModeChange() {     //モードチェンジ
        modeCount++;
        if (modeCount % 2 != 1) {
            //人間モード
            HumanModel.SetActive(true);
            AnimalModel.SetActive(false);
            ani.avatar = humanAva;
            ani.SetBool("IsCatMode", false);
            aniMode = false;
        } else {
            //動物モード
            HumanModel.SetActive(false);
            AnimalModel.SetActive(true);
            ani.avatar = catAva;
            ani.SetBool("IsCatMode", true);
            aniMode = true;
        }
    }
}
