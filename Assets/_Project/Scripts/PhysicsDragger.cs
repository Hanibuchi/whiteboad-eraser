using UnityEngine;
using UnityEngine.InputSystem;

public class PhysicsDragger : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask draggableLayer;
    [SerializeField, Min(0.01f)] private float maxRayDistance = 100f;

    [Header("Spring Joint Parameters")]
    [Tooltip("バネの強さ（大きいほどマウスに強く追従します）")]
    [SerializeField, Min(0f)] private float spring = 1000f;
    [Tooltip("直線運動の減衰（バネのバウンド・跳ね返りを抑えます）")]
    [SerializeField, Min(0f)] private float damper = 30f;
    [SerializeField, Min(0f)] private float massScale = 1f;

    [Header("Rotation & Weight Drag (掴み中の手応え)")]
    [Tooltip("掴んでいる最中の回転抵抗。値を大きくすると回転がしっとり落ち着き、重みが出ます")]
    [SerializeField, Min(0f)] private float grabbedAngularDrag = 5f;
    [Tooltip("掴んでいる最中の直線移動抵抗。手応えを重くしたい場合にあげてください")]
    [SerializeField, Min(0f)] private float grabbedDrag = 1f;

    private GameObject _anchorObject;
    private Rigidbody _anchorRigidbody;
    private SpringJoint _currentJoint;
    private Rigidbody _draggedRigidbody;
    private Plane _dragPlane;
    private bool _isDragging;

    // 元の抵抗値を保持する変数
    private float _originalAngularDrag;
    private float _originalDrag;

    private void Awake()
    {
        ResolveCamera();
        CreateAnchorObject();
    }

    private void OnDisable()
    {
        ReleaseDraggedObject();
    }

    private void OnDestroy()
    {
        ReleaseDraggedObject();

        if (_anchorObject != null)
        {
            Destroy(_anchorObject);
            _anchorObject = null;
            _anchorRigidbody = null;
        }
    }

    private void Update()
    {
        ResolveCamera();

        if (targetCamera == null) return;

        if (TryGetPointerDown(out Vector2 pointerPosition))
        {
            TryBeginDrag(pointerPosition);
        }

        if (_isDragging && TryGetPointerUp())
        {
            ReleaseDraggedObject();
        }
    }

    private void FixedUpdate()
    {
        if (!_isDragging || _anchorRigidbody == null || targetCamera == null) return;

        if (TryGetPointerPosition(out Vector2 pointerPosition))
        {
            Ray ray = targetCamera.ScreenPointToRay(pointerPosition);
            if (_dragPlane.Raycast(ray, out float enter))
            {
                Vector3 targetPoint = ray.GetPoint(enter);
                _anchorRigidbody.MovePosition(targetPoint);
            }
        }
    }

    private void TryBeginDrag(Vector2 pointerPosition)
    {
        if (targetCamera == null) return;

        Ray ray = targetCamera.ScreenPointToRay(pointerPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, draggableLayer, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        Rigidbody hitRigidbody = hit.rigidbody;
        if (hitRigidbody == null || hitRigidbody.isKinematic) return;

        ReleaseDraggedObject();

        _draggedRigidbody = hitRigidbody;

        // 元の抵抗値を保存して、掴み用の抵抗値を適用
        _originalAngularDrag = _draggedRigidbody.angularDamping;
        _originalDrag = _draggedRigidbody.linearDamping;
        _draggedRigidbody.angularDamping = grabbedAngularDrag;
        _draggedRigidbody.linearDamping = grabbedDrag;

        // 掴んだ地点でカメラ対面平面を生成
        _dragPlane = new Plane(-targetCamera.transform.forward, hit.point);

        Vector3 localGrabPoint = _draggedRigidbody.transform.InverseTransformPoint(hit.point);
        _anchorRigidbody.position = hit.point;

        // SpringJoint設定
        _currentJoint = _draggedRigidbody.gameObject.AddComponent<SpringJoint>();
        _currentJoint.autoConfigureConnectedAnchor = false;
        _currentJoint.connectedBody = _anchorRigidbody;
        _currentJoint.connectedAnchor = Vector3.zero;
        _currentJoint.anchor = localGrabPoint;

        _currentJoint.spring = spring;
        _currentJoint.damper = damper;
        _currentJoint.minDistance = 0f;
        _currentJoint.maxDistance = 0f;
        _currentJoint.massScale = massScale;

        _isDragging = true;
    }

    private static bool TryGetPointerPosition(out Vector2 pointerPosition)
    {
        if (Mouse.current != null)
        {
            pointerPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            pointerPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        pointerPosition = default;
        return false;
    }

    private static bool TryGetPointerDown(out Vector2 pointerPosition)
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            pointerPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            pointerPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        pointerPosition = default;
        return false;
    }

    private static bool TryGetPointerUp()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
        {
            return true;
        }

        return false;
    }

    private void ReleaseDraggedObject()
    {
        _isDragging = false;

        if (_draggedRigidbody != null)
        {
            // 元の抵抗値に戻す（手放した後は元の慣性で飛んでいく）
            _draggedRigidbody.angularDamping = _originalAngularDrag;
            _draggedRigidbody.linearDamping = _originalDrag;
            _draggedRigidbody = null;
        }

        if (_currentJoint != null)
        {
            Destroy(_currentJoint);
            _currentJoint = null;
        }
    }

    private void ResolveCamera()
    {
        if (targetCamera != null) return;
        targetCamera = GetComponent<Camera>();
        if (targetCamera != null) return;
        targetCamera = Camera.main;
    }

    private void CreateAnchorObject()
    {
        if (_anchorObject != null) return;

        _anchorObject = new GameObject("PhysicsDragger Anchor");
        _anchorObject.hideFlags = HideFlags.HideAndDontSave;

        _anchorRigidbody = _anchorObject.AddComponent<Rigidbody>();
        _anchorRigidbody.isKinematic = true;
        _anchorRigidbody.useGravity = false;
    }
}