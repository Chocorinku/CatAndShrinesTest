using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GameEventManager : MonoBehaviour
{
    public static GameEventManager Instance { get; private set; }

    [System.Serializable]
    public struct CustomGameEvent {
        [Tooltip("イベントの識別名（例：GetCoin, BossTrigger など）")]
        public string eventName;

        [Header("--- [連動1] 自動でONにするストーリーフラグ ---")]
        [Tooltip("このイベントが発生した時に、ドロップダウンで指定したフラグを自動でTrueにします")]
        public StoryFlagType flagToSetTrue;

        [Header("--- [連動2] その他実行したいイベント（お金追加など） ---")]
        [Tooltip("お金の追加や演出など、追加のアクションがあればここに登録")]
        public UnityEvent additionalActions;
    }

    [Header("=== [ゲーム内イベントの一元管理リスト] ===")]
    [SerializeField] private CustomGameEvent[] globalEvents;

    private void Awake() {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); } else { Destroy(gameObject); }
    }

    /// <summary>
    /// ゲーム内のどこからでも、名前を指定するだけで登録されたイベントを物理的に実行する
    /// </summary>
    public void TriggerEvent(string nameOfEvent) {
        if (globalEvents == null || globalEvents.Length == 0) return;

        for (int i = 0; i < globalEvents.Length; i++) {
            if (globalEvents[i].eventName == nameOfEvent) {

                // 1. 指定されたストーリーフラグを安全にドロップダウンから読み取って自動ON
                StoryFlagType flag = globalEvents[i].flagToSetTrue;
                if (flag != StoryFlagType.None && StoryFlagManager.Instance != null) {
                    StoryFlagManager.Instance.SetFlag(flag, true);
                    Debug.Log($"<color=cyan>[FlagSuccess] 管制塔経由で {flag} がONになりました！</color>");
                }

                // 2. その他の追加アクション（お金追加など）があれば実行
                globalEvents[i].additionalActions?.Invoke();

                Debug.Log($"<color=yellow>[EventTriggered] イベント「{nameOfEvent}」の全処理を完了しました</color>");
                return;
            }
        }
        Debug.LogWarning($"[EventWarning] イベント「{nameOfEvent}」は見つかりませんでした");
    }
}
