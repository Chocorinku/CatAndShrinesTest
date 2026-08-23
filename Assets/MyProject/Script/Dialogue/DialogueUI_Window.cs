using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

//１ ノベル風UI：名前表示、文字送り、選択肢対応
public class DialogueUI_Window : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private GameObject WindowBackground;       // 会話ウィンドウの親オブジェクト
    [SerializeField] private TextMeshProUGUI nameText;     // ★名前表示用テキスト
    [SerializeField] private TextMeshProUGUI dialogueText; // 本文用テキスト
    [SerializeField] private GameObject nextArrow;

    [Header("Choice Systems")]
    [SerializeField] private RectTransform choiceContainer; // ボタンを並べる親オブジェクト(VerticalLayoutGroup等を推奨)
    [SerializeField] private DialogueChoiceButton choiceButtonPrefab; // ボタンのプレハブ

    [Header("Text Settings")]
    [SerializeField] private float textSpeed = 0.02f;        // 1文字の表示速度（秒）

    [Header("=== [タスク9] 文字送りSE設定 ===")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip typeSound;
    [Tooltip("連続で鳴りすぎるのを防ぐ最小インターバル(秒)")]
    [SerializeField] private float soundInterval = 0.04f;

    // マネージャーから参照されるプロパティ（エラー解消用）
    public bool IsTyping { get; private set; } = false;     //文字が流れている最中かのフラグ
    public bool IsWaitingForChoice { get; private set; } = false;   //選択肢があるページかどうか

    private DialogueNode currentNodeData;       
    private int totalCharacters = 0;
    private Coroutine typingCoroutine;

    // GCを完全に排除するための「ボタンオブジェクトプール」
    private List<DialogueChoiceButton> choiceButtonPool = new List<DialogueChoiceButton>();

    // SEの連続再生制御用タイマー
    private float lastSoundTime = 0f;

    void Start() {
        if (WindowBackground != null) WindowBackground.SetActive(false);
        if (nameText != null) nameText.enabled = false;
        if (dialogueText != null) dialogueText.enabled = false;
        if (nextArrow != null) nextArrow.SetActive(false);
        if (choiceContainer != null) choiceContainer.gameObject.SetActive(false);

        // インスペクターで未設定の場合のセーフティキャッシュ
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    // 1 ウィンドウの初期設定
    public void SetupWindow(string npcName) {
        if (nameText != null) { nameText.enabled = true; nameText.text = npcName; }
        if (WindowBackground != null) WindowBackground.SetActive(true);
    }

    // 2 会話ノード（1ページ）の表示
    public void ShowNode(DialogueNode node) {
        currentNodeData = node;
        IsWaitingForChoice = currentNodeData.choices.Length > 0;

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        if (nextArrow != null) nextArrow.SetActive(false);
        if (choiceContainer != null) choiceContainer.gameObject.SetActive(false);

        // プール内のボタンを一旦すべて非アクティブにする（GCゼロのクリア処理）
        for (int i = 0; i < choiceButtonPool.Count; i++) {
            choiceButtonPool[i].gameObject.SetActive(false);
        }

        if (dialogueText != null) dialogueText.enabled = true;
        dialogueText.text = currentNodeData.text;
        dialogueText.ForceMeshUpdate();
        totalCharacters = dialogueText.textInfo.characterCount;
        dialogueText.maxVisibleCharacters = 0;

        typingCoroutine = StartCoroutine(TypeTextRoutine());
    }

    // 文字数を少しずつ増やしていくコルーチン
    private IEnumerator TypeTextRoutine() {
        IsTyping = true;
        int visibleCount = 0;
        lastSoundTime = 0f; // ノード開始時にリセット

        while (visibleCount < totalCharacters) {
            visibleCount++;
            dialogueText.maxVisibleCharacters = visibleCount;
            // --- [タスク9] 最速SE再生ロジック ---
            if (audioSource != null && typeSound != null) {
                // 現在表示しようとしている文字の情報をTMPのキャッシュから安全に覗き見
                int charIndex = dialogueText.textInfo.characterInfo[visibleCount - 1].index;
                char currentChar = dialogueText.text[charIndex];

                // 空白文字、改行、リッチテキストタグ（'<' から始まる文字）は鳴らさない
                if (!char.IsWhiteSpace(currentChar) && currentChar != '<') {
                    // textSpeedが極限に速い場合でも耳障りにならないよう、インターバルを保障
                    if (Time.time - lastSoundTime >= soundInterval) {
                        audioSource.PlayOneShot(typeSound);
                        lastSoundTime = Time.time;
                    }
                }
            }

            yield return new WaitForSeconds(textSpeed);
        }
        CompleteLine();
    }

    // 3 文字送りの即時完了（スキップ） 現在の行の表示完了処理
    public void CompleteLine() {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);

        dialogueText.maxVisibleCharacters = totalCharacters;    // 全文字を表示
        IsTyping = false;

        // 文字送りが終わったら、選択肢があるかどうかで分岐
        if (currentNodeData.choices != null && currentNodeData.choices.Length > 0) {
            // 連打暴発を防ぐため、即座にボタンを作らず、コルーチンを挟んで0.4秒遅らせて出現させます
            StartCoroutine(WaitAndCreateChoicesRoutine());
        } else {
            // 選択肢がない場合：通常の「次へ矢印」を表示
            if (nextArrow != null) nextArrow.SetActive(true);   // 矢印を表示
        }      
    }

    // 連打防止用のウェイトコルーチン
    private IEnumerator WaitAndCreateChoicesRoutine() {
        // 文字送り完了後、0.4秒間だけプレイヤーの連打入力をやり過ごす時間を作る
        yield return new WaitForSeconds(0.4f);

        // 選択肢がある場合：選択肢ボタンを表示し、Enter進行をロックする
        CreateChoices();
    }

    // 4 選択肢ボタンの動的生成（オブジェクトプール駆動）
    private void CreateChoices() {
        IsWaitingForChoice = true;
        if (choiceContainer == null || choiceButtonPrefab == null) return;

        choiceContainer.gameObject.SetActive(true);

        //最初のボタン（一番上）を記憶するための変数
        GameObject firstButtonObject = null;
        
        for (int i = 0; i < currentNodeData.choices.Length; i++) {
            DialogueChoice choice = currentNodeData.choices[i];
            DialogueChoiceButton buttonInstance = null;

            // プールから未使用のボタンを探して再利用
            for (int j = 0; j < choiceButtonPool.Count; j++) {
                if (!choiceButtonPool[j].gameObject.activeInHierarchy) {
                    buttonInstance = choiceButtonPool[j];
                    break;
                }
            }

            // プールに足りなければ新しく生成してプールへ追加
            if (buttonInstance == null) {
                buttonInstance = Instantiate(choiceButtonPrefab, choiceContainer);
                choiceButtonPool.Add(buttonInstance);
            }

            buttonInstance.transform.SetAsLastSibling();    // 順番を正しく並び替え
            buttonInstance.gameObject.SetActive(true);

            // ★超重要：ここで選択肢の文字と、ターゲットとなるノード番号（i ではなく choice.targetNodeIndex）を100%確実に渡す
            buttonInstance.Setup(choice.choiceText, choice.targetNodeIndex);

            // 1番最初のボタン（iが0の時）のゲームオブジェクトを記憶しておく
            if (i == 0) {
                firstButtonObject = buttonInstance.gameObject;
            }
        }

        // 覚えた1番最初のボタンを、UnityのEventSystemに強制的にフォーカス（選択）させる！
        // これにより、画面に出た瞬間にコントローラーの十字キーやスティックで上下に選べるようになります。
        if (firstButtonObject != null && UnityEngine.EventSystems.EventSystem.current != null) {
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(firstButtonObject);
        } 
    }

    // 会話UIを閉じる
    public void CloseWindow() {
        if (WindowBackground != null) WindowBackground.SetActive(false);
        if (nameText != null) nameText.enabled = false;
        if (dialogueText != null) dialogueText.enabled = false;
        if (nextArrow != null) nextArrow.SetActive(false);
        if (choiceContainer != null) choiceContainer.gameObject.SetActive(false);
        IsTyping = false;
        IsWaitingForChoice = false;

        // マネージャー側に会話終了を伝える
        DialogueManager.Instance.EndWindowDialogue();
    }
}
