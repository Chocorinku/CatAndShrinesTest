//== GameEnum.cs ====
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ゲーム内で使用するすべてのフラグ（分岐条件）をここに書き足していきます
public enum StoryFlagType {
    None = 0,

    // --- チャプター1 のフラグ例 ---
    Ch1_TalkedToVillageChief = 1,  // 村長に話しかけた
    Ch1_AcceptedQuest = 2,  // クエストを引き受けた
    Ch1_DefeatedSlime = 3,  // スライムを倒した
    Ch1_QuestReported = 4,  // クエストを報告して完了した

    // --- 今後必要なフラグをここにポチポチ追加するだけで拡張可能 ---
    Ch2_OpenedSecretDoor = 5,
}

