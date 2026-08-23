using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class HitStop : MonoBehaviour
{
    public enum StopMode { GlobalTimeScale, PlayerOnly }
    [Header("停止モード設定")]
    [SerializeField] private StopMode mode = StopMode.GlobalTimeScale;

    [Header("停止時間（秒）")]
    [SerializeField] private float stopDuration = 0.1f; // 止める時間
    [Header("停止中の時間の進み（0に近いほど静止）")]
    [SerializeField] private float slowness = 0.02f;

    private CinemachineImpulseSource _impulseSource;

    private bool isWaiting = false;
    private Animator playerAnimator;

    private void Awake() {
        _impulseSource = GetComponent<CinemachineImpulseSource>();
    }
    private void Start() {
        // プレイヤーのAnimatorを取得（自分の親や子から探す設定）
        playerAnimator = GetComponentInParent<Animator>();
    }

    // Invector(コントローラーコンポーネントの)イベント(OnReceiveDamage)からこれを呼ぶ
    public void PlayHitStop() {
        if (isWaiting) return;

        // 【★超重要追加】ヒットストップ発動と同時にカメラシェイクを実行！
        TriggerShake(1.8f);

        if (mode == StopMode.GlobalTimeScale) {
            StartCoroutine(GlobalStopRoutine());
        } else {
            StartCoroutine(PlayerOnlyRoutine());
        }
    }
    // A: 全体が止まる（KHのトどめ、格ゲー風）
    IEnumerator GlobalStopRoutine() {
        isWaiting = true;
        float originalTimeScale = Time.timeScale;

        // 1. 時間を遅くする（物理演算も含めて一瞬止まる）
        Time.timeScale = slowness;

        // 2. 実時間(Realtime)で待機（Time.timeScaleの影響を受けない）
        yield return new WaitForSecondsRealtime(stopDuration);

        // 3. 元に戻す
        Time.timeScale = originalTimeScale;
        isWaiting = false;
    }

    // B: プレイヤーだけが止まる（モンハン、ダクソ風）
    IEnumerator PlayerOnlyRoutine() {
        if (playerAnimator == null) yield break;
        isWaiting = true;
        playerAnimator.speed = slowness;
        // プレイヤーだけ止まる場合は、通常のWaitForSecondsでOK
        yield return new WaitForSeconds(stopDuration);
        playerAnimator.speed = 1.0f;    //プレイヤーのスピードを戻す
        isWaiting = false;
    }

    /// <summary>
    /// アドバンス・ヒットストップの実行と同時にこの関数を呼び出す
    /// </summary>
    /// <param name="damageIntensity">攻撃の強さ（0.0〜1.0など）</param>
    public void TriggerShake(float damageIntensity) {
        if (_impulseSource == null) return;

        // 攻撃の強さに応じて、シェイクの振幅（強さ）を動的に変更
        // 弱攻撃: 0.2 / 強攻撃: 1.0 / 必殺技（変身技）: 2.0 など
        float amplitudeMultiplier = Mathf.Clamp(damageIntensity, 0.1f, 3.0f);

        // 2.5Dアクション特性：Z軸（奥行き）への不要なカメラ飛び出しを防ぐため、ベクトルの強さを調整
        // 画面の横（X）と縦（Y）の揺れをメインにするため、デフォルトのランダム方向をカスタム
        Vector3 customVelocity = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-0.5f, 1f),
            0f // Z軸の揺れをカット、または極小に
        ).normalized * amplitudeMultiplier;

        // Unscaled Timeで動作するImpulseを発動
        _impulseSource.GenerateImpulse(customVelocity);


        Debug.Log("シェイク関数が呼ばれました");
    }
}
