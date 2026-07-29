using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SpeedSeController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField, Tooltip("SEを鳴らす基準となる速度")]
    private float speedThreshold = 10f;

    [SerializeField, Tooltip("SEを鳴らす間隔（秒）")]
    private float playInterval = 0.5f;

    [SerializeField, Tooltip("再生するSEのクリップ。この中からランダムに選ばれて再生されます。")]
    private AudioClip[] speedSeClips;

    private Rigidbody rb;
    private float timer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // 最初に閾値を超えた際、即座にSEを鳴らすためにインターバル値で初期化
        timer = playInterval;
    }

    private void FixedUpdate()
    {
        // 最新のUnity（2023.3以降）で推奨される linearVelocity を使用
        float currentSpeed = rb.linearVelocity.magnitude;

        if (currentSpeed >= speedThreshold)
        {
            // 物理演算の更新間隔（FixedUpdate）に合わせてタイマーを加算
            timer += Time.fixedDeltaTime;

            if (timer >= playInterval)
            {
                PlayRandomSpeedSe();
                // 再生後はタイマーをリセット
                timer = 0f;
            }
        }
        else
        {
            // 速度が閾値を下回った場合、次回閾値を超えたタイミングで即座に鳴らすようリセット
            timer = playInterval;
        }
    }

    private void PlayRandomSpeedSe()
    {
        // 配列が空、または未設定の場合は何もしない
        if (speedSeClips == null || speedSeClips.Length == 0)
        {
            return;
        }

        // 0 から 配列の要素数-1 までの間でランダムなインデックスを取得
        int randomIndex = Random.Range(0, speedSeClips.Length);
        AudioClip selectedClip = speedSeClips[randomIndex];

        // SoundManagerのシングルトンインスタンスを利用して選ばれたSEを再生
        if (selectedClip != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySe(selectedClip);
        }
    }
}