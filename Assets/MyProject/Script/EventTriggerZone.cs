using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventTriggerZone : MonoBehaviour
{
    [Header("--- 触れた時に実行するゲームイベント ---")]
    [Tooltip("GameEventManagerに登録したイベント名（GetCoinなど）を叩く場合は入力")]
    [SerializeField] private string targetEventName = "GetFirstCoin";

    [SerializeField] private bool destroyOnTrigger = true;

    [Header("--- [追加] 特定のフラグがONなら自動で消滅する設定 ---")]
    [Tooltip("このフラグがすでにTrue（ON）の場合、ゲーム開始時にこのオブジェクトを自動で消去します")]
    [SerializeField] private StoryFlagType destroyIfFlagIsTrue = StoryFlagType.None;

    private void Start() {
        // ★ゲーム開始時（またはシーン読み込み時）に世界の記憶ノートをチェック
        if (destroyIfFlagIsTrue != StoryFlagType.None && StoryFlagManager.Instance != null) {
            if (StoryFlagManager.Instance.GetFlag(destroyIfFlagIsTrue)) {
                Debug.Log($"[ObjectAutoDestroy] {gameObject.name} はフラグ {destroyIfFlagIsTrue} がONのため、自動消滅しました。");
                Destroy(gameObject); // すでに用済みなので消える（GCゼロ）
            }
        }
    }
    private void OnTriggerEnter(Collider other) {
        if (!other.CompareTag("Player")) return;

        // 共通イベント基地に「このイベント名を実行して！」とサイン（電波）を送るだけ
        if (!string.IsNullOrEmpty(targetEventName) && GameEventManager.Instance != null) {
            GameEventManager.Instance.TriggerEvent(targetEventName);
        }

        if (destroyOnTrigger) {
            Destroy(gameObject);
        }
    }
}
