using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CollisionSoundPlayer : MonoBehaviour
{
    [Tooltip("対象のレイヤー（LayerMask）: チェックされたレイヤーと衝突したときに再生します")]
    [SerializeField] private LayerMask targetLayerMask = ~0;

    [Tooltip("再生するSEクリップ配列。ランダムで1つ選んで再生します。空の場合は何もしません。")]
    [SerializeField] private AudioClip[] seClips;

    [Tooltip("SoundManager に渡す音量スケール（0-1）")]
    [SerializeField, Range(0f, 1f)] private float volumeScale = 1f;

    [Tooltip("トリガー衝突で再生するかどうか")]
    [SerializeField] private bool playOnTrigger = true;

    [Tooltip("物理衝突で再生するかどうか")]
    [SerializeField] private bool playOnCollision = false;

    [Tooltip("短時間で連続再生を抑えるクールダウン（秒）。0で無効")]
    [SerializeField, Min(0f)] private float cooldownSeconds = 0f;

    private float lastPlayTime = -Mathf.Infinity;

    private void OnTriggerEnter(Collider other)
    {
        if (!playOnTrigger) return;
        if (IsInTargetLayer(other.gameObject.layer))
        {
            TryPlay();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!playOnCollision) return;
        if (IsInTargetLayer(collision.gameObject.layer))
        {
            TryPlay();
        }
    }

    private bool IsInTargetLayer(int layer)
    {
        return (targetLayerMask.value & (1 << layer)) != 0;
    }

    private void TryPlay()
    {
        if (seClips == null || seClips.Length == 0) return;

        AudioClip clipToPlay = seClips[Random.Range(0, seClips.Length)];
        if (clipToPlay == null) return;

        if (cooldownSeconds > 0f && Time.unscaledTime - lastPlayTime < cooldownSeconds)
        {
            return;
        }

        lastPlayTime = Time.unscaledTime;

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySe(clipToPlay, volumeScale);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clipToPlay, transform.position, Mathf.Clamp01(volumeScale));
        }
    }
}
