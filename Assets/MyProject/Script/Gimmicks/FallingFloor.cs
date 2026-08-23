using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FallingFloor : MonoBehaviour, IGimmick
{
    [Header("落下の設定")]
    [SerializeField] private float shakeTime = 1.0f;    // 揺れる（待つ）時間
    [SerializeField] private float shakeIntensity = 0.05f; // 揺れの強さ
    [SerializeField] private float destroyTime = 3.0f;  // 消えるまでの時間
    [SerializeField] private float floorMass = 1.0f;    // 床の重さ

    [Header("起動エフェクト")]
    [SerializeField] private ParticleSystem dustEffect; // 起動時のエフェクト
    [Header("エフェクトが終わる時間")]
    [SerializeField] private float effectEnd = 2.0f;
    // ★追加：インスペクターから微調整できるようにする
    [SerializeField] private Vector3 effectOffset = new Vector3(0, -0.5f, 0);

    private bool _isActivated = false;
    private Vector3 _originalPos;

    public void OnActivate() {
        // ここに床が落ちる処理を書く
        if (_isActivated) return;
        _originalPos = transform.position; // 初期位置を記録
        StartCoroutine(FallRoutine());
        
    }
    IEnumerator FallRoutine() {
        _isActivated = true;
        // 生成場所を床と同じ位置、回転にする
        if (dustEffect != null) {
            
            Vector3 spawnPos = transform.position + effectOffset;
            ParticleSystem newParticle = Instantiate(dustEffect, spawnPos, dustEffect.transform.rotation, transform);

            newParticle.Play();
            Debug.Log("エフェクト生成完了: " + newParticle.name);
            Destroy(newParticle.gameObject, effectEnd);
        } else {
            Debug.LogWarning("dustEffectがアサインされていません！");
        }
        // 2. 揺れる演出
        float elapsed = 0f;
        while (elapsed < shakeTime) {
            // ランダムに座標をずらす
            float x = Random.Range(-1f, 1f) * shakeIntensity;
            float z = Random.Range(-1f, 1f) * shakeIntensity;

            transform.position = new Vector3(_originalPos.x + x, _originalPos.y, _originalPos.z + z);

            elapsed += Time.deltaTime;
            yield return null; // 1フレーム待機
        }
        // 落下前に位置を一度戻すと、不自然なズレを防げます
        transform.position = _originalPos;

        //yield return new WaitForSeconds(shakeTime);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) {
            rb.mass = floorMass;
            rb.useGravity = true;
            rb.isKinematic = false;
            Debug.Log("床が落ちました！");
        }
        
        Destroy(gameObject, destroyTime);
    }
}
