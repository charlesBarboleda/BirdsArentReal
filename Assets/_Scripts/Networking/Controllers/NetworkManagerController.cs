using System;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

namespace NGONetworking
{
    /// <summary>
    /// Add this component to the same GameObject as
    /// the NetworkManager component.
    /// </summary>
    public class NetworkManagerController : MonoBehaviour, IInitializable
    {
        [Header("References")]
        [SerializeField] NetworkManager m_NetworkManager;

        [Header("IInitializable")]
        public bool IsInitialized { get; private set; }
        public event Action OnInitializationStart;
        public event Action<bool> OnInitializationFinish;

        public async Awaitable InitializeAsync()
        {
            OnInitializationStart?.Invoke();

            if (m_NetworkManager == null && !TryGetComponent(out m_NetworkManager))
            {
                Debug.LogError($"[NetworkManagerController] No NetworkManager found or assigned to m_NetworkManager.");

                enabled = false;
                IsInitialized = false;
                OnInitializationFinish?.Invoke(IsInitialized);
            }

            IsInitialized = true;
            enabled = true;
            OnInitializationFinish?.Invoke(IsInitialized);
            Debug.Log($"[NetworkManagerController] Initialized.");
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 300));
            if (!m_NetworkManager.IsClient && !m_NetworkManager.IsServer)
            {
                StartButtons();
            }
            else
            {
                StatusLabels();
            }

            GUILayout.EndArea();
        }

        void StartButtons()
        {
            if (GUILayout.Button("Host")) m_NetworkManager.StartHost();
            if (GUILayout.Button("Client")) m_NetworkManager.StartClient();
            if (GUILayout.Button("Server")) m_NetworkManager.StartServer();
        }

        void StatusLabels()
        {
            var mode = m_NetworkManager.IsHost ?
                "Host" : m_NetworkManager.IsServer ? "Server" : "Client";

            GUILayout.Label("Transport: " +
                m_NetworkManager.NetworkConfig.NetworkTransport.GetType().Name);
            GUILayout.Label("Mode: " + mode);
        }
    }
}
