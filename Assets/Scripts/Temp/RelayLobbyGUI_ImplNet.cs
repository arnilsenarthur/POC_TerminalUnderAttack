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
        [SerializeField] private bool verboseLogs;
        [Tooltip("GUI window position")]
        [SerializeField] private Rect windowRect = new(10, 10, 280, 220);
        
        private string _roomCode = "";
        private string _joinCodeInput = "";
        private string _playerName = "";
        private bool _isConnecting;
        private string _statusMessage = "";
        private bool _isInitialized;
        private NetworkManager _networkManager;
        private bool _isHostStarted;
        private bool _isClientStarted;
        
        private void OnEnable()
        {
            _playerName = PlayerPrefs.GetString("TUA.PlayerName", string.Empty);
            if (!string.IsNullOrEmpty(_playerName)) 
                return;
            
            var random = new System.Random();
            var playerNumber = random.Next(100, 1000);
            _playerName = $"Player_{playerNumber}";
        }
        
        private void LogError(string message, Exception e = null)
        {
            if (verboseLogs)
                Debug.LogError(e == null ? message : $"{message}\n{e}");
        }
        
        private void Start()
        {
            _networkManager = InstanceFinder.NetworkManager;
            
            if (!_networkManager)
            {
                Debug.LogError("[RelayLobbyGUI] NetworkManager not found!");
                enabled = false;
                return;
            }
            
            if (_networkManager.ServerManager != null)
                _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
            
            if (_networkManager.ClientManager != null)
                _networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
            
            _ = InitializeUnityServices();
        }
        
        private void OnDestroy()
        {
            if (_networkManager)
            {
                if (_networkManager.ServerManager)
                    _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
                
                if (_networkManager.ClientManager)
                    _networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            }
        }
        private async Task InitializeUnityServices()
        {
            try
            {
                await UnityServices.InitializeAsync();
                
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                
                _isInitialized = true;
                _statusMessage = "Ready";
            }
            catch (Exception e)
            {
                _statusMessage = $"Init failed: {e.Message}";
                LogError("[RelayLobbyGUI] Failed to initialize Unity Services.", e);
            }
        }
        
        private void OnGUI()
        {
            if (!_networkManager)
                return;
            
            windowRect.height = 20f;
            var minWidth = 200f;
            windowRect.width = Mathf.Max(minWidth, windowRect.width);
            windowRect = GUILayout.Window(0, windowRect, DrawWindow, "Relay", GUILayout.ExpandWidth(false));
        }
        
        private void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            if (!string.IsNullOrEmpty(_statusMessage))
                GUILayout.Label($"Status: {_statusMessage}");
            var isConnected = _isHostStarted || _isClientStarted;
            var canShowIdleControls = !_isConnecting && !isConnected;
            
            if (canShowIdleControls)
            {
                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Name", GUILayout.Width(36));
                var newPlayerName = GUILayout.TextField(_playerName, GUILayout.Height(22));
                if (newPlayerName != _playerName)
                {
                    _playerName = newPlayerName;
                    PlayerPrefs.SetString("TUA.PlayerName", _playerName);
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
                GUILayout.TextField(_playerName, GUILayout.Height(22));
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
                _joinCodeInput = GUILayout.TextField(_joinCodeInput, GUILayout.Height(22));
                if (!string.IsNullOrEmpty(_joinCodeInput) && GUILayout.Button("Join", GUILayout.Height(24), GUILayout.Width(70)))
                    _ = StartClientWithRelay(_joinCodeInput);
                GUILayout.EndHorizontal();
            }
            
            if (showRoomCode && _isHostStarted && !string.IsNullOrEmpty(_roomCode))
            {
                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Room", GUILayout.Width(36));
                GUI.enabled = false;
                GUILayout.TextField(_roomCode, GUILayout.Height(22));
                GUI.enabled = true;
                if (GUILayout.Button("Copy", GUILayout.Height(24), GUILayout.Width(70)))
                {
                    GUIUtility.systemCopyBuffer = _roomCode;
                    _statusMessage = "Code copied!";
                }
                GUILayout.EndHorizontal();
            }
            
            if (isConnected && !_isConnecting)
            {
                GUILayout.Space(6);
                var connectionLabel = _isHostStarted ? "Hosting" : "Client";
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
            return _playerName;
        }
        
        public string GetRoomCode()
        {
            return _roomCode;
        }
        
        private async Task StartHostWithRelay()
        {
            if (!_isInitialized)
            {
                _statusMessage = "Initializing...";
                await InitializeUnityServices();
            }
            if (!_isInitialized)
            {
                _statusMessage = "Failed to initialize";
                return;
            }
            _isConnecting = true;
            _statusMessage = "Creating relay allocation...";
            
            try
            {
                var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
                var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                var unityTransport = _networkManager.TransportManager.GetTransport<UnityTransport>();
                if (!unityTransport)
                {
                    _statusMessage = "UnityTransport not found!";
                    _isConnecting = false;
                    return;
                }
                
                var relayData = BuildRelayServerData(allocation, connectionType);
                unityTransport.SetRelayServerData(relayData);
                if (_networkManager.ServerManager.StartConnection())
                {
                    _networkManager.ClientManager.StartConnection();
                    _roomCode = joinCode;
                    _isHostStarted = true;
                    _statusMessage = $"Host started! Code: {joinCode}";
                }
                else
                    _statusMessage = "Failed to start host";
            }
            catch (Exception e)
            {
                _statusMessage = $"Host failed: {e.Message}";
                LogError("[RelayLobbyGUI] Failed to start host.", e);
            }
            finally
            {
                _isConnecting = false;
            }
        }
        
        private async Task StartClientWithRelay(string joinCode)
        {
            if (string.IsNullOrEmpty(joinCode))
            {
                _statusMessage = "Please enter a join code";
                return;
            }
            
            if (!_isInitialized)
            {
                _statusMessage = "Initializing...";
                await InitializeUnityServices();
            }
            
            if (!_isInitialized)
            {
                _statusMessage = "Failed to initialize";
                return;
            }
            
            _isConnecting = true;
            _statusMessage = "Joining relay...";
            try
            {
                var allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                var unityTransport = _networkManager.TransportManager.GetTransport<UnityTransport>();
                if (!unityTransport)
                {
                    _statusMessage = "UnityTransport not found!";
                    _isConnecting = false;
                    return;
                }
                var relayData = BuildRelayServerData(allocation, connectionType);
                unityTransport.SetRelayServerData(relayData);
                if (_networkManager.ClientManager.StartConnection())
                {
                    _isClientStarted = true;
                    _statusMessage = "Connected!";
                }
                else
                    _statusMessage = "Failed to connect";
            }
            catch (Exception e)
            {
                _statusMessage = $"Join failed: {e.Message}";
                LogError("[RelayLobbyGUI] Failed to join.", e);
            }
            finally
            {
                _isConnecting = false;
            }
        }
        
        private void Disconnect()
        {
            _statusMessage = "Disconnecting...";
            if (_networkManager != null && _networkManager.IsClientStarted)
                _networkManager.ClientManager.StopConnection();
            
            if (_networkManager != null && _networkManager.IsServerStarted)
                _networkManager.ServerManager.StopConnection(true);
            
            _isHostStarted = false;
            _isClientStarted = false;
            _roomCode = "";
            _joinCodeInput = "";
            _statusMessage = "Disconnected";
        }
        
        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    _isHostStarted = true;
                    break;
                case LocalConnectionState.Stopped:
                    _isHostStarted = false;
                    _roomCode = "";
                    if (!_isClientStarted)
                        _statusMessage = "Disconnected";
                    break;
            }
        }
        
        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    _isClientStarted = true;
                    if (!string.IsNullOrEmpty(_playerName))
                    {
                        PlayerPrefs.SetString("TUA.PlayerName", _playerName);
                        PlayerPrefs.Save();
                    }
                    break;
                case LocalConnectionState.Stopped:
                    _isClientStarted = false;
                    if (!_isHostStarted)
                        _statusMessage = "Disconnected";
                    break;
            }
        }
        
        private static RelayServerEndpoint FindEndpoint(System.Collections.Generic.List<RelayServerEndpoint> endpoints, string connectionType)
        {
            if (endpoints == null || endpoints.Count == 0)
                throw new ArgumentException("Relay allocation does not contain any server endpoints.");
            var ct = (connectionType ?? "dtls").Trim().ToLowerInvariant();
            foreach (var ep in endpoints)
            {
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
                var s = Encoding.UTF8.GetString(keyBytes);
                var decoded = Convert.FromBase64String(s);
                if (decoded.Length == 64)
                    return decoded;
            }
            catch
            {
                // ignored
            }

            try
            {
                var s = Encoding.UTF8.GetString(keyBytes).Trim();
                s = s.Replace('-', '+').Replace('_', '/');
                switch (s.Length % 4)
                {
                    case 2: s += "=="; break;
                    case 3: s += "="; break;
                }
                var decoded = Convert.FromBase64String(s);
                if (decoded.Length == 64)
                    return decoded;
            }
            catch
            {
                // ignored
            }

            throw new ArgumentException($"Relay HMAC key length is invalid. Expected 64 bytes (or base64 encoding of 64 bytes), but got {keyBytes.Length} bytes.");
        }
        
        private static RelayServerData BuildRelayServerData(Allocation allocation, string connectionType)
        {
            if (allocation == null)
                throw new ArgumentException("Allocation is null.");
            
            var ep = FindEndpoint(allocation.ServerEndpoints, connectionType);
            var key = FixRelayHmacKey(allocation.Key);
            var isWebSocket = string.Equals((connectionType ?? "").Trim(), "wss", StringComparison.OrdinalIgnoreCase);
            
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
            
            var ep = FindEndpoint(allocation.ServerEndpoints, connectionType);
            var key = FixRelayHmacKey(allocation.Key);
            var isWebSocket = string.Equals((connectionType ?? "").Trim(), "wss", StringComparison.OrdinalIgnoreCase);
            
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
