using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TUA.Core;
using TUA.Entities;
using TUA.I18n;
using TUA.Misc;
using TUA.Systems;
using UnityEngine;

namespace TUA.GameModes
{
    [PlayerData("default")]
    [Serializable]
    public struct DefaultPlayerData : IPlayerData
    {
        public int KillCount;
        
        public void Serialize(INetWriter writer)
        {
            writer.WriteInt32(KillCount);
        }

        public IPlayerData Deserialize(INetReader reader)
        {
            KillCount = reader.ReadInt32();
            return this;
        }
    }

    public struct DefaultGameSettings : IGameSettings
    {
    }

    public class DefaultGameMode : GameMode<DefaultPlayerData, DefaultGameSettings>
    {
        private const string MatchInfoWarmupKey = "hud.match.warmup";
        private const string MatchInfoMatchKey = "hud.match.match";
        private const string MatchInfoRoundKey = "hud.match.round";
        private const string MatchInfoMatchOverKey = "hud.match.match_over";
        private const string ResultsMatchOverTitleKey = "results.title.match_over";
        private const string ResultsInvadersWinTitleKey = "results.title.invaders_win";
        private const string ResultsGuardsWinTitleKey = "results.title.guards_win";
        private const string ResultsDisksDeliveredObjectiveKey = "results.objective.disks_delivered";
        private const string ScoreboardInvadersHeaderKey = "scoreboard.team.invaders";
        private const string ScoreboardGuardsHeaderKey = "scoreboard.team.guards";
        private const string ScoreboardUnassignedHeaderKey = "scoreboard.team.unassigned";
        private const string ScoreboardDisksDeliveredKey = "scoreboard.stat.disks_delivered";
        private const string ScoreboardDisksUndeliveredKey = "scoreboard.stat.disks_undelivered";

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
        private string _lastSentMatchKey;
        private int _lastSentMatchArg0;
        private int _lastSentMatchArg1;
        private bool _lastSentShowTime;
        private GameWorld _activeWorld;
        #endregion

        #region Public Methods
        public override void OnWorldStart(GameWorld gameWorld, DefaultGameSettings gameSettings)
        {
            Debug.Log("DefaultGameMode OnWorldStart");
            if (gameWorld && gameWorld.IsServerSide)
            {
                _activeWorld = gameWorld;
                FeedSystem.OnKillEvent -= _OnKillEvent;
                FeedSystem.OnKillEvent += _OnKillEvent;

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
                FeedSystem.OnKillEvent -= _OnKillEvent;
                _activeWorld = null;
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

        public override void BuildScoreboard(GameWorld gameWorld, List<ScoreboardSection> sections)
        {
            if (gameWorld == null || sections == null)
                return;

            sections.Clear();

            var teams = gameWorld.GetTeams() ?? new List<Team>();
            var allTargets = gameWorld.GetEntities<HackingTarget>();
            var deliveredDisks = allTargets.Count(t => t && t.IsDelivered);

            foreach (var team in teams)
            {
                if (team == null || string.IsNullOrWhiteSpace(team.Name))
                    continue;

                var section = new ScoreboardSection
                {
                    TeamName = team.Name,
                    Header = team.Name,
                    TeamColor = team.Color
                };

                if (string.Equals(team.Name, "invaders", StringComparison.OrdinalIgnoreCase))
                {
                    section.Header = LocalizationManager.Get(ScoreboardInvadersHeaderKey);
                    section.StatLine = LocalizationManager.Get(ScoreboardDisksDeliveredKey, deliveredDisks);
                }
                else if (string.Equals(team.Name, "guards", StringComparison.OrdinalIgnoreCase))
                {
                    section.Header = LocalizationManager.Get(ScoreboardGuardsHeaderKey);
                    section.StatLine = LocalizationManager.Get(ScoreboardDisksUndeliveredKey, Mathf.Max(0, allTargets.Count - deliveredDisks));
                }

                var players = gameWorld.GetPlayersInTeam(team.Name)
                    .Where(p => p != null && p.IsOnline && !p.IsSpectator)
                    .OrderBy(p => p.Name)
                    .ToList();

                foreach (var player in players)
                {
                    var kills = 0;
                    if (gameWorld.IsServerSide)
                    {
                        if (gameWorld.Server_GetPlayerData(player) is DefaultPlayerData serverData)
                            kills = serverData.KillCount;
                    }
                    else
                    {
                        if (gameWorld.Client_GetPlayerData(player) is DefaultPlayerData clientData)
                            kills = clientData.KillCount;
                    }

                    section.Players.Add(new ScoreboardPlayerEntry
                    {
                        PlayerUuid = player.Uuid,
                        PlayerName = player.Name ?? string.Empty,
                        Kills = kills
                    });
                }

                sections.Add(section);
            }

            var teamNames = new HashSet<string>(teams.Where(t => t != null && !string.IsNullOrWhiteSpace(t.Name)).Select(t => t.Name), StringComparer.OrdinalIgnoreCase);
            var unassignedPlayers = (gameWorld.AllPlayers ?? new List<GamePlayer>())
                .Where(p => p != null && p.IsOnline && !p.IsSpectator && (string.IsNullOrWhiteSpace(p.TeamName) || !teamNames.Contains(p.TeamName)))
                .OrderBy(p => p.Name)
                .ToList();
            if (unassignedPlayers.Count > 0)
            {
                var unassigned = new ScoreboardSection
                {
                    TeamName = "unassigned",
                    Header = LocalizationManager.Get(ScoreboardUnassignedHeaderKey),
                    TeamColor = Color.white
                };

                foreach (var player in unassignedPlayers)
                {
                    var kills = 0;
                    if (gameWorld.IsServerSide)
                    {
                        if (gameWorld.Server_GetPlayerData(player) is DefaultPlayerData serverData)
                            kills = serverData.KillCount;
                    }
                    else
                    {
                        if (gameWorld.Client_GetPlayerData(player) is DefaultPlayerData clientData)
                            kills = clientData.KillCount;
                    }

                    unassigned.Players.Add(new ScoreboardPlayerEntry
                    {
                        PlayerUuid = player.Uuid,
                        PlayerName = player.Name ?? string.Empty,
                        Kills = kills
                    });
                }

                sections.Add(unassigned);
            }
        }

        public override void BuildMatchResults(GameWorld gameWorld, MatchResultsData results)
        {
            if (results == null)
                return;

            results.Title = LocalizationManager.Get(ResultsMatchOverTitleKey);
            results.ObjectiveLine = null;

            if (gameWorld == null)
                return;

            var allTargets = gameWorld.GetEntities<HackingTarget>();
            var totalTargets = allTargets.Count;
            if (totalTargets <= 0)
                return;

            var deliveredDisks = allTargets.Count(t => t && t.IsDelivered);
            results.ObjectiveLine = LocalizationManager.Get(ResultsDisksDeliveredObjectiveKey, deliveredDisks, totalTargets);
            results.Title = LocalizationManager.Get(deliveredDisks >= totalTargets ? ResultsInvadersWinTitleKey : ResultsGuardsWinTitleKey);
        }

        public override string GetMatchInfoText(GameWorld world)
        {
            if (world == null || string.IsNullOrWhiteSpace(world.MatchInfoKey))
                return string.Empty;

            if (string.Equals(world.MatchInfoKey, MatchInfoRoundKey, StringComparison.Ordinal))
                return LocalizationManager.Get(MatchInfoRoundKey, world.MatchInfoArg0, world.MatchInfoArg1);

            return LocalizationManager.Get(world.MatchInfoKey);
        }

        public override bool IsMatchOver(GameWorld world)
        {
            if (world == null)
                return false;

            if (world.IsServerSide)
                return _state == GS.MatchOver;

            return string.Equals(world.MatchInfoKey, MatchInfoMatchOverKey, StringComparison.Ordinal);
        }

        public override DefaultPlayerData GetPlayerDataSnapshot(GamePlayer player, DefaultPlayerData fullData, GamePlayer requestingPlayer, GameWorld gameWorld)
        {
            return fullData;
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
                var allTargets = gameWorld.GetEntities<HackingTarget>();
                if (allTargets.Count > 0)
                {
                    var deliveredDisks = allTargets.Count(t => t && t.IsDelivered);
                    if (deliveredDisks >= allTargets.Count)
                    {
                        _SetState(GS.MatchOver, gameWorld);
                        return;
                    }
                }

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

        private void _OnKillEvent(GamePlayer killer, GamePlayer victim)
        {
            if (_activeWorld == null || !_activeWorld.IsServerSide)
                return;
            
            if (killer == null)
                return;
            
            if (_activeWorld.Server_GetPlayerData(killer) is DefaultPlayerData data)
                data.KillCount = Mathf.Max(0, data.KillCount) + 1;
            else
                data = new DefaultPlayerData { KillCount = 1 };

            _activeWorld.Server_SetPlayerData(killer, data);
        }
        #endregion

        #region Private Methods
        private void _SetState(GS state, GameWorld gameWorld)
        {
            var previousState = _state;
            _state = state;
            _lastSentMatchTimeSeconds = int.MinValue;
            _lastSentMatchKey = null;
            _lastSentMatchArg0 = 0;
            _lastSentMatchArg1 = 0;
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

            if (previousState != GS.MatchOver && _state == GS.MatchOver)
                _KillAllPlayers(gameWorld);
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
            _GetMatchInfo(out var key, out var arg0, out var arg1);
            var showTime = _ShouldShowTime();
            var timeSeconds = 0;
            if (showTime)
            {
                var remaining = _phaseTimerRemainingSeconds;
                timeSeconds = (_phaseTimerRunning || remaining > 0f)
                    ? Mathf.CeilToInt(remaining)
                    : Mathf.CeilToInt(_phaseTimerDurationSeconds);
            }

            if (key == _lastSentMatchKey && arg0 == _lastSentMatchArg0 && arg1 == _lastSentMatchArg1 && showTime == _lastSentShowTime && timeSeconds == _lastSentMatchTimeSeconds)
                return;

            _lastSentMatchKey = key;
            _lastSentMatchArg0 = arg0;
            _lastSentMatchArg1 = arg1;
            _lastSentShowTime = showTime;
            _lastSentMatchTimeSeconds = timeSeconds;
            gameWorld.Server_SetMatchInfo(key, arg0, arg1, showTime, timeSeconds);
        }

        private void _GetMatchInfo(out string key, out int arg0, out int arg1)
        {
            arg0 = 0;
            arg1 = 0;

            if (_state == GS.Warmup)
            {
                key = MatchInfoWarmupKey;
                return;
            }
            if (_state == GS.Match)
            {
                if (useRounds && totalRounds > 0)
                {
                    key = MatchInfoRoundKey;
                    arg0 = _roundNumber;
                    arg1 = totalRounds;
                    return;
                }
                key = MatchInfoMatchKey;
                return;
            }
            if (_state == GS.MatchOver)
            {
                key = MatchInfoMatchOverKey;
                return;
            }

            key = string.Empty;
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
                if (_state != GS.MatchOver)
                    StartCoroutine(RespawnPlayerAfterDelay(player, gameWorld, 3f));
            };
        }

        private static void _KillAllPlayers(GameWorld gameWorld)
        {
            if (!gameWorld || !gameWorld.IsServerSide)
                return;

            var players = gameWorld.GetEntities<PlayerEntity>();
            foreach (var player in players)
            {
                gameWorld.Server_DespawnObject(player.gameObject);
            }
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
