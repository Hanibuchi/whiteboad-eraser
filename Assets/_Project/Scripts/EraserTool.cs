using UnityEngine;

public sealed class EraserTool : MonoBehaviour
{
    [SerializeField] private Whiteboard whiteboard;
    [SerializeField] private Collider bottomCollider;
    [SerializeField, Min(0.0001f)] private float minimumEraseSize = 0.01f;
    [SerializeField] private bool autoFindWhiteboard = true;

    private void Awake()
    {
        if (bottomCollider == null)
        {
            bottomCollider = GetComponent<Collider>();
        }

        if (autoFindWhiteboard && whiteboard == null)
        {
            whiteboard = FindFirstObjectByType<Whiteboard>();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        ProcessCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        ProcessCollision(collision);
    }

    private void ProcessCollision(Collision collision)
    {
        if (whiteboard == null || !whiteboard.IsInitialized || whiteboard.BoardCollider == null)
        {
            return;
        }

        if (collision.collider != whiteboard.BoardCollider)
        {
            return;
        }
        
        Collider sourceCollider = bottomCollider != null ? bottomCollider : GetComponent<Collider>();
        Vector3 centerPoint = sourceCollider != null ? sourceCollider.bounds.center : transform.position;

        if (!whiteboard.TryGetUv(centerPoint, out Vector2 uv))
        {
            return;
        }

        Vector2 eraseSizeUv = ResolveEraseSizeUv();
        float angleDeg = transform.eulerAngles.y;
        whiteboard.DrawRectangle(uv, eraseSizeUv, angleDeg);
    }

    private Vector2 ResolveEraseSizeUv()
    {
        Collider sourceCollider = bottomCollider != null ? bottomCollider : GetComponent<Collider>();
        if (sourceCollider == null || whiteboard == null)
        {
            return Vector2.one * minimumEraseSize;
        }

        Vector2 uvSize = whiteboard.WorldSizeToUvSize(sourceCollider.bounds.size);
        uvSize.x = Mathf.Max(minimumEraseSize, uvSize.x);
        uvSize.y = Mathf.Max(minimumEraseSize, uvSize.y);
        return uvSize;
    }
}