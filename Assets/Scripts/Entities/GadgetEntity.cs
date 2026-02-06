using FishNet.Object;
using System;
using TUA.Core;
using TUA.Core.Interfaces;
using TUA.Entities;
using TUA.Items;
using TUA.Misc;
using UnityEngine;

namespace TUA.Entities
{
    [RequireComponent(typeof(Rigidbody))]
    public class GadgetEntity : Entity, IGadgetEntity
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
        private float _fuseTimer;
        private float _initialFuseTime;
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
        public event Action<IGadgetEntity, Uuid, float> OnFlashBlindRequestEvent;
        public event Action<IGadgetEntity, Vector3, float, float> OnSmokeSpawnRequestEvent;
        public event Action<IGadgetEntity, GamePlayer, GamePlayer> OnKillEvent;
        #endregion

        #region Unity Callbacks
        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody != null)
            {
                _rigidbody.angularDamping = angularDamping;
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
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
                {
                    _fuseTimer = _initialFuseTime;
                }
            }

            if (_hasLanded)
            {
                _fuseTimer -= Time.deltaTime;
                if (_fuseTimer <= 0f)
                {
                    _TriggerEffect();
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServerSide || _hasExploded || _gadgetItem == null)
                return;

            if (!string.IsNullOrEmpty(_gadgetItem.impactSoundKey))
            {
                var audioSystem = TUA.Audio.AudioSystem.Instance;
                if (audioSystem != null)
                {
                    var contactPoint = collision.GetContact(0).point;
                    audioSystem.PlayBroadcast(_gadgetItem.impactSoundKey, contactPoint, 1f, TUA.Audio.AudioCategory.Gameplay);
                }
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

            if (_rigidbody != null)
            {
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
            }

            if (item is GrenadeItem grenade)
                _initialFuseTime = grenade.fuseTime;
            else if (item is FlashItem flash)
                _initialFuseTime = flash.flashFuseTime;
            else if (item is SmokeItem smoke)
                _initialFuseTime = smoke.smokeFuseTime;

            _fuseTimer = float.MaxValue;
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
            if (_hasExploded)
                return;

            _hasExploded = true;

            if (_gadgetItem is GrenadeItem)
                _TriggerGrenade();
            else if (_gadgetItem is FlashItem)
                _TriggerFlash();
            else if (_gadgetItem is SmokeItem)
                _TriggerSmoke();

            RpcClient_TriggerEffect();

            if (_gadgetItem is FlashItem flash)
            {
                var allPlayers = GameWorld.Instance?.AllPlayers;
                if (allPlayers != null)
                {
                    foreach (var player in allPlayers)
                    {
                        if (player == null || !player.IsOnline)
                            continue;

                        var playerEntity = GameWorld.Instance?.GetEntityOwnedByPlayer<PlayerEntity>(player);
                        if (playerEntity == null)
                            continue;

                        var playerPosition = playerEntity.transform.position + Vector3.up * playerEntity.headHeight;
                        var toPlayer = (playerPosition - transform.position).normalized;
                        var distance = Vector3.Distance(transform.position, playerPosition);

                        if (distance > flash.flashRadius)
                            continue;

                        var angle = Vector3.Angle(transform.forward, toPlayer);
                        if (angle > flash.blindAngle)
                            continue;

                        if (Physics.Raycast(transform.position, toPlayer, out var hit, distance))
                        {
                            if (hit.collider.GetComponent<PlayerEntity>() != playerEntity)
                                continue;
                        }

                        RpcClient_FlashBlind(playerEntity.EntityUuid, flash.blindDuration);
                    }
                }
            }

            if (GameWorld.Instance)
                GameWorld.Instance.Server_DespawnObject(gameObject);
        }

        private void _TriggerGrenade()
        {
            if (!(_gadgetItem is GrenadeItem grenade))
                return;

            var position = transform.position;
            var radius = grenade.explosionRadius;
            var damage = grenade.explosionDamage;
            var force = grenade.explosionForce;

            var colliders = Physics.OverlapSphere(position, radius);
            foreach (var col in colliders)
            {
                var distance = Vector3.Distance(position, col.transform.position);
                var normalizedDistance = Mathf.Clamp01(distance / radius);
                var damageMultiplier = grenade.damageFalloffCurve.Evaluate(normalizedDistance);
                var finalDamage = damage * damageMultiplier;

                var health = col.GetComponent<IHealth>();
                if (health != null)
                {
                    var gameWorld = GameWorld.Instance;
                    GamePlayer thrower = null;
                    
                    if (gameWorld != null && gameWorld.AllPlayers != null)
                    {
                        foreach (var player in gameWorld.AllPlayers)
                        {
                            if (player != null && player.Uuid == _throwerUuid)
                            {
                                thrower = player;
                                var throwerEntity = gameWorld.GetEntityOwnedByPlayer<PlayerEntity>(player);
                                if (throwerEntity != null && (object)health == throwerEntity)
                                    continue;
                                break;
                            }
                        }
                    }

                    var healthBeforeDamage = health.CurrentHealth;
                    health.Server_TakeDamage(finalDamage);

                    if (healthBeforeDamage > 0f && health.CurrentHealth <= 0f)
                    {
                        GamePlayer victim = null;
                        if (health is Entity victimEntity)
                            victim = victimEntity.GamePlayer;
                        else if (col != null)
                        {
                            var hitEntity = col.GetComponent<Entity>() ?? col.GetComponentInParent<Entity>();
                            if (hitEntity != null)
                                victim = hitEntity.GamePlayer;
                        }

                        if (thrower != null || victim != null)
                            OnKillEvent?.Invoke(this, thrower, victim);
                    }
                }

                var rb = col.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    var direction = (col.transform.position - position).normalized;
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

            var forward = transform.forward;
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

                var playerPosition = playerEntity.transform.position + Vector3.up * playerEntity.headHeight;
                var toPlayer = (playerPosition - position).normalized;
                var distance = Vector3.Distance(position, playerPosition);

                if (distance > radius)
                    continue;

                var angle = Vector3.Angle(forward, toPlayer);
                if (angle > blindAngle)
                    continue;

                if (Physics.Raycast(position, toPlayer, out var hit, distance))
                {
                    if (hit.collider.GetComponent<PlayerEntity>() != playerEntity)
                        continue;
                }

                RpcClient_FlashBlind(playerEntity.EntityUuid, blindDuration);
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
            {
                OnSmokeSpawnRequestEvent?.Invoke(this, position, radius, duration);
            }

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

        #region Network RPCs
        [ObserversRpc(ExcludeServer = false)]
        private void RpcClient_Initialize(string itemId)
        {
            if (IsServerSide)
                return;

            if (!string.IsNullOrEmpty(itemId) && itemRegistry != null)
                _gadgetItem = itemRegistry.GetEntry<GadgetItem>(itemId);
        }

        [ObserversRpc(ExcludeServer = false)]
        private void RpcClient_TriggerEffect()
        {
            if (_gadgetItem == null)
                return;

            if (!string.IsNullOrEmpty(_gadgetItem.effectSoundKey))
            {
                var audioSystem = Audio.AudioSystem.Instance;
                audioSystem?.PlayLocal(_gadgetItem.effectSoundKey, transform.position, 1f, TUA.Audio.AudioCategory.Gameplay);
            }

            if(_gadgetItem.effectPrefab == null)
                return;

            var effect = Instantiate(_gadgetItem.effectPrefab, transform.position, transform.rotation);
            Destroy(effect, 10f);
        }

        [ObserversRpc(ExcludeServer = false)]
        private void RpcClient_FlashBlind(Uuid playerUuid, float duration)
        {
            OnFlashBlindRequestEvent?.Invoke(this, playerUuid, duration);
        }

        [ObserversRpc(ExcludeServer = false)]
        private void RpcClient_SpawnSmoke(Vector3 position, float radius, float duration)
        {
            OnSmokeSpawnRequestEvent?.Invoke(this, position, radius, duration);
        }
        #endregion
    }
}
