using UnityEngine;
using UnityEngine.InputSystem;

namespace ShipEnemies
{
    // Asset-review controls only. Nothing here is used by the Onslaught mission.
    public sealed class EnemyPreview : MonoBehaviour
    {
        public EnemyBrain[] prefabs, displays;
        public Camera reviewCamera;
        public Vitality dummy;
        public Transform arenaSpawn, liveRoot;
        public TMPro.TMP_Text statusSign;
        public EnemyBrain Active { get; private set; }
        public int Selected { get; private set; }
        public bool Demo { get; private set; }
        bool lineup = true;
        float yaw, pitch = 13, distance = 5;
        int clip;
        readonly string[] clips = { "Idle", "Move", "Melee", "Hit", "Death" };
        InputAction orbit, zoom, orbitHeld, targetMove;
        void Awake()
        {
            orbit = new InputAction(binding: "<Mouse>/delta"); zoom = new InputAction(binding: "<Mouse>/scroll/y");
            orbitHeld = new InputAction(binding: "<Mouse>/rightButton");
            targetMove = new InputAction(type: InputActionType.Value);
            targetMove.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        }
        void OnEnable() { orbit.Enable(); zoom.Enable(); orbitHeld.Enable(); targetMove.Enable(); }
        void OnDisable() { orbit.Disable(); zoom.Disable(); orbitHeld.Disable(); targetMove.Disable(); }
        void OnDestroy() { orbit.Dispose(); zoom.Dispose(); orbitHeld.Dispose(); targetMove.Dispose(); }
        void Start() { Application.targetFrameRate = 60; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        public void Select(int index)
        {
            Selected = Mathf.Clamp(index, 0, prefabs.Length - 1); lineup = false; Demo = false;
            ClearDemo(); clip = 0; displays[Selected].Play("Idle", true); yaw = 0; pitch = 10;
            distance = Selected == 4 ? 7 : Selected >= 5 ? 2.3f : 4;
        }
        public void ShowLineup() { lineup = true; Demo = false; ClearDemo(); }
        public void ClearDemo()
        {
            Active = null;
            for (int i = liveRoot.childCount - 1; i >= 0; i--) Destroy(liveRoot.GetChild(i).gameObject);
            foreach (var p in FindObjectsByType<AcidProjectile>()) Destroy(p.gameObject);
            foreach (var p in FindObjectsByType<AcidPool>()) Destroy(p.gameObject);
            dummy.ResetHealth();
        }
        public void BeginDemo()
        {
            ClearDemo(); Demo = true; lineup = false;
            dummy.transform.position = new Vector3(0, 0, 17);
            Active = Instantiate(prefabs[Selected], arenaSpawn.position, Quaternion.identity, liveRoot);
            Active.SetTarget(dummy);
        }
        public void NextAnimation()
        {
            if (Demo) return;
            lineup = false; clip = (clip + 1) % clips.Length; displays[Selected].Play(clips[clip], true);
        }
        void Update()
        {
            var k = Keyboard.current;
            if (k != null)
            {
                for (int i = 0; i < prefabs.Length; i++) if (k[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) Select(i);
                if (k.digit0Key.wasPressedThisFrame) ShowLineup();
                if (k.tabKey.wasPressedThisFrame) BeginDemo();
                if (k.rKey.wasPressedThisFrame) { if (Demo) BeginDemo(); else displays[Selected].Play("Idle", true); }
                if (k.spaceKey.wasPressedThisFrame) NextAnimation();
                if (k.eKey.wasPressedThisFrame && !Demo) displays[Selected].Play(Selected == 4 ? "Vomit" : Selected == 2 ? "Spit" : "Melee", true);
                if (k.hKey.wasPressedThisFrame) { if (Demo && Active != null) Active.Health.TakeDamage(5); else displays[Selected].Play("Hit", true); }
                if (k.kKey.wasPressedThisFrame) { if (Demo && Active != null) Active.Health.TakeDamage(99999); else displays[Selected].Play("Death", true); }
            }
            if (Demo)
            {
                var input = targetMove.ReadValue<Vector2>();
                var p = dummy.transform.position + new Vector3(input.x, 0, input.y) * (5 * Time.deltaTime);
                p.x = Mathf.Clamp(p.x, -8, 8); p.z = Mathf.Clamp(p.z, 11, 22); dummy.transform.position = p;
            }
            else if (!lineup)
            {
                if (orbitHeld.IsPressed()) { var d = orbit.ReadValue<Vector2>(); yaw += d.x * .22f; pitch = Mathf.Clamp(pitch - d.y * .18f, -5, 65); }
                distance = Mathf.Clamp(distance - zoom.ReadValue<float>() * .004f, Selected >= 5 ? 1.2f : 2, 12);
            }
            if (statusSign != null)
                statusSign.text = Demo ? $"BEHAVIOR LAB  /  {prefabs[Selected].definition.displayName.ToUpperInvariant()}\n{(Active != null ? Active.State : "DEAD")}   |   TARGET DAMAGE {dummy.TotalDamage:0}\nWASD move target   H flinch   K kill   R restart   0 gallery"
                    : "ENEMY ASSET LAB\n1-7 inspect   0 lineup   TAB behavior demo\nSPACE animation   E ability   H flinch   K death   R idle\nRight-drag orbit   Scroll zoom";
        }
        void LateUpdate()
        {
            if (lineup) { reviewCamera.transform.SetPositionAndRotation(new Vector3(0, 5.1f, 13.5f), Quaternion.LookRotation(new Vector3(0, 1.5f, 0) - new Vector3(0, 5.1f, 13.5f))); return; }
            if (Demo) { var p = new Vector3(13, 13, 28); reviewCamera.transform.SetPositionAndRotation(p, Quaternion.LookRotation(new Vector3(0, 0, 15) - p)); return; }
            Vector3 center = displays[Selected].transform.position + Vector3.up * (Selected == 4 ? 1.85f : Selected >= 5 ? .3f : 1.1f);
            Vector3 pos = center + Quaternion.Euler(pitch, yaw, 0) * new Vector3(0, 0, distance);
            reviewCamera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(center - pos));
        }
    }
}
