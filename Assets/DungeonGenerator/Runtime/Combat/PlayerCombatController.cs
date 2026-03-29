using UnityEngine;

namespace DungeonGenerator
{
    public class PlayerCombatController : MonoBehaviour
    {
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private KeyCode attackKey = KeyCode.Mouse0;
        [SerializeField] private float castHeight = 0.5f;

        [Header("Feedback")]
        [SerializeField] private bool showAttackLine = true;
        [SerializeField] private float attackLineDuration = 0.08f;
        [SerializeField] private float attackLineWidth = 0.05f;
        [SerializeField] private Color hitLineColor = new(1f, 0.25f, 0.25f, 0.95f);
        [SerializeField] private Color missLineColor = new(0.9f, 0.9f, 0.9f, 0.85f);

        private float _lastAttackTime;
        private LineRenderer _attackLine;
        private Material _attackLineMaterial;
        private float _hideAttackLineAtTime;

        private void Awake()
        {
            if (equipment == null)
            {
                equipment = GetComponent<PlayerEquipment>();
            }

            _lastAttackTime = -999f;

            if (showAttackLine)
            {
                EnsureAttackLineRenderer();
            }
        }

        private void Update()
        {
            if (_attackLine != null && _attackLine.enabled && Time.time >= _hideAttackLineAtTime)
            {
                _attackLine.enabled = false;
            }

            if (!Input.GetKeyDown(attackKey))
            {
                return;
            }

            var weapon = equipment != null ? equipment.EquippedWeapon : null;
            if (weapon == null)
            {
                return;
            }

            if (Time.time < _lastAttackTime + weapon.Cooldown)
            {
                return;
            }

            _lastAttackTime = Time.time;
            TryAttack(weapon);
        }

        private void TryAttack(WeaponItemDefinition weapon)
        {
            var origin = transform.position + Vector3.up * castHeight;
            var direction = transform.forward;
            var maxEndPoint = origin + direction * weapon.Range;

            if (!Physics.Raycast(origin, direction, out var hit, weapon.Range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                ShowAttackFeedback(origin, maxEndPoint, false);
                return;
            }

            var damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable == null)
            {
                ShowAttackFeedback(origin, hit.point, false);
                return;
            }

            damageable.TakeDamage(weapon.Damage);
            ShowAttackFeedback(origin, hit.point, true);
        }

        private void ShowAttackFeedback(Vector3 from, Vector3 to, bool hit)
        {
            if (!showAttackLine)
            {
                return;
            }

            EnsureAttackLineRenderer();

            _attackLine.startColor = hit ? hitLineColor : missLineColor;
            _attackLine.endColor = hit ? hitLineColor : missLineColor;
            _attackLine.startWidth = attackLineWidth;
            _attackLine.endWidth = attackLineWidth;
            _attackLine.SetPosition(0, from);
            _attackLine.SetPosition(1, to);
            _attackLine.enabled = true;

            _hideAttackLineAtTime = Time.time + Mathf.Max(0.01f, attackLineDuration);
        }

        private void EnsureAttackLineRenderer()
        {
            if (_attackLine != null)
            {
                return;
            }

            var lineObject = new GameObject("AttackFeedbackLine");
            lineObject.transform.SetParent(transform, false);

            _attackLine = lineObject.AddComponent<LineRenderer>();
            _attackLine.positionCount = 2;
            _attackLine.useWorldSpace = true;
            _attackLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _attackLine.receiveShadows = false;
            _attackLine.enabled = false;

            _attackLineMaterial = new Material(Shader.Find("Sprites/Default"));
            _attackLine.material = _attackLineMaterial;
        }

        private void OnDestroy()
        {
            if (_attackLineMaterial != null)
            {
                Destroy(_attackLineMaterial);
            }
        }
    }
}
