using System;
using System.Threading.Tasks;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Core;
using Unity.Services.Authentication;

namespace TUA.Temp
{
    [MovedFrom("TUA.Interface")]
    public class RelayLobbyGUI : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Maximum number of connections for the host")]
        [SerializeField] private int maxConnections = 4;
        [Tooltip("Connection type: udp, dtls, or wss (for WebGL)")]
        [SerializeField] private string connectionType = "dtls";
        [Header("GUI Settings")]
        [Tooltip("Show room code field")]
        [SerializeField] private bool showRoomCode = true;
        [Tooltip("Enable extra Debug.Log* output for troubleshooting")]
        [SerializeField] private bool verboseLogs = false;
        [Tooltip("GUI window position")]
        [SerializeField] private Rect windowRect = new Rect(10, 10, 280, 220);
        private string roomCode = "";
        private string joinCodeInput = "";
        private string playerName = "";
        private bool isConnecting = false;
        private string statusMessage = "";
        private bool isInitialized = false;
        private NetworkManager networkManager;
        private bool isHostStarted = false;
        private bool isClientStarted = false;
        private void OnEnable()
        {
            playerName = PlayerPrefs.GetString("TUA.PlayerName", string.Empty);
            if (string.IsNullOrEmpty(playerName))
            {
                System.Random random = new System.Random();
                int playerNumber = random.Next(100, 1000);
                playerName = $"Player_{playerNumber}";
            }
        }
        private void LogError(string message, Exception e = null)
        {
            if (verboseLogs)
                Debug.LogError(e == null ? message : $"{message}\n{e}");
        }
        private void Start()
        {
            networkManager = InstanceFinder.NetworkManager;
            if (networkManager == null)
            {
                Debug.LogError("[RelayLobbyGUI] NetworkManager not found!");
                enabled = false;
                return;
            }
            if (networkManager.ServerManager != null)
                networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
            if (networkManager.ClientManager != null)
                networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            _ = InitializeUnityServices();
        }
        private void OnDestroy()
        {
            if (networkManager != null)
            {
                if (networkManager.ServerManager != null)
                    networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
                if (networkManager.ClientManager != null)
                    networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            }
        }
        private async Task InitializeUnityServices()
        {
            try
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                isInitialized = true;
                statusMessage = "Ready";
            }
            catch (Exception e)
            {
                statusMessage = $"Init failed: {e.Message}";
                LogError("[RelayLobbyGUI] Failed to initialize Unity Services.", e);
            }
        }
        private void OnGUI()
        {
            if (networkManager == null)
                return;
            windowRect.height = 20f;
            float minWidth = 200f;
            windowRect.width = Mathf.Max(minWidth, windowRect.width);
            windowRect = GUILayout.Window(0, windowRect, DrawWindow, "Relay", GUILayout.ExpandWidth(false));
        }
        private void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            if (!string.IsNullOrEmpty(statusMessage))
                GUILayout.Label($"Status: {statusMessage}");
            bool isConnected = isHostStarted || isClientStarted;
            bool canShowIdleControls = !isConnecting && !isConnected;
            if (canShowIdleControls)
            {
                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Name", GUILayout.Width(36));
                string newPlayerName = GUILayout.TextField(playerName, GUILayout.Height(22));
                if (newPlayerName != playerName)
                {
                    playerName = newPlayerName;
                    PlayerPrefs.SetString("TUA.PlayerName", playerName);
                    PlayerPrefs.Save();
                }
                GUILayout.EndHorizontal();
            }
            else if (isConnected)
            {
                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Name", GUILayout.Width(36));
                GUI.enabled = false;
                GUILayout.TextField(playerName, GUILayout.Height(22));
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            if (canShowIdleControls)
            {
                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Host", GUILayout.Height(24), GUILayout.Width(70)))
                    _ = StartHostWithRelay();
                GUILayout.Space(6);
                GUILayout.Label("Code", GUILayout.Width(36));
                joinCodeInput = GUILayout.TextField(joinCodeInput, GUILayout.Height(22));
                if (!string.IsNullOrEmpty(joinCodeInput) && GUILayout.Button("Join", GUILayout.Height(24), GUILayout.Width(70)))
                    _ = StartClientWithRelay(joinCodeInput);
                GUILayout.EndHorizontal();
            }
            if (showRoomCode && isHostStarted && !string.IsNullOrEmpty(roomCode))
            {
                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Room", GUILayout.Width(36));
                GUI.enabled = false;
                GUILayout.TextField(roomCode, GUILayout.Height(22));
                GUI.enabled = true;
                if (GUILayout.Button("Copy", GUILayout.Height(24), GUILayout.Width(70)))
                {
                    GUIUtility.systemCopyBuffer = roomCode;
                    statusMessage = "Code copied!";
                }
                GUILayout.EndHorizontal();
            }
            if (isConnected && !isConnecting)
            {
                GUILayout.Space(6);
                string connectionLabel = isHostStarted ? "Hosting" : "Client";
                GUILayout.BeginHorizontal();
                GUILayout.Label(connectionLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Disconnect", GUILayout.Height(24), GUILayout.Width(90)))
                    Disconnect();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUI.DragWindow();
        }
        public string GetPlayerName()
        {
            return playerName;
        }
        public string GetRoomCode()
        {
            return roomCode;
        }
        private async Task StartHostWithRelay()
        {
            if (!isInitialized)
            {
                statusMessage = "Initializing...";
                await InitializeUnityServices();
            }
            if (!isInitialized)
            {
                statusMessage = "Failed to initialize";
                return;
            }
            isConnecting = true;
            statusMessage = "Creating relay allocation...";
            string joinCode = "";
            try
            {
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
                joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                var unityTransport = networkManager.TransportManager.GetTransport<UnityTransport>();
                if (unityTransport == null)
                {
                    statusMessage = "UnityTransport not found!";
                    isConnecting = false;
                    return;
                }
                RelayServerData relayData = BuildRelayServerData(allocation, connectionType);
                unityTransport.SetRelayServerData(relayData);
                if (networkManager.ServerManager.StartConnection())
                {
                    networkManager.ClientManager.StartConnection();
                    roomCode = joinCode;
                    isHostStarted = true;
                    statusMessage = $"Host started! Code: {joinCode}";
                }
                else
                    statusMessage = "Failed to start host";
            }
            catch (Exception e)
            {
                statusMessage = $"Host failed: {e.Message}";
                LogError("[RelayLobbyGUI] Failed to start host.", e);
            }
            finally
            {
                isConnecting = false;
            }
        }
        private async Task StartClientWithRelay(string joinCode)
        {
            if (string.IsNullOrEmpty(joinCode))
            {
                statusMessage = "Please enter a join code";
                return;
            }
            if (!isInitialized)
            {
                statusMessage = "Initializing...";
                await InitializeUnityServices();
            }
            if (!isInitialized)
            {
                statusMessage = "Failed to initialize";
                return;
            }
            isConnecting = true;
            statusMessage = "Joining relay...";
            try
            {
                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                var unityTransport = networkManager.TransportManager.GetTransport<UnityTransport>();
                if (unityTransport == null)
                {
                    statusMessage = "UnityTransport not found!";
                    isConnecting = false;
                    return;
                }
                RelayServerData relayData = BuildRelayServerData(allocation, connectionType);
                unityTransport.SetRelayServerData(relayData);
                if (networkManager.ClientManager.StartConnection())
                {
                    isClientStarted = true;
                    statusMessage = "Connected!";
                }
                else
                    statusMessage = "Failed to connect";
            }
            catch (Exception e)
            {
                statusMessage = $"Join failed: {e.Message}";
                LogError("[RelayLobbyGUI] Failed to join.", e);
            }
            finally
            {
                isConnecting = false;
            }
        }
        private void Disconnect()
        {
            statusMessage = "Disconnecting...";
            if (networkManager != null && networkManager.IsClientStarted)
                networkManager.ClientManager.StopConnection();
            if (networkManager != null && networkManager.IsServerStarted)
                networkManager.ServerManager.StopConnection(true);
            isHostStarted = false;
            isClientStarted = false;
            roomCode = "";
            joinCodeInput = "";
            statusMessage = "Disconnected";
        }
        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    isHostStarted = true;
                    break;
                case LocalConnectionState.Stopped:
                    isHostStarted = false;
                    roomCode = "";
                    if (!isClientStarted)
                        statusMessage = "Disconnected";
                    break;
            }
        }
        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    isClientStarted = true;
                    if (!string.IsNullOrEmpty(playerName))
                    {
                        PlayerPrefs.SetString("TUA.PlayerName", playerName);
                        PlayerPrefs.Save();
                    }
                    break;
                case LocalConnectionState.Stopped:
                    isClientStarted = false;
                    if (!isHostStarted)
                        statusMessage = "Disconnected";
                    break;
            }
        }
        private static RelayServerEndpoint FindEndpoint(System.Collections.Generic.List<RelayServerEndpoint> endpoints, string connectionType)
        {
            if (endpoints == null || endpoints.Count == 0)
                throw new ArgumentException("Relay allocation does not contain any server endpoints.");
            string ct = (connectionType ?? "dtls").Trim().ToLowerInvariant();
            for (int i = 0; i < endpoints.Count; i++)
            {
                var ep = endpoints[i];
                if (ep == null)
                    continue;
                if (string.Equals(ep.ConnectionType, ct, StringComparison.OrdinalIgnoreCase))
                    return ep;
            }
            throw new ArgumentException($"No relay endpoint found for connectionType='{ct}'.");
        }
        private static byte[] FixRelayHmacKey(byte[] keyBytes)
        {
            if (keyBytes == null)
                throw new ArgumentException("Relay key is null.");
            if (keyBytes.Length == 64)
                return keyBytes;
            try
            {
                string s = Encoding.UTF8.GetString(keyBytes);
                byte[] decoded = Convert.FromBase64String(s);
                if (decoded.Length == 64)
                    return decoded;
            }
            catch
            {
            }
            try
            {
                string s = Encoding.UTF8.GetString(keyBytes).Trim();
                s = s.Replace('-', '+').Replace('_', '/');
                switch (s.Length % 4)
                {
                    case 2: s += "=="; break;
                    case 3: s += "="; break;
                }
                byte[] decoded = Convert.FromBase64String(s);
                if (decoded.Length == 64)
                    return decoded;
            }
            catch
            {
            }
            throw new ArgumentException($"Relay HMAC key length is invalid. Expected 64 bytes (or base64 encoding of 64 bytes), but got {keyBytes.Length} bytes.");
        }
        private static RelayServerData BuildRelayServerData(Allocation allocation, string connectionType)
        {
            if (allocation == null)
                throw new ArgumentException("Allocation is null.");
            RelayServerEndpoint ep = FindEndpoint(allocation.ServerEndpoints, connectionType);
            byte[] key = FixRelayHmacKey(allocation.Key);
            bool isWebSocket = string.Equals((connectionType ?? "").Trim(), "wss", StringComparison.OrdinalIgnoreCase);
            return new RelayServerData(
                host: ep.Host,
                port: (ushort)ep.Port,
                allocationId: allocation.AllocationIdBytes,
                connectionData: allocation.ConnectionData,
                hostConnectionData: allocation.ConnectionData,
                key: key,
                isSecure: ep.Secure,
                isWebSocket: isWebSocket
            );
        }
        private static RelayServerData BuildRelayServerData(JoinAllocation allocation, string connectionType)
        {
            if (allocation == null)
                throw new ArgumentException("JoinAllocation is null.");
            RelayServerEndpoint ep = FindEndpoint(allocation.ServerEndpoints, connectionType);
            byte[] key = FixRelayHmacKey(allocation.Key);
            bool isWebSocket = string.Equals((connectionType ?? "").Trim(), "wss", StringComparison.OrdinalIgnoreCase);
            return new RelayServerData(
                host: ep.Host,
                port: (ushort)ep.Port,
                allocationId: allocation.AllocationIdBytes,
                connectionData: allocation.ConnectionData,
                hostConnectionData: allocation.HostConnectionData,
                key: key,
                isSecure: ep.Secure,
                isWebSocket: isWebSocket
            );
        }
    }
}
