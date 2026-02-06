using System;
using System.Collections.Generic;
using System.Linq;
using TUA.Audio;
using TUA.Core;
using TUA.Core.Interfaces;
using TUA.Items;
using TUA.Misc;
using UnityEngine;

namespace TUA.Entities
{
    [RequireComponent(typeof(Rigidbody))]
    public partial class GadgetEntity : Entity, IGadgetEntity
    {
        #region Serialized Fields
        [Header("References")]
        [Tooltip("Item registry to look up gadget items on clients.")]
        public Registry itemRegistry;
        [Header("Physics")]
        [Tooltip("Bounce coefficient (0 = no bounce, 1 = full bounce). Realistic grenades have low bounce.")]
        public float bounceCoefficient = 0.3f;
        [Tooltip("Minimum velocity before grenade stops bouncing.")]
        public float minBounceVelocity = 0.5f;
        [Tooltip("Angular damping to slow rotation.")]
        public float angularDamping = 2f;
        [Header("Lifetime")]
        [Tooltip("Maximum lifetime in seconds before gadget despawns.")]
        public float maxLifetime = 60f;
        [Header("Landing Detection")]
        [Tooltip("Maximum distance in units the grenade can move to be considered stable.")]
        public float landingMovementThreshold = 0.1f;
        [Tooltip("Time in seconds the grenade must stay within movement threshold before considered landed.")]
        public float landingStableTime = 0.3f;
        #endregion

        #region Fields
        private Rigidbody _rigidbody;
        private bool _hasExploded;
        private bool _hasLanded;
        private float _lifetimeTimer;
        private float _landingStableTimer;
        private Vector3 _lastStablePosition;
        private float _lastStableTime;
        private Uuid _throwerUuid;
        private GadgetItem _gadgetItem;
        private bool _isGrenade;
        #endregion

        #region IGadgetEntity Implementation
        Uuid IGadgetEntity.EntityUuid => EntityUuid;
        bool IGadgetEntity.IsServerSide => IsServerSide;
        bool IGadgetEntity.IsValidAndSpawned => IsSpawned;
        public event Action<IGadgetEntity, Vector3, float, float> OnSmokeSpawnRequestEvent;
        public event Action<IGadgetEntity, GamePlayer, GamePlayer> OnKillEvent;
        #endregion

        #region Unity Callbacks
        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.angularDamping = angularDamping;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        public void Update()
        {
            if (!IsServerSide || _hasExploded || _gadgetItem == null)
                return;

            _lifetimeTimer += Time.deltaTime;
            if (_lifetimeTimer >= maxLifetime)
            {
                _TriggerEffect();
                return;
            }

            if (!_hasLanded)
            {
                _hasLanded = _CheckIfLanded();
                if (_hasLanded)
                    _TriggerEffect();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServerSide || _hasExploded || _gadgetItem == null)
                return;

            if (!string.IsNullOrEmpty(_gadgetItem.impactSoundKey))
            {
                var contactPoint = collision.GetContact(0).point;
                AudioSystem.Instance?.PlayBroadcast(_gadgetItem.impactSoundKey, contactPoint, 1f, AudioCategory.Gameplay);
            }

            if (!_isGrenade)
                return;

            var velocity = _rigidbody.linearVelocity.magnitude;
            if (velocity < minBounceVelocity)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                return;
            }

            var contact = collision.GetContact(0);
            var normal = contact.normal;
            var incomingVelocity = _rigidbody.linearVelocity;
            var reflectedVelocity = Vector3.Reflect(incomingVelocity, normal) * bounceCoefficient;

            _rigidbody.linearVelocity = reflectedVelocity;

            var perpendicular = Vector3.Cross(normal, incomingVelocity.normalized);
            if (perpendicular.magnitude > 0.1f)
            {
                var spin = perpendicular.normalized * velocity * 0.5f;
                _rigidbody.angularVelocity += spin;
            }
        }
        #endregion

        #region Public Methods
        public void Server_Initialize(GadgetItem item, Vector3 velocity, Uuid throwerUuid)
        {
            if (!IsServerSide)
                return;

            _gadgetItem = item;
            _throwerUuid = throwerUuid;
            _isGrenade = item is GrenadeItem;

            if (_rigidbody == null)
                _rigidbody = GetComponent<Rigidbody>();

            _rigidbody.constraints = RigidbodyConstraints.None;
            _rigidbody.linearVelocity = velocity;
            _rigidbody.angularDamping = angularDamping;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;

                if (_isGrenade)
                {
                    var randomSpin = new Vector3(
                        UnityEngine.Random.Range(-5f, 5f),
                        UnityEngine.Random.Range(-5f, 5f),
                        UnityEngine.Random.Range(-5f, 5f)
                    );
                    _rigidbody.angularVelocity = randomSpin;
                }

            _hasLanded = false;
            _lifetimeTimer = 0f;
            _landingStableTimer = 0f;
            _lastStablePosition = transform.position;
            _lastStableTime = Time.time;

            var itemId = item != null ? item.Id : string.Empty;
            RpcClient_Initialize(itemId);
        }
        #endregion

        #region Private Methods
        private void _TriggerEffect()
        {
            if (!IsServerSide || _hasExploded)
                return;

            _hasExploded = true;

            if (_gadgetItem is GrenadeItem)
                _TriggerGrenade();
            else if (_gadgetItem is FlashItem)
                _TriggerFlash();
            else if (_gadgetItem is SmokeItem)
                _TriggerSmoke();

            RpcClient_TriggerEffect();

            if (GameWorld.Instance)
                GameWorld.Instance.Server_DespawnObject(gameObject);
        }

        private void _TriggerGrenade()
        {
            if (!IsServerSide || !(_gadgetItem is GrenadeItem grenade))
                return;

            var position = transform.position;
            var radius = grenade.explosionRadius;
            var damage = grenade.explosionDamage;
            var force = grenade.explosionForce;

            var gameWorld = GameWorld.Instance;
            if (gameWorld == null)
                return;

            GamePlayer thrower = null;
            PlayerEntity throwerEntity = null;
            if (_throwerUuid.IsValid)
            {
                foreach (var player in gameWorld.AllPlayers)
                {
                    if (player != null && player.Uuid == _throwerUuid)
                    {
                        thrower = player;
                        throwerEntity = gameWorld.GetEntityOwnedByPlayer<PlayerEntity>(thrower);
                        break;
                    }
                }
            }

            // Entity-based damage handling (avoids multiple hits from multiple colliders on same entity)
            var processedEntities = new HashSet<Entity>();
            var allEntities = gameWorld.AllEntities.Values.ToList();

            foreach (var entity in allEntities)
            {
                if (entity == null || !entity.IsSpawned || !entity.IsServerSide)
                    continue;

                if (processedEntities.Contains(entity))
                    continue;

                var entityPosition = entity.transform.position;
                var distance = Vector3.Distance(position, entityPosition);

                if (distance > radius)
                    continue;

                var normalizedDistance = Mathf.Clamp01(distance / radius);
                var damageMultiplier = grenade.damageFalloffCurve.Evaluate(normalizedDistance);
                var finalDamage = damage * damageMultiplier;

                if (entity is IHealth health)
                {
                    if (throwerEntity != null && entity == throwerEntity)
                        continue;

                    processedEntities.Add(entity);

                    var healthBeforeDamage = health.CurrentHealth;
                    health.Server_TakeDamage(finalDamage);

                    if (healthBeforeDamage > 0f && health.CurrentHealth <= 0f)
                    {
                        var victim = entity.GamePlayer;
                        OnKillEvent?.Invoke(this, thrower, victim);
                    }
                }
            }

            // Collider-based physics forces (handles all rigidbodies, not just entities)
            var colliders = Physics.OverlapSphere(position, radius);
            foreach (var col in colliders)
            {
                var colPosition = col.transform.position;
                var distance = Vector3.Distance(position, colPosition);
                var normalizedDistance = Mathf.Clamp01(distance / radius);

                var rb = col.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    var direction = (colPosition - position).normalized;
                    rb.AddForce(direction * force * (1f - normalizedDistance), ForceMode.Impulse);
                }
            }
        }

        private void _TriggerFlash()
        {
            if (!(_gadgetItem is FlashItem flash))
                return;

            var position = transform.position;
            var radius = flash.flashRadius;
            var blindAngle = flash.blindAngle;
            var blindDuration = flash.blindDuration;

            var allPlayers = GameWorld.Instance?.AllPlayers;

            if (allPlayers == null)
                return;

            foreach (var player in allPlayers)
            {
                if (player == null || !player.IsOnline)
                    continue;

                var playerEntity = GameWorld.Instance?.GetEntityOwnedByPlayer<PlayerEntity>(player);
                if (playerEntity == null)
                    continue;

                playerEntity.GetCameraView(out var cameraPosition, out var cameraRotation, out _);
                var cameraForward = cameraRotation * Vector3.forward;

                var toFlash = position - cameraPosition;
                var distance = toFlash.magnitude;

                if (distance > radius)
                    continue;

                var toFlashDir = toFlash / Mathf.Max(0.001f, distance);
                var angle = Vector3.Angle(cameraForward, toFlashDir);
                if (angle > blindAngle)
                    continue;

                var hits = Physics.RaycastAll(cameraPosition, toFlashDir, distance, ~0, QueryTriggerInteraction.Ignore);
                if (hits?.Length > 0)
                {
                    Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    var blocked = false;
                    foreach (var h in hits)
                    {
                        if (h.collider == null)
                            continue;

                        if (h.collider.GetComponentInParent<PlayerEntity>() == playerEntity)
                            continue;

                        if (h.collider.GetComponentInParent<GadgetEntity>() == this)
                            break;

                        blocked = true;
                        break;
                    }

                    if (blocked)
                        continue;
                }

                RpcClient_FlashBlind(player.Uuid, blindDuration);
            }
        }

        private void _TriggerSmoke()
        {
            if (!(_gadgetItem is SmokeItem smoke))
                return;

            var position = transform.position;
            var radius = smoke.smokeRadius;
            var duration = smoke.smokeDuration;

            if (IsServerSide)
                OnSmokeSpawnRequestEvent?.Invoke(this, position, radius, duration);

            RpcClient_SpawnSmoke(position, radius, duration);
        }

        private bool _CheckIfLanded()
        {
            if (_rigidbody == null)
                return false;

            var currentPosition = transform.position;
            var distanceMoved = Vector3.Distance(currentPosition, _lastStablePosition);
            
            if (distanceMoved <= landingMovementThreshold)
            {
                var timeSinceStable = Time.time - _lastStableTime;
                if (timeSinceStable >= landingStableTime)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                    _rigidbody.constraints = RigidbodyConstraints.FreezeAll;
                    return true;
                }
            }
            else
            {
                _lastStablePosition = currentPosition;
                _lastStableTime = Time.time;
            }
            
            return false;
        }
        #endregion
    }
}
