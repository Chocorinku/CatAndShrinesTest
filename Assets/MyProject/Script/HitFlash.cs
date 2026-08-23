using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HitFlash : MonoBehaviour
{
    [Header("点滅させる設定")]
    [SerializeField] private Color flashColor = Color.white; // 点滅時の色（白が基本）
    [SerializeField] private float flashDuration = 0.1f;    // 点滅している時間

    // 敵の見た目を管理するレンダラー
    private Renderer[] _renderers;
    private Color[] _originalColors;
    private Coroutine _flashCoroutine;

    private void Awake() {
        // 敵のオブジェクト、およびその子オブジェクトにある全てのRenderer（見た目の部品）を取得
        _renderers = GetComponentsInChildren<Renderer>();

        // 元の色を記憶するための配列を用意
        _originalColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) {
            // マテリアルに「_Color」という標準プロパティがあれば取得（一般的なマテリアルはほぼ対応）
            if (_renderers[i].material.HasProperty("_Color")) {
                _originalColors[i] = _renderers[i].material.color;
            }
        }
    }

    /// <summary>
    /// 敵がダメージを受けた瞬間にこの関数を呼び出す
    /// </summary>
    public void Flash() {
        if (_renderers == null || _renderers.Length == 0) return;

        // すでに点滅中の場合は一度強制停止して新しくやり直す（連撃対策）
        if (_flashCoroutine != null) {
            StopCoroutine(_flashCoroutine);
            ResetColors();
        }

        _flashCoroutine = StartCoroutine(FlashRoutine());
    }

    // UnscaledTimeで動作するコルーチン（ヒットストップ中も点滅アニメを止めないため）
    private IEnumerator FlashRoutine() {
        // 1. 全てのパーツを一時的にフラッシュ色（白など）に変える
        for (int i = 0; i < _renderers.Length; i++) {
            if (_renderers[i].material.HasProperty("_Color")) {
                _renderers[i].material.color = flashColor;
            }
        }

        // 2. ヒットストップの時間停止（Time.timeScale=0）の影響を受けずに指定秒数待つ
        yield return new WaitForSecondsRealtime(flashDuration);

        // 3. 元の色に戻す
        ResetColors();
        _flashCoroutine = null;
    }

    private void ResetColors() {
        for (int i = 0; i < _renderers.Length; i++) {
            if (_renderers[i].material.HasProperty("_Color")) {
                _renderers[i].material.color = _originalColors[i];
            }
        }
    }

    // 念のため、ゲームを途中で終了した際にマテリアルが白いまま保存されるのを防ぐ
    private void OnDisable() {
        if (_flashCoroutine != null) {
            ResetColors();
        }
    }
}
