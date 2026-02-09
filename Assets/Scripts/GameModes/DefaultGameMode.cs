using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TUA.Core;
using TUA.Entities;
using TUA.Misc;
using UnityEngine;

namespace TUA.GameModes
{
    [PlayerData("default")]
    [Serializable]
    public struct DefaultPlayerData : IPlayerData
    {
        public void Serialize(INetWriter writer)
        {
        }

        public IPlayerData Deserialize(INetReader reader)
        {
            return this;
        }
    }

    public struct DefaultGameSettings : IGameSettings
    {
    }

    public class DefaultGameMode : GameMode<DefaultPlayerData, DefaultGameSettings>
    {
        public enum GS
        {
            Warmup = 0,
            Match = 1,
            MatchOver = 2
        }

        #region Serialized Fields
        [Header("References")]
        public GameObject firstPersonPlayerPrefab;
        public GameObject thirdPersonPlayerPrefab;
        public Item[] defaultItems;

        [Header("Spawn Points")]
        public Transform[] invadersSpawnPoints;
        public Transform[] guardsSpawnPoints;

        [Header("Timer")]
        public float matchDurationSeconds = 600f;
        public bool useRounds;
        public int totalRounds = 5;
        public float warmupSeconds = 5f;
        public float roundDurationSeconds = 180f;
        #endregion

        #region Private Fields
        private int _invadersSpawnIndex;
        private int _guardsSpawnIndex;
        private GS _state;
        private int _roundNumber;
        private bool _phaseTimerRunning;
        private float _phaseTimerDurationSeconds;
        private float _phaseTimerRemainingSeconds;
        private int _lastSentMatchTimeSeconds = int.MinValue;
        private string _lastSentMatchMessage;
        private bool _lastSentShowTime;
        #endregion

        #region Public Methods
        public override void OnWorldStart(GameWorld gameWorld, DefaultGameSettings gameSettings)
        {
            Debug.Log("DefaultGameMode OnWorldStart");
            if (gameWorld && gameWorld.IsServerSide)
            {
                _roundNumber = 1;
                if (warmupSeconds > 0f)
                    _SetState(GS.Warmup, gameWorld);
                else
                    _SetState(GS.Match, gameWorld);
            }
            SpawnPlayerEntity(null, gameWorld);
        }

        public override void OnWorldEnd(GameWorld gameWorld)
        {
            if (gameWorld && gameWorld.IsServerSide)
            {
                gameWorld.Server_ClearMatchInfo();
            }
        }

        public override void OnPlayerJoined(GamePlayer player, GameWorld gameWorld)
        {
            if (player == null || gameWorld == null)
                return;

            if (gameWorld.IsServerSide)
            {
                if (!gameWorld.Server_HasPlayerData(player))
                    gameWorld.Server_SetPlayerData(player, new DefaultPlayerData());
            }

            if (string.Equals(player.Name, "spec", StringComparison.OrdinalIgnoreCase))
            {
                var targetUuid = OnGetNextSpectateTarget(player, gameWorld);
                if (targetUuid.IsValid)
                    gameWorld.Server_SetSpectatorTarget(player, targetUuid);
                return;
            }

            if (gameWorld.IsServerSide && string.IsNullOrWhiteSpace(player.TeamName))
            {
                var teams = GetTeams(gameWorld);
                var teamA = teams is { Count: > 0 } ? teams[0]?.Name : null;
                var teamB = teams is { Count: > 1 } ? teams[1]?.Name : null;

                if (!string.IsNullOrWhiteSpace(teamA))
                {
                    var allPlayers = gameWorld.AllPlayers;
                    var countA = allPlayers.Count(p =>
                        p != null &&
                        p.IsOnline &&
                        !p.IsSpectator &&
                        string.Equals(p.TeamName, teamA, StringComparison.OrdinalIgnoreCase));
                    var countB = !string.IsNullOrWhiteSpace(teamB)
                        ? allPlayers.Count(p =>
                            p != null &&
                            p.IsOnline &&
                            !p.IsSpectator &&
                            string.Equals(p.TeamName, teamB, StringComparison.OrdinalIgnoreCase))
                        : int.MaxValue;

                    var assignedTeam = countA <= countB ? teamA : teamB;
                    if (string.IsNullOrWhiteSpace(assignedTeam))
                        assignedTeam = teamA;

                    gameWorld.Server_SetPlayerTeam(player, assignedTeam);
                }
            }

            SpawnPlayerEntity(player, gameWorld);
        }

        public override List<Team> GetTeams(GameWorld gameWorld)
        {
            return new List<Team>
            {
                new("guards", new Color(0f, 120f / 255f, 1f)),
                new("invaders", new Color(220f / 255f, 50f / 255f, 50f / 255f))
            };
        }

        public override Uuid OnGetNextSpectateTarget(GamePlayer spectator, GameWorld gameWorld)
        {
            if (spectator == null || gameWorld == null)
                return default;

            var allPlayers = gameWorld.AllPlayers;
            if (allPlayers == null || allPlayers.Count == 0)
                return default;

            var currentTargetUuid = spectator.SpectatorTargetUuid;
            var currentIndex = -1;
            var validTargetUuids = new List<Uuid>();

            foreach (var player in allPlayers)
            {
                if (player == null || !player.IsOnline || player.Uuid == spectator.Uuid)
                    continue;

                var entity = gameWorld.GetEntityOwnedByPlayer<Entity>(player);
                if (entity != null)
                {
                    var entityUuid = entity.EntityUuid;
                    validTargetUuids.Add(entityUuid);
                    if (entityUuid == currentTargetUuid)
                        currentIndex = validTargetUuids.Count - 1;
                }
            }

            if (validTargetUuids.Count == 0)
                return default;

            if (currentIndex < 0)
                currentIndex = -1;

            var nextIndex = (currentIndex + 1) % validTargetUuids.Count;
            return validTargetUuids[nextIndex];
        }

        public override Uuid OnGetPrevSpectateTarget(GamePlayer spectator, GameWorld gameWorld)
        {
            if (spectator == null || gameWorld == null)
                return default;

            var allPlayers = gameWorld.AllPlayers;
            if (allPlayers == null || allPlayers.Count == 0)
                return default;

            var currentTargetUuid = spectator.SpectatorTargetUuid;
            var currentIndex = -1;
            var validTargetUuids = new List<Uuid>();

            foreach (var player in allPlayers)
            {
                if (player == null || !player.IsOnline || player.Uuid == spectator.Uuid)
                    continue;

                var entity = gameWorld.GetEntityOwnedByPlayer<Entity>(player);
                if (entity != null)
                {
                    var entityUuid = entity.EntityUuid;
                    validTargetUuids.Add(entityUuid);
                    if (entityUuid == currentTargetUuid)
                        currentIndex = validTargetUuids.Count - 1;
                }
            }

            if (validTargetUuids.Count == 0)
                return default;

            if (currentIndex < 0)
                currentIndex = validTargetUuids.Count;

            var prevIndex = currentIndex <= 0 ? validTargetUuids.Count - 1 : currentIndex - 1;
            return validTargetUuids[prevIndex];
        }

        public override void OnTick(float deltaTime, GameWorld gameWorld)
        {
            if (!gameWorld || !gameWorld.IsServerSide)
                return;

            if (_phaseTimerRunning && _phaseTimerRemainingSeconds > 0f)
            {
                _phaseTimerRemainingSeconds = Mathf.Max(0f, _phaseTimerRemainingSeconds - deltaTime);
                if (_phaseTimerRemainingSeconds <= 0f)
                    _phaseTimerRunning = false;
            }

            if (_state == GS.Warmup)
            {
                if (!_phaseTimerRunning && _phaseTimerRemainingSeconds <= 0f)
                    _SetState(GS.Match, gameWorld);
            }
            else if (_state == GS.Match)
            {
                if (!_phaseTimerRunning && _phaseTimerRemainingSeconds <= 0f)
                {
                    if (useRounds && totalRounds > 0 && _roundNumber < totalRounds)
                    {
                        _roundNumber++;
                        _StartRoundTimerAndMatchInfo(gameWorld);
                    }
                    else
                    {
                        _SetState(GS.MatchOver, gameWorld);
                    }
                }
            }

            _UpdateMatchInfo(gameWorld);
        }
        #endregion

        #region Private Methods
        private void _SetState(GS state, GameWorld gameWorld)
        {
            _state = state;
            _lastSentMatchTimeSeconds = int.MinValue;
            _lastSentMatchMessage = null;
            _lastSentShowTime = false;

            if (_state == GS.Warmup)
            {
                _phaseTimerDurationSeconds = Mathf.Max(0f, warmupSeconds);
                _phaseTimerRemainingSeconds = _phaseTimerDurationSeconds;
                _phaseTimerRunning = _phaseTimerDurationSeconds > 0f;
            }
            else if (_state == GS.Match)
            {
                if (useRounds && totalRounds > 0)
                {
                    _roundNumber = Mathf.Clamp(_roundNumber, 1, totalRounds);
                    _StartRoundTimerAndMatchInfo(gameWorld);
                    return;
                }

                _phaseTimerDurationSeconds = Mathf.Max(0f, matchDurationSeconds);
                _phaseTimerRemainingSeconds = _phaseTimerDurationSeconds;
                _phaseTimerRunning = _phaseTimerDurationSeconds > 0f;
            }
            else
            {
                _phaseTimerDurationSeconds = 0f;
                _phaseTimerRemainingSeconds = 0f;
                _phaseTimerRunning = false;
            }

            _UpdateMatchInfo(gameWorld);
        }

        private void _StartRoundTimerAndMatchInfo(GameWorld gameWorld)
        {
            _phaseTimerDurationSeconds = Mathf.Max(0f, roundDurationSeconds);
            _phaseTimerRemainingSeconds = _phaseTimerDurationSeconds;
            _phaseTimerRunning = _phaseTimerDurationSeconds > 0f;

            _UpdateMatchInfo(gameWorld);
        }

        private void _UpdateMatchInfo(GameWorld gameWorld)
        {
            var message = _GetMatchMessage();
            var showTime = _ShouldShowTime();
            var timeSeconds = 0;
            if (showTime)
            {
                var remaining = _phaseTimerRemainingSeconds;
                timeSeconds = (_phaseTimerRunning || remaining > 0f)
                    ? Mathf.CeilToInt(remaining)
                    : Mathf.CeilToInt(_phaseTimerDurationSeconds);
            }

            if (message == _lastSentMatchMessage && showTime == _lastSentShowTime && timeSeconds == _lastSentMatchTimeSeconds)
                return;

            _lastSentMatchMessage = message;
            _lastSentShowTime = showTime;
            _lastSentMatchTimeSeconds = timeSeconds;
            gameWorld.Server_SetMatchInfo(message, showTime, timeSeconds);
        }

        private string _GetMatchMessage()
        {
            if (_state == GS.Warmup)
                return "WARMUP";
            if (_state == GS.Match)
            {
                if (useRounds && totalRounds > 0)
                    return $"ROUND {_roundNumber}/{totalRounds}";
                return "MATCH";
            }
            if (_state == GS.MatchOver)
                return "MATCH OVER";
            return string.Empty;
        }

        private bool _ShouldShowTime()
        {
            if (_state == GS.Match)
                return gameWorldTimerHasPositiveDuration();
            if (_state == GS.Warmup)
                return warmupSeconds > 0f;
            return false;

            bool gameWorldTimerHasPositiveDuration()
            {
                if (useRounds && totalRounds > 0)
                    return roundDurationSeconds > 0f;
                return matchDurationSeconds > 0f;
            }
        }

        private float _GetPhaseDurationSeconds()
        {
            if (_state == GS.Warmup)
                return warmupSeconds;
            if (_state == GS.Match)
            {
                if (useRounds && totalRounds > 0)
                    return roundDurationSeconds;
                return matchDurationSeconds;
            }
            return 0f;
        }

        private void SpawnPlayerEntity(GamePlayer player, GameWorld gameWorld)
        {
            var spawn = ResolveSpawnTransform(player);
            var spawnPosition = spawn ? spawn.position : Vector3.zero;
            var spawnRotation = spawn ? spawn.rotation : Quaternion.identity;
            var pe = gameWorld.Server_SpawnObject<PlayerEntity>(firstPersonPlayerPrefab, spawnPosition, spawnRotation, player);

            pe.Server_SetInventory(new Inventory
            {
                slots = defaultItems.Select(item => item.GetDefaultItemStack()).ToArray(),
            });

            pe.OnDeathEvent += () =>
            {
                if (!pe || !pe.gameObject)
                    return;

                var deadEntityUuid = pe.EntityUuid;
                foreach (var spectatorPlayer in gameWorld.AllPlayers)
                {
                    if (spectatorPlayer == null || !spectatorPlayer.IsSpectator)
                        continue;

                    if (spectatorPlayer.SpectatorTargetUuid == deadEntityUuid)
                    {
                        var nextTargetUuid = OnGetNextSpectateTarget(spectatorPlayer, gameWorld);
                        if (nextTargetUuid.IsValid)
                            gameWorld.Server_SetSpectatorTarget(spectatorPlayer, nextTargetUuid);
                    }
                }

                gameWorld.Server_DespawnObject(pe.gameObject);
                StartCoroutine(RespawnPlayerAfterDelay(player, gameWorld, 3f));
            };
        }

        private Transform ResolveSpawnTransform(GamePlayer player)
        {
            var teamName = player?.TeamName;
            if (teamName == "invaders")
                return GetNextSpawn(invadersSpawnPoints, ref _invadersSpawnIndex);
            if (teamName == "guards")
                return GetNextSpawn(guardsSpawnPoints, ref _guardsSpawnIndex);

            var any = GetNextSpawn(invadersSpawnPoints, ref _invadersSpawnIndex);
            if (any)
                return any;
            return GetNextSpawn(guardsSpawnPoints, ref _guardsSpawnIndex);
        }

        private static Transform GetNextSpawn(Transform[] spawnPoints, ref int index)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                return null;

            var startIndex = Mathf.Clamp(index, 0, spawnPoints.Length - 1);
            for (var i = 0; i < spawnPoints.Length; i++)
            {
                var probeIndex = (startIndex + i) % spawnPoints.Length;
                var spawn = spawnPoints[probeIndex];
                if (spawn)
                {
                    index = (probeIndex + 1) % spawnPoints.Length;
                    return spawn;
                }
            }

            return null;
        }

        private IEnumerator RespawnPlayerAfterDelay(GamePlayer player, GameWorld gameWorld, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (gameWorld && gameWorld.IsServerSide)
                SpawnPlayerEntity(player, gameWorld);
        }
        #endregion
    }
}
