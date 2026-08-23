using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Invector.vCharacterController;
using Invector.vCamera;

public class CameraSidePan : MonoBehaviour
{
    public vThirdPersonCamera tpCamera; // メインカメラを割り当て
    public float panDistance = 2.0f;    // どのくらい先行させるか
    public float panSpeed = 1.5f;       // パンする速度
    public float thresholdTime = 1.0f;  // 何秒移動したら発動するか

    private vThirdPersonController cc;
    private float moveTimer;
    private Vector3 currentOffset;

    void Start()
    {
        cc = GetComponent<vThirdPersonController>();
        if (!tpCamera) tpCamera = vThirdPersonCamera.instance;
    }

    void Update()
    {
        // 横方向の入力（A/Dキーなど）があるかチェック
        if (Mathf.Abs(cc.input.x) > 0.1f) {
            moveTimer += Time.deltaTime;
        } else {
            moveTimer = 0;
            // 入力がない時は徐々に中央に戻る
            currentOffset = Vector3.Lerp(currentOffset, Vector3.zero, Time.deltaTime * panSpeed);
        }

        // 一定時間以上移動したら、入力方向にオフセットを計算
        if (moveTimer >= thresholdTime) {
            float targetX = (cc.input.x > 0) ? panDistance : -panDistance;
            Vector3 targetOffset = new Vector3(targetX, 0, 0);
            currentOffset = Vector3.Lerp(currentOffset, targetOffset, Time.deltaTime * panSpeed);
        }

        // ★ここが重要：Invectorカメラにオフセットを適用する
        // ※ vThirdPersonCameraのCustomLookAtなどを利用するか、
        // 直接カメラのトランスフォームに加算する処理をここに書きます
        //tpCamera.additionalTargetStackedOffset = currentOffset;
    }
}
