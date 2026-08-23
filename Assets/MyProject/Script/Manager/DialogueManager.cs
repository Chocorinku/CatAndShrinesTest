// == DialogueManager.cs ==
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Invector.vCharacterController; // Invectorの機能を使用
using Cinemachine;
using UnityEngine.Events;

//全体の進行管理・プレイヤーの完全ロック
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("Cinemachine Camera Settings")]
    [Tooltip("会話中にカメラ演出（バーチャルカメラの切り替え）を使用するかどうか")]
    [SerializeField] private bool useCameraDirector = true;
    [Tooltip("会話専用のCinemachineバーチャルカメラの参照")]
    [SerializeField] private Cinemachine.CinemachineVirtualCamera dialogueVirtualCamera; // Cinemachineのコンポーネント、またはGameObject型
    [Tooltip("会話中のカメラ優先度")]
    [SerializeField] private int activePriority = 20;
    [Tooltip("会話終了時の通常カメラ優先度")]
    [SerializeField] private int defaultPriority = 12;


    [Header("UI References")]
    [SerializeField] private DialogueUI_Window windowUI;
    [SerializeField] private DialogueUI_Bubble bubbleUI;

    [Header("Player Settings")]
    private vThirdPersonInput playerInput;
    public vThirdPersonInput PlayerInput => playerInput;
    private Rigidbody playerRb;
    private Animator playerAnim;
    // 外部のNPCからプレイヤーのアニメーターを安全に参照するためのプロパティ（GCゼロ）
    public Animator PlayerAnimator => playerAnim;

    //「ゲームがいま、会話中かどうか」を全システムに知らせるための安全弁（フラグ）
    //ゲーム開始時の初期値として「会話中ではない（false）」を代入しています
    public bool IsInDialogue { get; private set; } = false;

    // 現在アクティブなUIとノードデータの管理用
    private DialogueUI_Window activeWindow;
    private DialogueNode[] currentNodes;
    private int currentNodeIndex = 0;

    private float inputGuardTimer = 0f; // 会話終了直後のボタン暴発を防ぐタイマー
    private float cooldownTime = 0.2f;

    private StandaloneInputModule standardModule;

    // ★新しく上の方（変数宣言の場所）に1行だけ追加
    private bool isGamepadMode = false;

    private Coroutine lookAtCoroutine;  //回転用コルーチンを管理するための変数

    [Header("lookAt Settings (NPCの方向を向く時に何秒かけと、回転の滑らかさ)")]
    [SerializeField] float duration = 0.4f; // ★何秒かけてNPCの方を向かせるか（0.4秒でフワッと回る）
    [SerializeField] float rotateSpeed = 15f; // 回転の滑らかさの倍率

    private void Awake() {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject);
        }
    }

    void Start() {
        // プレイヤーのキャッシュ（Invectorコンポの取得）
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) {
            playerInput = player.GetComponent<vThirdPersonInput>();
            playerRb = player.GetComponent<Rigidbody>();
            playerAnim = player.GetComponent<Animator>();
        }

        // Start時点でのキャッシュ（Nullチェック付き）
        CacheInputModule();
    }
    // インプットモジュールを安全にキャッシュする関数
    private void CacheInputModule() {
        if (standardModule == null && EventSystem.current != null) {
            standardModule = EventSystem.current.GetComponent<StandaloneInputModule>();
        }
    }
    private void Update() {
        // スティックの傾き、または十字キー・ボタンのいずれかが押されているかをチェック
        bool hasGamepadInput =
            Mathf.Abs(Input.GetAxisRaw("LeftAnalogVertical")) > 0.1f ||
            Mathf.Abs(Input.GetAxisRaw("LeftAnalogHorizontal")) > 0.1f ||
            Input.GetKeyDown(KeyCode.JoystickButton0) || // Aボタン
            Input.GetKeyDown(KeyCode.JoystickButton1) || // Bボタン
            Input.GetKeyDown(KeyCode.JoystickButton2) || // Xボタン
            Input.GetKeyDown(KeyCode.JoystickButton3) || // Yボタン
            Input.GetKeyDown(KeyCode.JoystickButton7);   // スタートボタン（ポーズ用等）
        
        // ゲームパッドの入力が少しでもある、またはAボタン連打中なら確実にパッドモードを維持
        if (hasGamepadInput) {
            isGamepadMode = true;
        }
        // --- 2. キーボード・マウスの入力を検知 ---
        // パッドの入力が【完全に何も検知されていない時だけ】キーボードの入力を判定する
        else {
            // 1. キーボード・マウスの入力を検知（WASD、矢印キー、マウス移動、マウスクリック）
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) ||
            Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || // ★EnterとSpaceを追加
            Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f) {

                // パッドが完全に静止しており、かつキーボードが押されたなら切り替える
                isGamepadMode = false;
            }
        }
        // ガードタイマーが動いている間はカウントダウンする
        if (inputGuardTimer > 0f) {
            inputGuardTimer -= Time.deltaTime;
        }

        // 会話中でない、またはガードタイマー作動中は処理を通さない
        if (!IsInDialogue || activeWindow == null || inputGuardTimer > 0f) return;

        // 【重要】UI側で選択肢が表示されている間は、EnterキーやBボタンによる「ページめくり」を無効化する
        if (activeWindow.IsWaitingForChoice) return;

        // 【入力ハック】Enterキー または コントローラーの右ボタン（標準のSubmitや火力を割り当てられるPositiveボタン等）
        // ここでは一般的なKeyCode.Return(Enter)と、Unity標準のJoystickButton1(PSなら〇、XboxならBに相当)を検知
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.JoystickButton0)) {
            if (!IsInDialogue) return;
            // 選択肢が出ていない通常時だけ、次のページへ進む
            if(!activeWindow.IsWaitingForChoice)
            AdvanceDialogueNodes();
        }
    }

    // １ ノベルゲーム風（画面下ウィンドウ）の開始（引数をDialogueNode配列にアップグレード）
    public void StartWindowDialogue(Transform npcTransform, string npcName, DialogueNode[] nodes, int startNodeIndex = 0) {
        if (IsInDialogue || windowUI == null || nodes == null || nodes.Length == 0 || inputGuardTimer > 0f) return;
        // ★【最重要：バグ修正】会話が始まった「この瞬間」に、話しかけたボタンからデバイスを最終確定する
        // パッドのAボタン（JoystickButton0）が今押されたか、またはスティックに僅かでも反応があればパッドモード
        if (Input.GetKey(KeyCode.JoystickButton0) ||
            Mathf.Abs(Input.GetAxisRaw("LeftAnalogVertical")) > 0.1f ||
            Mathf.Abs(Input.GetAxisRaw("LeftAnalogHorizontal")) > 0.1f) {
            isGamepadMode = true;
        }
        // キーボードのEnter（Return）またはSpaceが押されて話しかけられたならキーボードモード
        else if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.Space)) {
            isGamepadMode = false;
        }

        IsInDialogue = true;
        inputGuardTimer = cooldownTime;
        // Invectorの入力を完全ロック＆物理の慣性をストップ
        if (playerInput != null) {
            playerInput.lockInput = true;
            // ゲームプレイ入力を完全にUIへ明け渡すことができます。
            playerInput.SetLockAllInput(true);

            // Invector内部の移動・カメラ入力を完全に遮断する公式メソッド
            playerInput.cc.input = Vector2.zero;
        }
        
        // 物理の慣性を完全にストップ（会話中に滑っていくバグを防ぐ）
        if (playerRb != null) { playerRb.velocity = Vector3.zero; playerRb.angularVelocity = Vector3.zero; }

        // アニメーションをアイドル状態に強制
        if (playerAnim != null) {
            playerAnim.SetFloat("InputMagnitude", 0f);
            playerAnim.SetFloat("InputVertical", 0f);
            playerAnim.SetFloat("InputHorizontal", 0f);
        }

        // ★【最重要追加】二重起動を防ぎつつ、フワッとNPCの方を向くコルーチンを開始
        if (lookAtCoroutine != null) StopCoroutine(lookAtCoroutine);
        lookAtCoroutine = StartCoroutine(LookAtNPCRoutine(npcTransform));
        
        // 2. ★【最重要】EventSystemの入力設定を「Unity標準」に強制リセットする
        if (EventSystem.current != null) {
            // インベントリ等がEventSystemに割り込んできている場合を想定し、
            // Unity標準のインプットモジュールを強制的に引き剥がして再初期化、
            // または標準の入力軸（Vertical / Submit）を明示的に指定します。

            // ★インプットモジュールの入力軸を会話専用（Invectorのゲームパッド軸）に書き換える
            CacheInputModule();

            if (standardModule != null) {
                if (isGamepadMode) {
                    // パッド操作中なら、Invectorのスティック軸を流す
                    // Unity標準の入力軸（Project Settings -> Input Manager の設定名）を強制固定
                    // ※ここを後述の手順で変更した「DialogueVertical」等に指定すると、すべて同時に動くようになります
                    standardModule.horizontalAxis = "LeftAnalogHorizontal";
                    standardModule.verticalAxis = "LeftAnalogVertical";
                } else {
                    // キーボード操作中なら、Unity標準の軸（W/Sや矢印）を流す
                    standardModule.horizontalAxis = "Horizontal";
                    standardModule.verticalAxis = "Vertical";
                    //standardModule.verticalAxis = "DialogueVertical";
                }
                standardModule.submitButton = "Submit"; // これがキーボードのEnterやパッドのAボタンに相当
                standardModule.cancelButton = "Cancel";
                
            }
        }

        // ★追加：マウスカーソルのロックを強制解除し、画面上に本物の矢印を表示させる！
        //インベクタ―による「カーソルハック」という画面中央に固定して隠すという使用を解除
        Cursor.lockState = CursorLockMode.None; // 中央固定を解除して自由に動かせるようにする
        Cursor.visible = true;                  // 矢印をハッキリと表示させる

        // ここで選択された形式（ウィンドウ or 吹き出し）のUIを表示する処理を呼ぶ
        activeWindow = windowUI;
        currentNodes = nodes;
        // ★修正部分：0を直接代入していたところを、受け取った startNodeIndex に書き換える
        currentNodeIndex = startNodeIndex;

        windowUI.SetupWindow(npcName);
        ShowCurrentNode();

        // ★Cinemachineカメラ演出のオン・オフ切り替え
        if (useCameraDirector && dialogueVirtualCamera != null) {
            dialogueVirtualCamera.Priority = activePriority;
        }
    }

    // ★新設：NPCの方をスムーズに向かせるコルーチン
    private IEnumerator LookAtNPCRoutine(Transform npcTransform) {
        Transform playerTransform = playerInput != null ? playerInput.transform : null;
        if (playerTransform == null || npcTransform == null) yield break;

        float elapsed = 0f;

        while (elapsed < duration) {
            elapsed += Time.deltaTime;

            // プレイヤーからNPCへの方向を計算
            Vector3 directionToNPC = npcTransform.position - playerTransform.position;
            directionToNPC.y = 0f; // 上下の傾きによるキャラクターの傾きを防止

            if (directionToNPC != Vector3.zero) {
                // 地面に対して水平な回転ベクトルを計算
                Vector3 desiredForward = Vector3.RotateTowards(
                    playerTransform.forward,
                    directionToNPC.normalized,
                    rotateSpeed * Time.deltaTime,
                    0f
                );

                // 新しい回転値を適用（Rigidbodyが止まっているのでtransformへ直接安全に代入）
                playerTransform.rotation = Quaternion.LookRotation(desiredForward);
            }

            yield return null; // 1フレーム待つ
        }
    }

    // 現在のページ（ノード）を表示する
    private void ShowCurrentNode() {
        if (currentNodes == null || currentNodeIndex >= currentNodes.Length) {
            EndWindowDialogue();
            return;
        }

        // UI側に現在のテキストと選択肢のデータを丸ごと渡す
        activeWindow.ShowNode(currentNodes[currentNodeIndex]);
    }

    // プレイヤーがEnterを押した時の進行処理
    private void AdvanceDialogueNodes() {
        if (activeWindow.IsTyping) {
            activeWindow.CompleteLine();

            return;
        }

        // ★修正：現在のノードの「次の行き先」をチェックする
        DialogueNode currentNode = currentNodes[currentNodeIndex];

        if (currentNode.nextNodeIndex == -1) {
            // -1 が指定されていたら、次のページには進まずここで会話を終了する
            EndWindowDialogue();
            return;
        } else if (currentNode.nextNodeIndex >= 0) {
            // 0以上の具体的なインデックスが指定されていたら、そこへジャンプする
            currentNodeIndex = currentNode.nextNodeIndex;
        } else {
            // それ以外（インスペクターで特に指定していない初期値 -2 など）は通常通り次のページへ
            currentNodeIndex++;
        }

        ShowCurrentNode();
    }

    // ★選択肢ボタンがクリックされたときにUIから呼ばれる関数
    public void OnSelectChoice(int targetNodeIndex) {
        // targetNodeIndex が -1 の場合は「会話終了」の合図とする
        if (targetNodeIndex < 0) {
            EndWindowDialogue();
            return;
        }

        // 選択肢が決定された瞬間にも、0.2秒間のガードタイマーをかけます！
        // これにより、決定ボタンを押した勢いで次のページが一瞬でスキップされて滑っていくのを完全に防ぎます。
        inputGuardTimer = cooldownTime;

        // データチェック（不正ならスルー）
        if (currentNodes == null || currentNodeIndex >= currentNodes.Length) goto SkipFlagProcess;

        DialogueChoice[] choices = currentNodes[currentNodeIndex].choices;
        if (choices == null || choices.Length == 0) goto SkipFlagProcess;
        
        // ★選択肢のフラグ書き換え処理
        for (int i = 0; i < choices.Length; i++) {
            // 選んだ選択肢（インデックス）を確実に特定してフラグをチェック
            if (choices[i].targetNodeIndex == targetNodeIndex) {
                StoryFlagType flag = choices[i].flagToSet;

                if (flag != StoryFlagType.None && StoryFlagManager.Instance != null) {
                    StoryFlagManager.Instance.SetFlag(flag, true);
                    Debug.Log($"<color=cyan>[FlagSuccess] {flag} がONになりました！</color>");
                }
                break;
            }
        }

        SkipFlagProcess:
        // 指定された番号のセリフへジャンプする
        currentNodeIndex = targetNodeIndex;
        ShowCurrentNode();
    }

    // １ ノベルゲーム風の終了（UI側から呼ばれる）
    public void EndWindowDialogue() {
        if (!IsInDialogue) return;
        IsInDialogue = false;

        // ★【追加】会話が閉じるまさにその瞬間に、最後に読んでいたセリフのUnityEventを最速実行（GCゼロ）
        if (currentNodes != null && currentNodeIndex >= 0 && currentNodeIndex < currentNodes.Length) {
            UnityEvent endEvent = currentNodes[currentNodeIndex].onNodeEndEvent;
            if (endEvent != null) {
                // インスペクターで設定された登録関数（ボス戦開始やアイテム付与など）を物理的に叩く
                endEvent.Invoke();
            }
        }

        activeWindow = null;
        currentNodes = null;

        // ★インプットモジュールの入力軸を通常（ゲームプレイ用）に完全復元
        CacheInputModule();
        if (standardModule != null || isGamepadMode) { 
            standardModule.horizontalAxis = "Horizontal";
            standardModule.verticalAxis = "Vertical";
        }


        if (windowUI != null) windowUI.CloseWindow();
        // プレイヤーの操作を解放 Invectorの入力を解放
        if (playerInput != null) {
            playerInput.lockInput = false;
            playerInput.SetLockAllInput(false);
        }
        
        // ★追加：会話が終わったら、Invector本来のアクションゲーム用カーソルロックに戻す
        Cursor.lockState = CursorLockMode.Locked; // 再び画面中央にマウスをロックする
        Cursor.visible = false;                   // 矢印を隠す


        // ★Cinemachineカメラ演出を通常時（オフ）に戻す
        if (useCameraDirector && dialogueVirtualCamera != null) {
            dialogueVirtualCamera.Priority = defaultPriority;
        }


        // 会話が完全に閉じたその瞬間に、0.2秒間のガードタイマーを起動する！
        // これにより、人間の指がBボタンを離すまでの間にNPCがボタンを誤検知するのをマネージャー側で完全にブロックします
        inputGuardTimer = cooldownTime;
    }

    // ２ コミック風（頭上吹き出し）の開始：プレイヤーは停止させない
    public void StartBubbleDialogue(Transform npcHeadTarget, string bubbleText, bool shouldLookAt = false) {
        if (bubbleUI == null) return;

        // ★【タスク8完全ドッキング】
        // NPCの頭上に配置された DialogueUI_Bubble コンポーネントを探して直接実行する（GCゼロ）
        if (npcHeadTarget != null) {
            DialogueUI_Bubble npcBubble = npcHeadTarget.GetComponentInChildren<DialogueUI_Bubble>(true);
            if (npcBubble != null) {
                npcBubble.ShowBubble(bubbleText);
            }
        }

        // ★指示（isLookAtEnabled）が true の時だけ、プレイヤーをNPCの方に向かせる！
        if (shouldLookAt) {
            if (lookAtCoroutine != null) StopCoroutine(lookAtCoroutine);
            lookAtCoroutine = StartCoroutine(LookAtNPCRoutine(npcHeadTarget));
        }
        Debug.Log($"【頭上吹き出し】{npcHeadTarget.name}: {bubbleText} (ルックアット: {shouldLookAt})");
        bubbleUI.ShowBubble(npcHeadTarget, bubbleText);
    }
}
