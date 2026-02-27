using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using NetworkEvent = Unity.Networking.Transport.NetworkEvent;

public class PPTRelay : MonoBehaviour
{
    public static PPTRelay Singleton { get; private set; }

    [SerializeField] GameObject[] enableObjsForSpawning; //There objs should then disable themselves in onnetworkspawn

    [SerializeField] GameObject playButtonBlocker;
    [SerializeField] GameObject disconnectedUnexpectedlyPopUp;

    UnityTransport _transport;
    NetworkDriver hostDriver, playerDriver;
    NetworkConnection myClientConnection;
    bool isRelayServerConnected;

    private void Awake()
    {
        Singleton = this;
        DontDestroyOnLoad(gameObject);
    }
    private void Start()
    {
        _transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
    }
    private void Update()
    {
        if(PPTLobby.Singleton != null && PPTLobby.Singleton.InLobby && NetworkManager.Singleton != null) playButtonBlocker.SetActive(PPTLobby.Singleton.NumPlayersJoined != NetworkManager.Singleton.ConnectedClientsList.Count);
    }

    private void OnDestroy()
    {
        //Tell network drivers to disconnect so they can clean up
        if (hostDriver.IsCreated)
        {
            myClientConnection.Disconnect(hostDriver);
            hostDriver.ScheduleUpdate().Complete();

            hostDriver.Dispose();
        }
        if (playerDriver.IsCreated)
        {
            myClientConnection.Disconnect(playerDriver);
            playerDriver.ScheduleUpdate().Complete();

            playerDriver.Dispose();
        }
    }

    public async Task<string> StartHostWithRelay(int maxConnections)
    {
        //Briefly enable objs to spawn
        foreach (GameObject o in enableObjsForSpawning) o.SetActive(true);

        print("Allocating relay server...");

        Allocation a = await RelayService.Instance.CreateAllocationAsync(maxConnections, "us-central1");
        //print("Best region: " + a.Region);

        _transport.SetHostRelayData(a.RelayServer.IpV4, (ushort)a.RelayServer.Port, a.AllocationIdBytes, a.Key, a.ConnectionData);
        var joinCode = await RelayService.Instance.GetJoinCodeAsync(a.AllocationId);

        print("Creating network driver");
        hostDriver = NetworkDriver.Create();
        NetworkConnection connectionFound = hostDriver.Accept();
        while (connectionFound != default)
        {
            connectionFound = hostDriver.Accept();
            Debug.Log("Accepted an incoming connection.");
        }
        myClientConnection = connectionFound;

        if (NetworkManager.Singleton.StartHost())
        {
            isRelayServerConnected = true;
            StartCoroutine(PingRelay());
            NetworkManager.Singleton.OnClientDisconnectCallback += PlayerDisconnected;

            return joinCode;
        }
        else
        {
            Debug.LogWarning("Could not start network host!");

            //Disable objs since they won't spawn so they won't disable themselves
            foreach (GameObject o in enableObjsForSpawning) o.SetActive(false);

            return null;
        }
    }
    public async Task<bool> StartClientWithRelay(string joinCode)
    {
        //Briefly enable objs to spawn
        foreach (GameObject o in enableObjsForSpawning) o.SetActive(true);

        print("Joining relay server as client...");

        JoinAllocation a = await RelayService.Instance.JoinAllocationAsync(joinCode);
        _transport.SetClientRelayData(a.RelayServer.IpV4, (ushort)a.RelayServer.Port, a.AllocationIdBytes, a.Key, a.ConnectionData, a.HostConnectionData);

        print("Creating network driver");
        playerDriver = NetworkDriver.Create();
        NetworkConnection connectionFound = playerDriver.Accept();
        while (connectionFound != default)
        {
            connectionFound = playerDriver.Accept();
            Debug.Log("Accepted an incoming connection.");
        }
        myClientConnection = connectionFound;

        StartCoroutine(PingRelay());

        if (NetworkManager.Singleton.StartClient())
        {
            isRelayServerConnected = true;
            NetworkManager.Singleton.OnClientDisconnectCallback += CheckIfHostLeft;
        }
        else
        {
            Debug.LogWarning("Client couldn't connect to relay server");
            //Disable objs since they won't spawn so they won't disable themselves
            foreach (GameObject o in enableObjsForSpawning) o.SetActive(false);
        }

        return !string.IsNullOrEmpty(joinCode);
    }

    IEnumerator PingRelay()
    {
        while (true)
        {
            yield return new WaitForSeconds(5);

            // Update the NetworkDrivers regularly to ensure the host/player is kept online.
            if (hostDriver.IsCreated && isRelayServerConnected)
            {
                //print("Ping host driver");

                hostDriver.ScheduleUpdate().Complete();

                //Accept incoming client connections
                while (hostDriver.Accept() != default)
                {
                    Debug.Log("Accepted an incoming connection.");
                }
            }

            if (playerDriver.IsCreated && myClientConnection.IsCreated)
            {
                //print("Ping player driver");

                playerDriver.ScheduleUpdate().Complete();

                //Resolve event queue
                NetworkEvent.Type eventType;
                while ((eventType = myClientConnection.PopEvent(playerDriver, out _)) != NetworkEvent.Type.Empty)
                {
                    if (eventType == NetworkEvent.Type.Connect)
                    {
                        Debug.Log("Client connected to the server");
                    }
                    else if (eventType == NetworkEvent.Type.Disconnect)
                    {
                        Debug.Log("Client got disconnected from server");
                        myClientConnection = default(NetworkConnection);
                    }
                }
            }
        }
    }

    public IEnumerator Disconnect()
    {
        isRelayServerConnected = false;

        if (NetworkManager.Singleton != null)
        {
            print("Disconnecting network manager");
            NetworkManager.Singleton.OnClientDisconnectCallback -= PlayerDisconnected;

            NetworkManager.Singleton.Shutdown();
        }

        //Tell network drivers to disconnect so they can clean up
        if (hostDriver.IsCreated)
        {
            myClientConnection.Disconnect(hostDriver);
            hostDriver.ScheduleUpdate().Complete();
        }
        if (playerDriver.IsCreated)
        {
            myClientConnection.Disconnect(playerDriver);
            playerDriver.ScheduleUpdate().Complete();
        }

        yield return new WaitForEndOfFrame();

        if (hostDriver.IsCreated) hostDriver.Dispose();
        if (playerDriver.IsCreated) playerDriver.Dispose();
    }


    //Callbacks

    //Called on host when client disconnects: destroy their tank if game is running
    private void PlayerDisconnected(ulong clientId)
    {
        if(GameManager.Singleton.gameStarted && NetworkManager.Singleton.IsHost) GameManager.Singleton.KILLTANKMWHAHA(clientId);
    }
    private void CheckIfHostLeft(ulong clientId)
    {
        //isRelayServerConnected will be true if the client diesn't know they're disconnecting
        //print("Disconnect. Id mine: " + (clientId == NetworkManager.Singleton.LocalClientId) + " should be connected: " + isRelayServerConnected);
        if(clientId == NetworkManager.Singleton.LocalClientId && isRelayServerConnected)
        {
            //Client was disconnected involunatrily, which means the host left
            print("Host left the game!");
            if (GameManager.Singleton.gameStarted)
            {
                GameManager.Singleton.Exit();
                disconnectedUnexpectedlyPopUp.SetActive(true);
            }
        }
    }
}
