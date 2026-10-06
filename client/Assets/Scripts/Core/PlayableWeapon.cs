using UnityEngine;
using UnityEngine.InputSystem;

namespace Megame.Client
{
    /// <summary>
    /// First-person weapon with a procedural view model, hitscan firing and a
    /// visible tracer. Built from primitives so it needs no prefab.
    ///
    /// Mouse look is consumed by <see cref="PlayableCameraRig"/>, which exposes
    /// the accumulated delta each frame via <see cref="ConsumeLookDelta"/> so the
    /// weapon and the camera agree on where the player is aiming.
    /// </summary>
    public class PlayableWeapon : MonoBehaviour
    {
        public enum WeaponKind { Pistol, Rifle, Shotgun }

        public WeaponKind Kind = WeaponKind.Rifle;
        public float Damage = 26f;
        public float Range = 120f;
        public float FireRateRpm = 600f;
        public int MagazineSize = 30;
        public float ReloadSeconds = 1.9f;
        public float RecoilKick = 0.9f;
        public float RecoilRecovery = 7f;
        public int MaxReserve = 240;

        public int AmmoInMag { get; private set; }
        public int AmmoReserve { get; private set; }
        public bool IsReloading { get; private set; }
        public float Range01 { get; private set; }

        private Transform _camera;
        private Transform _viewModel;
        private Transform _muzzle;
        private Camera _cam;
        private float _nextFireTime;
        private float _reloadEndsAt;
        private float _recoil;
        private Vector3 _restPos;
        private Quaternion _restRot;
        private LineRenderer _tracer;
        private Light _muzzleFlash;
        private float _flashEndsAt;

        public static PlayableWeapon Create(Camera cam, WeaponKind kind)
        {
            var go = new GameObject("Weapon");
            var weapon = go.AddComponent<PlayableWeapon>();
            weapon.Kind = kind;
            weapon._cam = cam;
            weapon._camera = cam != null ? cam.transform : null;
            weapon.BuildViewModel();
            weapon.ResetAmmo();
            return weapon;
        }

        public void ResetAmmo()
        {
            AmmoInMag = MagazineSize;
            AmmoReserve = MaxReserve;
        }

        private void BuildViewModel()
        {
            float len = Kind == WeaponKind.Pistol ? 0.26f : 0.62f;
            float barrelR = Kind == WeaponKind.Shotgun ? 0.032f : 0.021f;

            var gunMat = PlayableWorld.NewMaterial(new Color(0.13f, 0.13f, 0.15f));
            var gripMat = PlayableWorld.NewMaterial(new Color(0.22f, 0.16f, 0.11f));
            var metalMat = PlayableWorld.NewMaterial(new Color(0.42f, 0.44f, 0.48f));

            _viewModel = new GameObject("ViewModel").transform;
            _viewModel.SetParent(transform, false);

            // Receiver
            var receiver = GameObject.CreatePrimitive(PrimitiveType.Cube);
            receiver.name = "Receiver";
            receiver.transform.SetParent(_viewModel, false);
            receiver.transform.localPosition = new Vector3(0f, 0f, len * 0.5f);
            receiver.transform.localScale = new Vector3(0.075f, 0.10f, len);
            Destroy(receiver.GetComponent<Collider>());
            receiver.GetComponent<MeshRenderer>().sharedMaterial = gunMat;

            // Barrel
            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "Barrel";
            barrel.transform.SetParent(_viewModel, false);
            barrel.transform.localPosition = new Vector3(0f, 0.012f, len + barrelR);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(barrelR * 2f, len * 0.22f, barrelR * 2f);
            Destroy(barrel.GetComponent<Collider>());
            barrel.GetComponent<MeshRenderer>().sharedMaterial = metalMat;

            // Grip
            var grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grip.name = "Grip";
            grip.transform.SetParent(_viewModel, false);
            grip.transform.localPosition = new Vector3(0f, -0.085f, len * 0.18f);
            grip.transform.localRotation = Quaternion.Euler(-16f, 0f, 0f);
            grip.transform.localScale = new Vector3(0.055f, 0.16f, 0.07f);
            Destroy(grip.GetComponent<Collider>());
            grip.GetComponent<MeshRenderer>().sharedMaterial = gripMat;

            if (Kind != WeaponKind.Pistol)
            {
                // Magazine
                var mag = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mag.name = "Magazine";
                mag.transform.SetParent(_viewModel, false);
                mag.transform.localPosition = new Vector3(0f, -0.10f, len * 0.42f);
                mag.transform.localScale = new Vector3(0.045f, 0.18f, 0.09f);
                Destroy(mag.GetComponent<Collider>());
                mag.GetComponent<MeshRenderer>().sharedMaterial = gunMat;

                // Stock
                var stock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stock.name = "Stock";
                stock.transform.SetParent(_viewModel, false);
                stock.transform.localPosition = new Vector3(0f, -0.01f, -0.10f);
                stock.transform.localScale = new Vector3(0.05f, 0.09f, 0.22f);
                Destroy(stock.GetComponent<Collider>());
                stock.GetComponent<MeshRenderer>().sharedMaterial = gripMat;
            }

            // Muzzle marker
            _muzzle = new GameObject("Muzzle").transform;
            _muzzle.SetParent(_viewModel, false);
            _muzzle.localPosition = new Vector3(0f, 0.012f, len + barrelR * 4f);

            // Tracer
            var tracerGo = new GameObject("Tracer");
            tracerGo.transform.SetParent(transform, false);
            _tracer = tracerGo.AddComponent<LineRenderer>();
            _tracer.positionCount = 2;
            _tracer.startWidth = 0.02f;
            _tracer.endWidth = 0.008f;
            _tracer.useWorldSpace = true;
            _tracer.enabled = false;
            var tracerMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"))
            {
                color = new Color(1f, 0.85f, 0.45f, 0.9f)
            };
            _tracer.material = tracerMat;

            // Muzzle flash light
            var flashGo = new GameObject("MuzzleFlash");
            flashGo.transform.SetParent(_muzzle, false);
            _muzzleFlash = flashGo.AddComponent<Light>();
            _muzzleFlash.type = LightType.Point;
            _muzzleFlash.color = new Color(1f, 0.82f, 0.45f);
            _muzzleFlash.range = 9f;
            _muzzleFlash.intensity = 0f;

            _restPos = new Vector3(0.22f, -0.18f, 0.32f);
            _restRot = Quaternion.Euler(0f, 0f, 0f);
            _viewModel.localPosition = _restPos;
            _viewModel.localRotation = _restRot;

            // Park it in front of the camera, otherwise nothing is visible.
            transform.SetParent(_camera, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            if (_cam == null) return;

            HandleLook();
            HandleFire();
            HandleReload();
            UpdateRecoil();
            UpdateTracerAndFlash();
        }

        private void HandleLook()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 look = PlayableCameraRig.ConsumeLookDelta();

            if (mouse.leftButton.isPressed)
            {
                _recoil += RecoilKick * 0.02f;
            }

            // Recoil pushes the weapon up and slightly to the right.
            float pitch = -look.y + _recoil * 4f;
            float yaw = -look.x;
            _viewModel.localPosition = _restPos + new Vector3(0f, -pitch * 0.012f, 0f);
            _viewModel.localRotation = _restRot * Quaternion.Euler(pitch * 2.2f, yaw * 1.4f, 0f);
        }

        private void HandleFire()
        {
            var mouse = Mouse.current;
            if (mouse == null || IsReloading) return;
            if (AmmoInMag <= 0)
            {
                StartReload();
                return;
            }
            if (!mouse.leftButton.isPressed) return;
            if (Time.time < _nextFireTime) return;

            float interval = 60f / Mathf.Max(1f, FireRateRpm);
            _nextFireTime = Time.time + interval;

            AmmoInMag--;
            Hitscan();
        }

        private void Hitscan()
        {
            Vector3 origin = _cam.transform.position;
            Vector3 dir = _cam.transform.forward;
            // Spread grows while moving and while holding the trigger.
            float spread = 0.004f + _rbSpeedFactor() * 0.02f + _recoil * 0.02f;
            dir = (dir + _cam.transform.right * Random.Range(-spread, spread)
                       + _cam.transform.up * Random.Range(-spread, spread)).normalized;

            Vector3 end = origin + dir * Range;
            RaycastHit hit;
            if (Physics.Raycast(origin, dir, out hit, Range))
            {
                end = hit.point;
                SpawnImpact(hit.point, hit.normal);
                ApplyDamage(hit.collider, hit.point);
            }

            _tracer.SetPosition(0, _muzzle.position);
            _tracer.SetPosition(1, end);
            _tracer.enabled = true;
            _tracerEndTime = Time.time + 0.045f;

            _muzzleFlash.intensity = 3.2f;
            _flashEndsAt = Time.time + 0.04f;

            _recoil = Mathf.Min(_recoil + RecoilKick, 3.5f);
        }

        private float _tracerEndTime;
        private int _hits;

        /// <summary>How many shots have connected with a pedestrian.</summary>
        public int Hits => _hits;

        /// <summary>
        /// Routes a hit to whatever it landed on. Currently only pedestrians
        /// are damageable; buildings and vehicles absorb the round.
        /// </summary>
        private void ApplyDamage(Collider hit, Vector3 point)
        {
            if (hit == null) return;

            var npc = hit.GetComponentInParent<PlayableNpc>();
            if (npc == null || npc.IsDead) return;

            bool killed = npc.TakeDamage(Damage);
            _hits++;

            if (killed)
            {
                // A small reward makes shooting people a real decision rather
                // than free damage.
                var vitals = PlayerVitals.Instance;
                if (vitals != null) vitals.AddCash(40f);

                var quests = QuestSystem.Instance;
                if (quests != null) quests.ReportKill();
            }
        }

        private float _rbSpeedFactor()
        {
            var player = PlayablePlayer.Instance;
            return player != null ? Mathf.Clamp01(player.Velocity.magnitude / 8f) : 0f;
        }

        private void SpawnImpact(Vector3 point, Vector3 normal)
        {
            var spark = GameObject.CreatePrimitive(PrimitiveType.Quad);
            spark.name = "Impact";
            spark.transform.position = point + normal * 0.01f;
            spark.transform.rotation = Quaternion.LookRotation(normal);
            spark.transform.localScale = Vector3.one * 0.07f;
            Destroy(spark.GetComponent<Collider>());
            spark.GetComponent<MeshRenderer>().sharedMaterial =
                PlayableWorld.NewMaterial(new Color(1f, 0.9f, 0.6f), true);
            Destroy(spark, 6f);
        }

        public void StartReload()
        {
            if (IsReloading) return;
            if (AmmoInMag >= MagazineSize || AmmoReserve <= 0) return;
            IsReloading = true;
            _reloadEndsAt = Time.time + ReloadSeconds;
        }

        private void HandleReload()
        {
            if (!IsReloading) return;
            if (Time.time < _reloadEndsAt) return;

            int need = MagazineSize - AmmoInMag;
            int take = Mathf.Min(need, AmmoReserve);
            AmmoInMag += take;
            AmmoReserve -= take;
            IsReloading = false;
        }

        private void UpdateRecoil()
        {
            _recoil = Mathf.MoveTowards(_recoil, 0f, RecoilRecovery * Time.deltaTime);
        }

        private void UpdateTracerAndFlash()
        {
            if (_tracer.enabled && Time.time > _tracerEndTime) _tracer.enabled = false;
            if (_muzzleFlash.intensity > 0f && Time.time > _flashEndsAt) _muzzleFlash.intensity = 0f;
        }
    }
}