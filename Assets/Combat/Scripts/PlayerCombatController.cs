using System.Collections.Generic;
using UnityEngine;

namespace DungeonGenerator
{
    public class PlayerCombatController : MonoBehaviour
    {
        private enum AttackShape
        {
            Thrust,
            Swipe
        }

        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private DungeonBasic3DBuilder dungeonBuilder;
        [SerializeField] private KeyCode attackKey = KeyCode.Mouse0;
        [SerializeField] private float castHeight = 0.5f;
        [SerializeField] private float attackActiveDuration = 0.12f;

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
        private readonly Collider[] _swipeOverlapBuffer = new Collider[24];
        private readonly HashSet<IDamageable> _damagedThisAttack = new HashSet<IDamageable>();

        private bool _hasActiveAttack;
        private bool _activeAttackHasHit;
        private float _activeAttackEndsAt;
        private AttackShape _activeAttackShape;
        private float _activeAttackDamage;
        private float _activeAttackRange;
        private float _activeAttackSwipeWidth;

        private void Awake()
        {
            if (equipment == null)
            {
                equipment = GetComponent<PlayerEquipment>();
            }

            if (dungeonBuilder == null)
            {
                dungeonBuilder = FindFirstObjectByType<DungeonBasic3DBuilder>();
            }

            _lastAttackTime = -999f;

            if (showAttackLine)
            {
                EnsureAttackLineRenderer();
            }
        }

        private void Update()
        {
            ProcessActiveAttack();

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
            weapon.ExecuteAttack(this);
        }

        public void PerformThrustAttack(WeaponItemDefinition weapon)
        {
            BeginAttack(AttackShape.Thrust, weapon, 0f);
        }

        public void PerformSwordSwipeAttack(WeaponItemDefinition weapon, float swipeWidth)
        {
            BeginAttack(AttackShape.Swipe, weapon, swipeWidth);
        }

        private void BeginAttack(AttackShape shape, WeaponItemDefinition weapon, float swipeWidth)
        {
            _hasActiveAttack = true;
            _activeAttackHasHit = false;
            _activeAttackEndsAt = Time.time + Mathf.Max(0.01f, attackActiveDuration);
            _activeAttackShape = shape;
            _activeAttackDamage = weapon.Damage;
            _activeAttackRange = weapon.Range;
            _activeAttackSwipeWidth = Mathf.Max(0.1f, swipeWidth);
            _damagedThisAttack.Clear();

            ProcessActiveAttack();
        }

        private void ProcessActiveAttack()
        {
            if (!_hasActiveAttack)
            {
                return;
            }

            if (Time.time > _activeAttackEndsAt)
            {
                _hasActiveAttack = false;
                return;
            }

            if (_activeAttackShape == AttackShape.Thrust)
            {
                ProcessThrustAttack();
                return;
            }

            ProcessSwipeAttack();
        }

        private void ProcessThrustAttack()
        {
            var origin = transform.position + Vector3.up * castHeight;
            var forward = transform.forward;

            var end = origin + forward * _activeAttackRange;
            var hitCount = Physics.OverlapCapsuleNonAlloc(
                origin,
                end,
                0.22f,
                _swipeOverlapBuffer,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);

            ApplyDamageForOverlaps(hitCount, _activeAttackDamage);
            ShowAttackFeedback(origin, end, _activeAttackHasHit);
        }

        private void ProcessSwipeAttack()
        {
            var origin = transform.position + Vector3.up * castHeight;
            var forward = transform.forward;

            var oneCellDistance = dungeonBuilder != null ? Mathf.Max(0.1f, dungeonBuilder.cellSize) : _activeAttackRange;
            var forwardDistance = Mathf.Min(oneCellDistance, _activeAttackRange);
            var center = origin + forward * forwardDistance;

            var halfWidth = _activeAttackSwipeWidth * 0.5f;
            var halfDepth = Mathf.Max(0.1f, forwardDistance * 0.35f);
            var halfExtents = new Vector3(halfWidth, 0.75f, halfDepth);
            var rotation = Quaternion.LookRotation(forward, Vector3.up);

            var hitCount = Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                _swipeOverlapBuffer,
                rotation,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);

            ApplyDamageForOverlaps(hitCount, _activeAttackDamage);

            var from = center - transform.right * halfWidth;
            var to = center + transform.right * halfWidth;
            ShowAttackFeedback(from, to, _activeAttackHasHit);
        }

        private void ApplyDamageForOverlaps(int hitCount, float damage)
        {
            for (var i = 0; i < hitCount; i++)
            {
                var collider = _swipeOverlapBuffer[i];
                _swipeOverlapBuffer[i] = null;

                if (collider == null || collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                var damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable == null || !_damagedThisAttack.Add(damageable))
                {
                    continue;
                }

                damageable.TakeDamage(damage);
                _activeAttackHasHit = true;
            }
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
