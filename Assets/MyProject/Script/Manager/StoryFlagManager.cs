using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StoryFlagManager : MonoBehaviour
{
    public static StoryFlagManager Instance { get; private set; }

    // enumの総数分の配列を確保（自動で最大値を取得してクリーンに初期化）
    private bool[] flagStates;

    private void Awake() {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); } else { Destroy(gameObject); }

        InitializeFlags();
    }

    private void InitializeFlags() {
        // StoryFlagTypeの項目数を数えて、ジャストサイズの配列を1度だけ生成（GC最小限）
        int flagCount = System.Enum.GetValues(typeof(StoryFlagType)).Length;
        flagStates = new bool[flagCount];
    }

    /// <summary>
    /// フラグの状態を書き換える（最速・GCゼロ）
    /// </summary>
    public void SetFlag(StoryFlagType flag, bool state) {
        if (flag == StoryFlagType.None) return;

        int index = (int)flag;
        if (index >= 0 && index < flagStates.Length) {
            flagStates[index] = state;
            Debug.Log($"[FlagChanged] {flag} -> {state}");
        }
    }

    /// <summary>
    /// フラグの状態を確認する（最速 * 毎フレーム呼んでもGCゼロ）
    /// </summary>
    public bool GetFlag(StoryFlagType flag) {
        if (flag == StoryFlagType.None) return false;

        int index = (int)flag;
        if (index >= 0 && index < flagStates.Length) {
            return flagStates[index];
        }
        return false;
    }

    /// <summary>
    /// すべてのフラグをリセットする（ニューゲーム用）
    /// </summary>
    public void ResetAllFlags() {
        if (flagStates == null) return;
        System.Array.Clear(flagStates, 0, flagStates.Length);
    }

    // ★フェーズ3のセーブシステム実装時に、この配列をそのままEasy Save 3に1行で丸投げして保存できます
    public bool[] GetRawFlagArray() => flagStates;
    public void SetRawFlagArray(bool[] loadedFlags) { if (loadedFlags != null) flagStates = loadedFlags; }

    /// <summary>
    /// 【インスペクター専用】引数を1つ（enumのみ）にすることで、UnityEventから選択できるようにした橋渡し関数。呼び出されると自動でTrueにします。
    /// </summary>
    public void SetFlagTrueFromInspector(StoryFlagType flag) {
        SetFlag(flag, true);
    }

    /// <summary>
    /// 【int型 回避策】UnityEventに100%確実に表示させるためのint引数版。
    /// </summary>
    public void SetFlagTrueFromInspectorInt(int flagIndex) {
        // 数値を安全にenumにキャストしてTrueにする（GCゼロ）
        SetFlag((StoryFlagType)flagIndex, true);
    }
}
