using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using System;

public class LockZoneTest : MonoBehaviour
{
    [Header("普段使っている通常カメラ")]
    [SerializeField] private CinemachineVirtualCamera normalCamera;

    [Header("ロック用カメラの参照")]
    [SerializeField] private CinemachineVirtualCamera lockCamera;

    [Header("エリアに入った時の優先度（普段のカメラ「10」より高くする）")]
    [SerializeField] private int activePriority = 15;

    [Header("カメラが完全に切り替わるまでの時間（CinemachineBrainのBlendTimeと同じにする）")]
    [SerializeField] private float cameraBlendTime = 1.5f;

    private int defaultPriority;
    private CinemachineFramingTransposer framingTransposer;
    private CinemachineFramingTransposer lockFramingTransposer;
    private float defaultDeadZoneWidth;
    private float lockDeadZoneWidth;
    private float defaultXDamping;
    private float lockXDamping;
    private bool onZone;
    private Coroutine lockCoroutine; // コルーチンの二重起動防止用

    void Start()
    {
        if (normalCamera != null) {
            framingTransposer = normalCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
            defaultDeadZoneWidth = framingTransposer.m_DeadZoneWidth;
            defaultXDamping = framingTransposer.m_XDamping;
        }
        if (lockCamera != null) {
            defaultPriority = lockCamera.Priority;
            lockFramingTransposer = lockCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
            lockDeadZoneWidth = lockFramingTransposer.m_DeadZoneWidth;

            lockFramingTransposer.m_DeadZoneWidth = defaultDeadZoneWidth;
            lockXDamping = lockFramingTransposer.m_XDamping;
            lockFramingTransposer.m_XDamping = defaultXDamping;
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player") && lockCamera != null && normalCamera != null) {
            onZone = true;

            // 優先度を上げてメインカメラにする
            lockCamera.Priority = activePriority;

            // 2. 二重起動を防ぎつつ、ブレンド完了を待つコルーチンを開始
            if (lockCoroutine != null) StopCoroutine(lockCoroutine);
            lockCoroutine = StartCoroutine(ActivateLockDeadZone());

            lockFramingTransposer.m_DeadZoneWidth = lockDeadZoneWidth;
            
        }
    }
    private void OnTriggerExit(Collider other) {
        if (other.CompareTag("Player") && lockCamera != null) {
            onZone = false;
            // 優先度を元に戻して通常カメラにバトンタッチ
            lockCamera.Priority = defaultPriority;
            lockFramingTransposer.m_DeadZoneWidth = defaultDeadZoneWidth;
            lockFramingTransposer.m_XDamping = lockXDamping;

            if(!onZone)
                StartCoroutine(RevertXDamping());
        }
    }

    private IEnumerator RevertXDamping() {
        yield return new WaitForSeconds(2);
        if (!onZone)
            lockFramingTransposer.m_XDamping = defaultXDamping;
    }

    private IEnumerator ActivateLockDeadZone() {
        // カメラのブレンド（移行）が完了するまでじっと待つ
        yield return new WaitForSeconds(cameraBlendTime);

        // 完全にカメラが切り替わった後に、初めてデッドゾーンを広げて横移動をロックする
        if (onZone && lockFramingTransposer != null) {
            lockFramingTransposer.m_DeadZoneWidth = lockDeadZoneWidth;
        }
    }
}
