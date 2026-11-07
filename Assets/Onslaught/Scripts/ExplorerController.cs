using UnityEngine;
using UnityEngine.InputSystem;

namespace Onslaught
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ExplorerController : MonoBehaviour
    {
        public InputActionAsset input;
        public Camera eye, overview;
        public GameObject mapMarker;
        public GameObject ceiling;
        public TMPro.TMP_Text interactionPrompt;
        public ShipDoor[] gates;
        public float walkSpeed = 4.5f, sensitivity = .09f;
        public bool allowPositionReset = true;
        public bool handlesEscape = true;
        public bool Captured { get; private set; }
        public bool MapVisible { get; private set; }
        public InputActionAsset Actions { get; private set; }
        CharacterController body;
        InputAction move, look, release, focus, reset, map, interact;
        Vector3 spawn;
        Quaternion spawnRotation;
        float pitch, verticalSpeed;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            spawn = transform.position; spawnRotation = transform.rotation;
            Actions = Instantiate(input);
            move = Actions.FindAction("Move", true); look = Actions.FindAction("Look", true);
            release = Actions.FindAction("Release", true);
            focus = Actions.FindAction("Focus", true); reset = Actions.FindAction("Reset", true);
            map = Actions.FindAction("Map", true);
            interact = Actions.FindAction("Interact");
        }
        void OnEnable() { Actions.Enable(); }
        void Start() { Application.targetFrameRate = 60; SetMap(false); }
        void OnDisable() { Actions?.Disable(); Capture(false); }
        void OnDestroy() { if (Actions != null) Destroy(Actions); }
        void OnApplicationFocus(bool value) { if (!value) Capture(false); }
        public void Capture(bool value)
        {
            Captured = value && !MapVisible;
            Cursor.lockState = Captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !Captured;
        }
        public void SetMap(bool visible)
        {
            MapVisible = visible; eye.enabled = !visible; overview.enabled = visible;
            mapMarker.SetActive(visible); Capture(!visible);
            if (ceiling != null) ceiling.SetActive(!visible);
        }
        public void ResetPosition()
        {
            body.enabled = false; transform.SetPositionAndRotation(spawn, spawnRotation); body.enabled = true;
            pitch = 0; verticalSpeed = 0; eye.transform.localRotation = Quaternion.identity;
            SetMap(false);
        }
        void Update()
        {
            if (map.WasPressedThisFrame()) SetMap(!MapVisible);
            if (handlesEscape && release.WasPressedThisFrame()) Capture(false);
            if (allowPositionReset && reset.WasPressedThisFrame() || transform.position.y < -5) ResetPosition();
            if (MapVisible) { if (interactionPrompt != null) interactionPrompt.text = ""; return; }
            if (!Captured && focus.WasPressedThisFrame()) Capture(true);
            if (Captured)
            {
                Vector2 delta = look.ReadValue<Vector2>() * sensitivity;
                transform.Rotate(0, delta.x, 0);
                pitch = Mathf.Clamp(pitch - delta.y, -80, 80);
                eye.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            ShipDoor nearest = null; float distance = 4;
            if (Captured && gates != null)
                foreach (var gate in gates)
                {
                    float d = gate.DistanceTo(eye.transform.position);
                    Vector3 target = gate.transform.position + Vector3.up * 1.5f;
                    if (d < distance && Vector3.Dot(eye.transform.forward, (target - eye.transform.position).normalized) > .25f
                        && (!Physics.Linecast(eye.transform.position, target, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                            || hit.collider.transform.IsChildOf(gate.transform)))
                    { nearest = gate; distance = d; }
                }
            if (nearest != null && interact != null && interact.WasPressedThisFrame()) nearest.Toggle();
            if (interactionPrompt != null) interactionPrompt.text = nearest == null ? "" : nearest.RequestedOpen ? "[E] CLOSE BOARDING GATE" : "[E] OPEN BOARDING GATE";
            Vector2 axis = Captured ? move.ReadValue<Vector2>() : Vector2.zero;
            if (body.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed = Mathf.Max(verticalSpeed - 20 * Time.deltaTime, -30);
            Vector3 planar = transform.TransformDirection(new Vector3(axis.x, 0, axis.y));
            body.Move((Vector3.ClampMagnitude(planar, 1) * walkSpeed
                + Vector3.up * verticalSpeed) * Time.deltaTime);
        }
    }
}
