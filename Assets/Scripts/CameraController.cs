using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using System.Collections;
using UnityEngine.Tilemaps;

public class CameraController : MonoBehaviour
{
    
    private static CameraController _instance = null; public static CameraController Singleton { get { if (_instance == null) { _instance = FindFirstObjectByType<CameraController>(); } return _instance; } }
    
    /// <summary> Called as soon as the player touches the screen. The argument is the screen position. </summary>
    public event Action<Vector2> onStartTouch;
    /// <summary> Called as soon as the player stops touching the screen. The argument is the screen position. </summary>
    public event Action<Vector2> onEndTouch;
    /// <summary> Called if the player completed a quick tap motion. The argument is the screen position. </summary>
    public event Action<Vector2> onTap;
    /// <summary> Called if the player swiped the screen. The argument is the screen movement delta. </summary>
    public event Action<Vector2, Vector2> onSwipe;
    /// <summary> Called if the player pinched the screen. The arguments are the distance between the fingers before and after. </summary>
    public event Action<float, float> onPinch;

    [Header("Tap")]
    [Tooltip("The maximum movement for a touch motion to be treated as a tap")]
    public float maxDistanceForTap = 40;
    [Tooltip("The maximum duration for a touch motion to be treated as a tap")]
    public float maxDurationForTap = 0.4f;

    [FormerlySerializedAs("useMouse")]
    [Header("Desktop Debug")]
    [Tooltip("Use the mouse on desktop?")]
    [SerializeField] private bool _forceUseMouse = true;
    [FormerlySerializedAs("mouseScrollSpeed")]
    [Tooltip("The simulated pinch speed using the scroll wheel")]
    [SerializeField] [Min(0.01f)] private float _mouseScrollSpeed = 2;

    [Header("Camera Control")]
    [Tooltip("Does the script control camera movement?")]
    public bool controlCamera = true;
    
    [Header("UI")]
    [Tooltip("Are touch motions listened to if they are over UI elements?")]
    public bool ignoreUI = false;
    
    [Header("Zoom Bounds")]
    public float defaultZoom = 5f;
    public float minZoom = 1f;
    public float maxZoom = 10;
    
    [Header("Move Bounds")]
    [Tooltip("Is the camera bound to an area?")]
    public bool useBounds;
    public float boundMinX = -150; public float minX { get { return boundMinX; } }
    public float boundMaxX = 150; public float maxX { get { return boundMaxX; } }
    public float boundMinY = -150; public float minY { get { return boundMinY; } }
    public float boundMaxY = 150; public float maxY { get { return boundMaxY; } }

    private Vector2 touch0StartPosition;
    private Vector2 touch0LastPosition;
    private float touch0StartTime;
    private bool cameraControlEnabled = true;
    private Camera _camera;
    public float orthographicSize { get { return _camera.orthographicSize; } }
    
    /// <summary> Has the player at least one finger on the screen? </summary>
    public bool isTouching { get; private set; }

    /// <summary> The point of contact if it exists in Screen space. </summary>
    public Vector2 touchPosition { get { return touch0LastPosition; } }
    
    private bool _autoMoving = false; public bool autoMoving { get { return _autoMoving; } }
    private Vector3 _autoMovePosition = Vector3.zero;
    private Vector3 _autoMoveBasePosition = Vector3.zero;
    private float _autoMoveZoom = 2f;
    private float _autoMoveBaseZoom = 2f;
    private float _autoMoveTime = 1f;
    private float _autoMoveTimer = 0f;
    
    private void Awake()
    {
        _instance = this;
        _camera = GetComponent<Camera>();
        _camera.nearClipPlane = 0.01f;
        _camera.orthographic = true;
        _camera.nearClipPlane = 0;
        _camera.orthographicSize = defaultZoom;
    }

    public void AutoMoveToWorldPosition(Vector3 position, float zoom, float time)
    {
        if (_autoMoving)
        {
           StopCoroutine(AutoMove());
        }
        _autoMovePosition = position;
        _autoMoveBasePosition = _camera.transform.position;
        _autoMoveZoom = zoom;
        _autoMoveBaseZoom = _camera.orthographicSize;
        _autoMoveTime = time;
        _autoMoveTimer = 0;
        StartCoroutine(AutoMove());
    }
    
    public void AutoMoveToTilemapCoordinate(Tilemap tilemap, Vector3Int coordinate, float zoom, float time)
    {
        if (tilemap != null)
        {
            AutoMoveToWorldPosition(tilemap.CellToWorld(coordinate), zoom, time);
        }
    }
    
    private IEnumerator AutoMove()
    {
        _autoMoving = true;
        while (Vector3.Distance(_autoMovePosition, _camera.transform.position) > 0.01f)
        {
            float t = Mathf.Clamp01(_autoMoveTimer / _autoMoveTime);
            _camera.transform.position = Vector3.Lerp(_autoMoveBasePosition, _autoMovePosition, t);
            _camera.orthographicSize = Mathf.Clamp(Mathf.Lerp(_autoMoveBaseZoom, _autoMoveZoom, t), minZoom, maxZoom);
            _autoMoveTimer += Time.deltaTime;
            yield return null;
        }
        _autoMoving = false;
    }
    
    private void Update()
    {
        if (_forceUseMouse || !Input.touchSupported) 
        {
            UpdateWithMouse();
        } 
        else 
        {
            UpdateWithTouch();
        }
    }

    private void LateUpdate() 
    {
        CameraInBounds();
    }

    private void UpdateWithMouse() 
    {
        if (Input.GetMouseButtonDown(0)) 
        {
            if (ignoreUI || !IsPointerOverUIObject()) 
            {
                touch0StartPosition = Input.mousePosition;
                touch0StartTime = Time.time;
                touch0LastPosition = touch0StartPosition;
                isTouching = true;
                if (onStartTouch != null)
                {
                    onStartTouch(Input.mousePosition);
                }
            }
        }
        if (Input.GetMouseButton(0) && isTouching) 
        {
            Vector2 move = (Vector2)Input.mousePosition - touch0LastPosition;
            touch0LastPosition = Input.mousePosition;
            if (move != Vector2.zero) 
            {
                OnSwipe(move, Input.mousePosition);
            }
        }
        if (Input.GetMouseButtonUp(0) && isTouching) 
        {
            if (Time.time - touch0StartTime <= maxDurationForTap && Vector2.Distance(Input.mousePosition, touch0StartPosition) <= maxDistanceForTap) 
            {
                OnClick(Input.mousePosition);
            }
            if (onEndTouch != null)
            {
                onEndTouch(Input.mousePosition);
            }
            isTouching = false;
            cameraControlEnabled = true;
        }
        if (Input.mouseScrollDelta.y != 0)
        {
            float amount = Input.mouseScrollDelta.y < 0 ? 1f / (_mouseScrollSpeed + 1f) : _mouseScrollSpeed + 1f;
            OnPinch(Input.mousePosition, 1, amount, Vector2.right);
        }
    }

    private void UpdateWithTouch() 
    {
        int touchCount = Input.touches.Length;
        if (touchCount == 1) 
        {
            Touch touch = Input.touches[0];
            switch (touch.phase) 
            {
                case TouchPhase.Began: 
                {
                        if (ignoreUI || !IsPointerOverUIObject()) 
                        {
                            touch0StartPosition = touch.position;
                            touch0StartTime = Time.time;
                            touch0LastPosition = touch0StartPosition;
                            isTouching = true;
                            if (onStartTouch != null)
                            {
                                onStartTouch(touch0StartPosition);
                            }
                        }
                        break;
                    }
                case TouchPhase.Moved: 
                {
                        touch0LastPosition = touch.position;
                        if (touch.deltaPosition != Vector2.zero && isTouching) 
                        {
                            OnSwipe(touch.deltaPosition, touch.position);
                        }
                        break;
                    }
                case TouchPhase.Ended: 
                {
                        if (Time.time - touch0StartTime <= maxDurationForTap && Vector2.Distance(touch.position, touch0StartPosition) <= maxDistanceForTap && isTouching) 
                        {
                            OnClick(touch.position);
                        }
                        if (onEndTouch != null)
                        {
                            onEndTouch(touch.position);
                        }
                        isTouching = false;
                        cameraControlEnabled = true;
                        break;
                    }
                case TouchPhase.Stationary:
                case TouchPhase.Canceled:
                    break;
            }
        } 
        else if (touchCount == 2) 
        {
            Touch touch0 = Input.touches[0];
            Touch touch1 = Input.touches[1];
            if (touch0.phase == TouchPhase.Ended || touch1.phase == TouchPhase.Ended)
            {
                return;
            }
            isTouching = true;
            float previousDistance = Vector2.Distance(touch0.position - touch0.deltaPosition, touch1.position - touch1.deltaPosition);
            float currentDistance = Vector2.Distance(touch0.position, touch1.position);
            if (previousDistance != currentDistance) 
            {
                OnPinch((touch0.position + touch1.position) / 2f, previousDistance, currentDistance, (touch1.position - touch0.position).normalized);
            }
        }
        else 
        {
            if (isTouching) 
            {
                if (onEndTouch != null)
                {
                    onEndTouch(touch0LastPosition);
                }
                isTouching = false;
            }
            cameraControlEnabled = true;
        }
    }

    private void OnClick(Vector2 position) 
    {
        if (onTap != null && (ignoreUI || !IsPointerOverUIObject())) 
        {
            onTap(position);
        }
    }
    
    private void OnSwipe(Vector2 deltaPosition, Vector2 position) 
    {
        if (onSwipe != null) 
        {
            onSwipe(position, deltaPosition);
        }
        if (controlCamera && cameraControlEnabled)
        {
            _camera.transform.position -= (_camera.ScreenToWorldPoint(deltaPosition) - _camera.ScreenToWorldPoint(Vector2.zero));
        }
    }
    
    private void OnPinch(Vector2 center, float oldDistance, float newDistance, Vector2 touchDelta) 
    {
        if (onPinch != null) 
        {
            onPinch(oldDistance, newDistance);
        }
        if (controlCamera && cameraControlEnabled) 
        {
            if (_camera.orthographic) 
            {
                var currentPinchPosition = _camera.ScreenToWorldPoint(center);
                _camera.orthographicSize = Mathf.Clamp(Mathf.Max(0.1f, _camera.orthographicSize * oldDistance / newDistance), minZoom, maxZoom);
                var newPinchPosition = _camera.ScreenToWorldPoint(center);
                _camera.transform.position -= newPinchPosition - currentPinchPosition;
            } 
            else 
            {
                _camera.fieldOfView = Mathf.Clamp(_camera.fieldOfView * oldDistance / newDistance, 0.1f, 179.9f);
            }
        }
    }

    /// <summary> Checks if the the current input is over canvas UI </summary>
    public bool IsPointerOverUIObject()
    {
        return IsScreenPositionOverUIObject(Input.mousePosition);
    }
    
    public bool IsScreenPositionOverUIObject(Vector2 position) 
    {
        if (EventSystem.current == null)
        {
            return false;
        }
        PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
        eventDataCurrentPosition.position = position;
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventDataCurrentPosition, results);
        return results.Count > 0;
    }
    
    /// <summary> Cancels camera movement for the current motion. Resets to use camera at the end of the touch motion.</summary>
    public void CancelCamera() 
    {
        cameraControlEnabled = false;
    }

    private void CameraInBounds() 
    {
        if(controlCamera && useBounds && _camera != null && _camera.orthographic) 
        {
            _camera.orthographicSize = Mathf.Min(_camera.orthographicSize, ((boundMaxY - boundMinY) / 2f) - 0.001f);
            _camera.orthographicSize = Mathf.Min(_camera.orthographicSize, (Screen.height * (boundMaxX - boundMinX) / (2f * Screen.width)) - 0.001f);
            Vector2 margin = _camera.ScreenToWorldPoint((Vector2.up * Screen.height / 2f) + (Vector2.right * Screen.width / 2f)) - _camera.ScreenToWorldPoint(Vector2.zero);
            float marginX = margin.x;
            float marginY = margin.y;
            float camMaxX = boundMaxX - marginX;
            float camMaxY = boundMaxY - marginY;
            float camMinX = boundMinX + marginX;
            float camMinY = boundMinY + marginY;
            float camX = Mathf.Clamp(_camera.transform.position.x, camMinX, camMaxX);
            float camY = Mathf.Clamp(_camera.transform.position.y, camMinY, camMaxY);
            _camera.transform.position = new Vector3(camX, camY, _camera.transform.position.z);
        }
    }

    public Vector3 ScreenPositionToWorldPosition(Vector3 position)
    {
        return _camera.ScreenToWorldPoint(position);
    }
    
    public Vector3 WorldPositionToScreenPosition(Vector3 position)
    {
        return _camera.WorldToScreenPoint(position);
    }
    
    public Vector3 ScreenPositionToViewportPosition(Vector3 position)
    {
        return _camera.ScreenToViewportPoint(position);
    }
    
}