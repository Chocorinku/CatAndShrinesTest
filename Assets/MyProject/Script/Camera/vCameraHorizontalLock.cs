using Invector.vCamera;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class vCameraHorizontalLock : MonoBehaviour {

    private vThirdPersonCamera vCam;
    private float lockedX;
    private float startReturnX;                 // 復帰を開始した瞬間のX座標
    private bool isReturning = false;           // 復帰中かどうか
    private bool wasLockedLastFrame = false;    // エリア内にいたかどうかの記録

    [Header("復帰設定")]
    [SerializeField] private float returnDuration = 1.5f;       // 何秒かけて戻るか
    [SerializeField] private AnimationCurve returnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // 動きに緩急をつける
    private float lerpTimer = 0f;
    [SerializeField] private float stabilizationTime = 2.0f;    //カメラが戻る時に安定させる数値（１より高い数値にしないと変になる）


    void Start() {
        vCam = GetComponent<vThirdPersonCamera>();
        lockedX = transform.position.x;
    }

    // Invectorの追従処理(LateUpdate)が走った直後に、X座標だけを上書きして戻します
    void LateUpdate() {
        if (vCam == null || vCam.mainTarget == null) return;

        if (vCam.SaidLockTrigger) {                     //制限エリアに入った合図
            // --- ロック中 ---
            if (!wasLockedLastFrame) {
                lockedX = transform.position.x; // 現在のXをロック位置として保存

                vCam.ChangeState("SaidLock", true);
                wasLockedLastFrame = true;
                isReturning = false; // 復帰モードをリセット
            }
            ApplyX(lockedX);
            return; // ロック中はここで処理終了
        }
        // 2.エリアから出た瞬間
        if (wasLockedLastFrame && !vCam.SaidLockTrigger) {
            // ★ポイント：transform.positionではなく「固定していたlockedX」を起点にする
            startReturnX = lockedX;
            isReturning = true;
            lerpTimer = 0f;
            wasLockedLastFrame = false; // フラグを下ろす 

            //vCam.ChangeState("Default", true);
        }
        // 3. スムーズな復帰処理
        if (isReturning) {
            lerpTimer += Time.deltaTime;
            float t = Mathf.Clamp01(lerpTimer / returnDuration);
            // AnimationCurveを適用して、より自然な緩急をつける
            float curveT = returnCurve.Evaluate(t);

            // 目標は常に「今のプレイヤーのX座標」
            float targetX = vCam.mainTarget.position.x;

            // 完全に固定値(startReturnX)から補間を開始する
            float smoothX = Mathf.Lerp(startReturnX, targetX, curveT);

            ApplyX(smoothX);

            // ほぼ重なったら終了（バトンタッチ時の衝撃をゼロにする）
            if (t >= stabilizationTime) {
                isReturning = false;
            }
        }
    }
    private void ApplyX(float x) {
        Vector3 currentPos = transform.position;
        currentPos.x = x;
        transform.position = currentPos;
    }
}


