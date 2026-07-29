using UnityEngine;

public sealed class PenTool : MonoBehaviour
{
    [SerializeField] private Whiteboard whiteboard;
    [SerializeField] private Collider tipCollider;
    [SerializeField, Min(0f)] private float inkConsumptionPerUvDistance = 0.35f;
    [SerializeField, Min(0.0001f)] private float minimumBrushRadius = 0.003f;
    [SerializeField, Min(0.0001f)] private float brushSpacingFactor = 0.5f;
    [SerializeField, Range(0f, 1f)] private float maxOpacity = 1f;
    [SerializeField] private bool autoFindWhiteboard = true;

    private bool hasLastUv;
    private Vector2 lastUv;
    private float inkLevel = 1f;

    public float InkLevel => inkLevel;
    public float InkPercentage => inkLevel * 100f;

    public void SetWhiteboard(Whiteboard targetWhiteboard)
    {
        whiteboard = targetWhiteboard;
    }

    private void Awake()
    {
        if (tipCollider == null)
        {
            tipCollider = GetComponent<Collider>();
        }

        if (autoFindWhiteboard && whiteboard == null)
        {
            whiteboard = FindFirstObjectByType<Whiteboard>();
        }
    }

    public void HandleCollisionExit(Collision collision)
    {
        if (whiteboard != null && collision.collider == whiteboard.BoardCollider)
        {
            hasLastUv = false;
        }
    }

    public void ResetInk(float normalizedAmount = 1f)
    {
        inkLevel = Mathf.Clamp01(normalizedAmount);
        hasLastUv = false;
    }

    public void ProcessCollision(Collision collision)
    {
        if (whiteboard == null || !whiteboard.IsInitialized || whiteboard.BoardCollider == null)
        {
            return;
        }
        // Debug.Log($"initialized. {transform.parent.name}");

        if (collision.collider != whiteboard.BoardCollider)
        {
            return;
        }
        // Debug.Log($"collide with whiteboard. {transform.parent.name}");

        if (collision.contactCount == 0)
        {
            return;
        }
        // Debug.Log($"contact count is not zero. {transform.parent.name}");

        Collider sourceCollider = tipCollider != null ? tipCollider : GetComponent<Collider>();

        ContactPoint validContact = default;
        bool isTouching = false;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            if (contact.thisCollider == sourceCollider)
            {
                validContact = contact;
                isTouching = true;
                break;
            }
        }

        if (!isTouching)
        {
            return;
        }
            // Debug.Log($"contact point found. {transform.parent.name}");

        if (inkLevel <= 0f)
        {
            hasLastUv = false;
            return;
        }

        // 先頭の接触点ではなく、ペン自身の接触点(validContact)を使用
        if (!whiteboard.TryGetUv(validContact.point, out Vector2 uv))
        {
            return;
        }
            // Debug.Log($"UV found. {transform.parent.name}");
        // --- ここまで ---

        Vector2 brushSizeUv = ResolveBrushSizeUv();
        float brushRadiusUv = Mathf.Max(minimumBrushRadius, Mathf.Min(brushSizeUv.x, brushSizeUv.y) * 0.5f);

        float movementDistance = hasLastUv ? Vector2.Distance(lastUv, uv) : 0f;
        if (hasLastUv)
        {
            inkLevel = Mathf.Max(0f, inkLevel - movementDistance * inkConsumptionPerUvDistance);
        }

        if (inkLevel <= 0f)
        {
            hasLastUv = false;
            return;
        }

        float opacity = Mathf.Clamp01(inkLevel * maxOpacity);
        int stampCount = 1;
        if (hasLastUv)
        {
            float spacing = Mathf.Max(0.0001f, brushRadiusUv * brushSpacingFactor);
            stampCount = Mathf.Max(1, Mathf.CeilToInt(movementDistance / spacing));
        }

        for (int stampIndex = 0; stampIndex < stampCount; stampIndex++)
        {
            float t = stampCount == 1 ? 1f : (float)stampIndex / (stampCount - 1);
            Vector2 stampUv = hasLastUv ? Vector2.Lerp(lastUv, uv, t) : uv;
            whiteboard.DrawCircle(stampUv, brushRadiusUv, opacity);
        }

        lastUv = uv;
        hasLastUv = true;
    }

    private Vector2 ResolveBrushSizeUv()
    {
        Collider sourceCollider = tipCollider != null ? tipCollider : GetComponent<Collider>();
        if (sourceCollider == null || whiteboard == null)
        {
            return Vector2.one * minimumBrushRadius * 2f;
        }

        Vector2 uvSize = whiteboard.WorldSizeToUvSize(sourceCollider.bounds.size);
        if (uvSize.x <= 0f || uvSize.y <= 0f)
        {
            return Vector2.one * minimumBrushRadius * 2f;
        }

        return uvSize;
    }
}