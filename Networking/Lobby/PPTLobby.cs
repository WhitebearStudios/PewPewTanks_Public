using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class PPTLobby : MonoBehaviour
{
    public static PPTLobby Singleton { get; private set; }

    [SerializeField] private GameObject[] hostUI;

    [SerializeField] GameObject partyModeMenu, startMenu;
    [SerializeField] GameObject lobbyField;
    [SerializeField] GameObject refreshButtonBlocker;
    [SerializeField] Transform lobbyEntriesParent;

    [SerializeField] TMPro.TextMeshProUGUI lobbyJoinCodeText;
    [SerializeField] GameObject noLobbiesFoundText;
    [SerializeField] TMPro.TextMeshProUGUI header;
    [SerializeField] GameObject connectingText;
    [SerializeField] GameObject kickedPopup;
    [SerializeField] GameObject multiplayerButtonBlocker;
    DotDotDotText ConnectionText => multiplayerButtonBlocker.transform.GetChild(0).GetComponent<DotDotDotText>();

    [SerializeField] GameObject lobbySelection;
    [SerializeField] Transform lobbyPlayerEntriesParent;

    float lobbyRefreshTimer;
    private bool isRefreshing;
    private bool isJoining;
    bool isLeaving;

    private Lobby _myLobby;
    private ILobbyEvents _lobbyEvents;
    bool isHost = false;
    int _playerNum;

    bool signedIn = false;

    public bool InLobby => _myLobby != null;
    public int NumPlayersJoined => _myLobby.Players.Count;
    public bool HostIsNotPlaying => _myLobby.Data["HostPlayerIndex"].Value == "-1";

    public string PlayerId => AuthenticationService.Instance.PlayerId;


    Dictionary<string, string> gamemodes = new Dictionary<string, string>()
    {
        {"0", "Brawl" },
        {"1", "Team vs Team" }
    };

    private void Awake()
    {
        Singleton = this;

        SignIn();
    }
    public async void SignIn()
    {
        ConnectionText.text = "Connecting";
        ConnectionText.Animate = true;

        try
        {
            await UnityServices.InitializeAsync();

            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        catch
        {
            multiplayerButtonBlocker.SetActive(true);
            ConnectionText.text = "Offline";
            ConnectionText.Animate = false;
            multiplayerButtonBlocker.transform.GetChild(1).gameObject.SetActive(true); //Display retry button

            return;
        }

        multiplayerButtonBlocker.SetActive(false);

        signedIn = true;
    }

    public void LobbySelectionMenuActivated()
    {
        if (isHost) TeamsEditor.Singleton.onTeamAppearencesUpdated.AddListener(HostChangedLobbyTeams);
        TeamsEditor.Singleton.EditorInteractable(isHost);

        if (signedIn)
        {
            //Fetch available lobbies
            RefreshOpenLobbies();
        }
    }
    public void LobbySelectionMenuDeactivated()
    {
        TeamsEditor.Singleton.onTeamAppearencesUpdated.RemoveListener(HostChangedLobbyTeams);
        TeamsEditor.Singleton.EditorInteractable(true);

        multiplayerButtonBlocker.SetActive(false);
    }

    private void Update()
    {
        if(lobbyRefreshTimer > 0)
        {
            lobbyRefreshTimer -= Time.deltaTime;
            if (lobbyRefreshTimer <= 0) refreshButtonBlocker.SetActive(false); //Allow user to refresh lobbies again
        }
    }


    //Pre:
    //0= any game mode
    //1= free for all
    //2= teams
    //Post:
    //0= do nothing
    //1= join successful
    //2= no available lobbies so create one
    //3= error
    public async Task<byte> QuickJoinLobby(int gameModeFilter)
    {
        if (isJoining) return 0;

        isJoining = true;

        foreach (GameObject o in hostUI) o.SetActive(false);

        string selectionGameMode = "";
        if (gameModeFilter == 1) selectionGameMode = "Brawl";
        else if (gameModeFilter == 1) selectionGameMode = "Teams";

        QuickJoinLobbyOptions options = new QuickJoinLobbyOptions
        {
            Player = CreateNewPlayer(),
        };
        if(selectionGameMode != "")
        {
            options.Filter = new List<QueryFilter>()
            {
                new QueryFilter(
                    field: QueryFilter.FieldOptions.S1,
                    op: QueryFilter.OpOptions.EQ,
                    value: selectionGameMode)
            };
        }

        try
        {
            _myLobby = await LobbyService.Instance.QuickJoinLobbyAsync(options);
        }
        catch (LobbyServiceException e)
        {
            isJoining = false;
            if (e.Reason != LobbyExceptionReason.NoOpenLobbies)
            {
                Debug.LogWarning(e);
                return 3;
            }

            return 2;
        }
        //No exception so join successful

        //Show lobby menu
        partyModeMenu.SetActive(true);
        startMenu.SetActive(false);
        //Hide lobby selection
        lobbyEntriesParent.parent.gameObject.SetActive(false);

        JoinLobbyAsClient();

        isJoining = false;

        return 1;
    }

    public async void CreateLobby(string name, bool isPrivate, int gamemode, int maxPlayers, bool joinAsHost)
    {
        connectingText.SetActive(true);

        foreach (GameObject o in hostUI) o.SetActive(true);

        CreateLobbyOptions createLobbyOptions = new CreateLobbyOptions
        {
            IsPrivate = isPrivate,
            Data = new Dictionary<string, DataObject>()
            {
                {
                    "Teams", new DataObject(
                        visibility: DataObject.VisibilityOptions.Member,
                        value: "Team 1;0;1;0.03137255;False;Team 2;1;0;0;False;Team 3;0;0.9803922;1;False;Team 4;0.5058824;0;1;False;Team 5;1;0.6235294;0;False;")
                },
                {
                    "Gamemode", new DataObject(
                        visibility: DataObject.VisibilityOptions.Public,
                        value: gamemode.ToString(),
                        index: DataObject.IndexOptions.S1) //Indexed string so can filter
                },
                {
                    "HostPlayerIndex", new DataObject(
                        visibility: DataObject.VisibilityOptions.Member,
                        value: joinAsHost ? "0" : "-1")
                },
                {
                    "JoinCode", new DataObject(
                        visibility: DataObject.VisibilityOptions.Member,
                        value: await PPTRelay.Singleton.StartHostWithRelay(maxPlayers)) //Tell PPTRelay to allocate a server and return the join code
                },
                {
                    "MaxPlayers", new DataObject(DataObject.VisibilityOptions.Member, maxPlayers.ToString())
                }
            }
        };
        if (joinAsHost)
        {
            _playerNum = 1;
            createLobbyOptions.Player = CreateNewPlayer();
        }

        Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(name, maxPlayers, createLobbyOptions);
        _myLobby = lobby;


        StartCoroutine(HeartbeatLobbyCoroutine(15));

        print("Created lobby; name: " + lobby.Name + " id: " + lobby.Id);

        //Listen for player updates
        ProfileSave.onProfileUpdated.AddListener(SendProfileUpdate);
        isHost = true;
        lobbyJoinCodeText.text = "Join with Code: " + _myLobby.LobbyCode;
        header.text = "Waiting for players to join...";

        lobbySelection.SetActive(false);

        SubscribeToLobbyCallbacks();

        if (joinAsHost) GameSetup.Singleton.AddLobbyPlayers(_myLobby.Players, 0);
        TeamsEditor.Singleton.EditorInteractable(true);
        TeamsEditor.Singleton.onTeamAppearencesUpdated.AddListener(HostChangedLobbyTeams);

        connectingText.SetActive(false);
    }

    //Called when new player wants to join lobby
    Player CreateNewPlayer()
    {
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                {"Name", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, ProfileSave.username) },
                {"Appearence", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, ProfileSave.appearence.Serialize) },
                {"Dir", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, GameSetup.PlayerFacesRight(_playerNum) ? "True" : "False") },
                {"Team", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "0") } //Need to look at lobby's player data and gamemode to figure out what team to be
            }
        };
    }
    async void SendProfileUpdate()
    {
        //Update local entry
        GameSetup.Singleton.UpdateLobbyPlayerName(_playerNum - 1, ProfileSave.username);
        GameSetup.Singleton.UpdateLobbyPlayerAppearence(_playerNum - 1, ProfileSave.appearence);

        var update = new UpdatePlayerOptions
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                {"Name", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, ProfileSave.username) },
                {"Appearence", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, ProfileSave.appearence.Serialize) }
            }
        };

        Lobby lobby = await LobbyService.Instance.UpdatePlayerAsync(_myLobby.Id, PlayerId, update);
        _myLobby = lobby;
    }
    public async void UpdatePlayerTeam(int team)
    {
        var update = new UpdatePlayerOptions
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                {"Team", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, team.ToString()) }
            }
        };

        Lobby lobby = await LobbyService.Instance.UpdatePlayerAsync(_myLobby.Id, PlayerId, update);
        _myLobby = lobby;
    }


    IEnumerator HeartbeatLobbyCoroutine(float waitTimeSeconds)
    {
        var delay = new WaitForSecondsRealtime(waitTimeSeconds);

        while (true)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(_myLobby.Id);
            yield return delay;
        }
    }

    void OnApplicationQuit()
    {
        if(_myLobby != null) LobbyService.Instance.DeleteLobbyAsync(_myLobby.Id);
    }


    public void RefreshLobbyListButton()
    {
        if(lobbyRefreshTimer <= 0)
        {
            lobbyRefreshTimer = 1;
            refreshButtonBlocker.SetActive(true);
            RefreshOpenLobbies();
        }
    }

    async void RefreshOpenLobbies()
    {
        if(isRefreshing) return;

        isRefreshing = true;

        //Destroy previous entries
        for (int i = 0; i < lobbyEntriesParent.childCount; i++) Destroy(lobbyEntriesParent.GetChild(i).gameObject);

        print("Searching for public lobbies...");

        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions();
            options.Count = 25;

            // Filter for open lobbies only
            options.Filters = new List<QueryFilter>()
            {
                new QueryFilter(
                    field: QueryFilter.FieldOptions.AvailableSlots,
                    op: QueryFilter.OpOptions.GT,
                    value: "0")
            };

            // Order by newest lobbies first
            options.Order = new List<QueryOrder>()
            {
            new QueryOrder(
                asc: false,
                field: QueryOrder.FieldOptions.Created)
            };

            QueryResponse lobbies = await LobbyService.Instance.QueryLobbiesAsync(options);

            //Let the user know if no lobbies were found
            noLobbiesFoundText.SetActive(lobbies.Results.Count == 0);

            //Add lobby field for each lobby found
            foreach (Lobby l in lobbies.Results)
            {
                print("Found lobby; name: " + l.Name + " id: " + l.Id);

                LobbyEntry entry = Instantiate(lobbyField, lobbyEntriesParent).GetComponent<LobbyEntry>();
                entry.Set(l.Name, gamemodes[l.Data["Gamemode"].Value], l.Players.Count, l.MaxPlayers, l.Id);
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning(e);
        }

        isRefreshing = false;
    }

    public async void JoinLobbyByID(string Id)
    {
        if (isJoining) return;

        isJoining = true;

        connectingText.SetActive(true);

        foreach (GameObject o in hostUI) o.SetActive(false);

        JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
        {
            Player = CreateNewPlayer()
        };

        try
        {
            _myLobby = await LobbyService.Instance.JoinLobbyByIdAsync(Id, options);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning(e);
            isJoining = false;
            return;
        }

        JoinLobbyAsClient();

        isJoining = false;
    }
    public async Task<bool> JoinLobbyByCode(string code)
    {
        if (isJoining) return false;

        isJoining = true;

        connectingText.SetActive(true);

        foreach (GameObject o in hostUI) o.SetActive(false);

        JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions
        {
            Player = CreateNewPlayer()
        };

        try
        {
            _myLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code, options);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning(e);
            isJoining = false;
            return false;
        }

        JoinLobbyAsClient();

        isJoining = false;

        return true;
    }
    async void JoinLobbyAsClient()
    {
        print("Successfully joined lobby " + _myLobby.Name);

        isHost = false;
        lobbyJoinCodeText.text = "Join with Code: " + _myLobby.LobbyCode;
        header.text = "Waiting for host to start the game...";
        lobbySelection.SetActive(false);

        _playerNum = HostIsNotPlaying ? _myLobby.Players.Count - 1 : _myLobby.Players.Count;

        SubscribeToLobbyCallbacks();
        //Listen for player updates
        ProfileSave.onProfileUpdated.AddListener(SendProfileUpdate);

        //Integrate lobby players into client UI
        GameSetup.Singleton.AddLobbyPlayers(_myLobby.Players, _playerNum - 1);
        TeamsEditor.Singleton.EditorInteractable(false);

        int team = GameSetup.Singleton.NextDefTeam(offset:-1);
        string relayJoinCode = _myLobby.Data["JoinCode"].Value;

        //Set player's team and allocation id
        var updatePlayer = new UpdatePlayerOptions
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                {"Team", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, team.ToString()) }
            },
            AllocationId = relayJoinCode
        };

        _myLobby = await LobbyService.Instance.UpdatePlayerAsync(_myLobby.Id, PlayerId, updatePlayer);

        //Update player entry's team UI
        GameSetup.Singleton.UpdateLobbyPlayerTeam(_playerNum-1, team);

        ClientJoinRelayServer(relayJoinCode);

        connectingText.SetActive(false);
    }

    IEnumerator RestartNetworkManager(Action doAfter)
    {
        NetworkManager.Singleton.Shutdown();

        yield return new WaitUntil(delegate { return !NetworkManager.Singleton.ShutdownInProgress; });

        doAfter();
    }
    async void ClientJoinRelayServer(string relayJoinCode)
    {
        // Connect to relay server
        await PPTRelay.Singleton.StartClientWithRelay(relayJoinCode);
            
    }

    public async void LeaveLobby(bool lobbyGone = false)
    {
        if(isLeaving) return;

        isLeaving = true;

        ProfileSave.onProfileUpdated.RemoveListener(SendProfileUpdate);

        //Reset UI
        GameSetup.Singleton.RemoveAllLobbyPlayers();

        //Go back to start menu
        lobbySelection.SetActive(true);
        lobbySelection.transform.parent.gameObject.SetActive(false);
        //print("a=name: " + lobbySelection.transform.parent.name);

        try
        {
            if (!lobbyGone && isHost && _myLobby.Players.Count > 1)
            {
                //Set new host
                UpdateLobbyOptions updateLobbyOptions = new UpdateLobbyOptions
                {
                    HostId = _myLobby.Players[1].Id
                };

                await LobbyService.Instance.UpdateLobbyAsync(_myLobby.Id, updateLobbyOptions);
            }

            await LobbyService.Instance.RemovePlayerAsync(_myLobby.Id, PlayerId);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning(e);
        }

        _myLobby = null;
        _playerNum = -1;
        StartCoroutine(
        PPTRelay.Singleton.Disconnect());
    }

    public async void SubscribeToLobbyCallbacks()
    {
        var callbacks = new LobbyEventCallbacks();
        callbacks.LobbyChanged += OnLobbyChanged;
        callbacks.LobbyChanged += OnLobbyChanged;
        callbacks.PlayerJoined += PlayerJoined;
        callbacks.PlayerLeft += PlayerLeft;
        callbacks.PlayerDataChanged += PlayerDataUpdated;
        callbacks.KickedFromLobby += OnKickedFromLobby;

        try
        {
            _lobbyEvents = await LobbyService.Instance.SubscribeToLobbyEventsAsync(_myLobby.Id, callbacks);
            Debug.Log("Successfully subscribed to lobby "+_myLobby.Name);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }


    }
    public async void RemoveHostPlayerEntry()
    {
        _playerNum = -1;

        var updateLobbyOptions = new UpdateLobbyOptions
        {
            Data = new Dictionary<string, DataObject>
            {
                {"HostPlayerIndex", new DataObject(DataObject.VisibilityOptions.Member, "-1") }
            }
        };

        try
        {
            _myLobby = await LobbyService.Instance.UpdateLobbyAsync(_myLobby.Id, updateLobbyOptions);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }

    //-----Callbacks
    public async void KickPlayer(int i) //i is the index of the player entry
    {
        try
        {
            if (HostIsNotPlaying) i++; //Host isn't playing so they don't have a player entry

            string playerId = _myLobby.Players[i].Id;
            await LobbyService.Instance.RemovePlayerAsync(_myLobby.Id, playerId);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning(e);
        }
    }

    void OnLobbyChanged(ILobbyChanges changes)
    {
        if(_myLobby == null) return;
        string prevJoinCode = _myLobby.Data["JoinCode"].Value;

        if (changes.LobbyDeleted)
        {
            LeaveLobby();
        }
        else
        {
            changes.ApplyToLobby(_myLobby);
        }

        if (changes.Data.Changed)
        {
            if (!isHost)
            {
                if(changes.Data.Value.TryGetValue("JoinCode", out ChangedOrRemovedLobbyValue<DataObject> joinCodeObj) && joinCodeObj.Changed && joinCodeObj.Value.Value != prevJoinCode)
                {
                    print("Host changed! Joining new server..."+joinCodeObj.Value.Value);

                    //Host migrated to someone else so disconnect and reconnect to their server
                    StartCoroutine(RestartNetworkManager(delegate { ClientJoinRelayServer(joinCodeObj.Value.Value); }));
                }
                if (changes.Data.Value.TryGetValue("Teams", out _))
                {
                    print("Host changed the teams");
                    TeamsEditor.Singleton.LoadTeams(_myLobby.Data["Teams"].Value);
                }
                if (changes.Data.Value.TryGetValue("HostPlayerIndex", out _))
                {
                    if (HostIsNotPlaying)
                    {
                        print("Host doesn't want to play anymore");
                        GameSetup.Singleton.RemoveHostPlayerEntry();
                    }
                }
            }
        }
    }
    void PlayerJoined(List<LobbyPlayerJoined> lobbyPlayers)
    {
        List<Player> players = new List<Player>();
        foreach (LobbyPlayerJoined playerJoined in lobbyPlayers)
        {
            print("Player " + playerJoined.Player.Data["Name"].Value + " joined");
            players.Add(playerJoined.Player);
        }

        GameSetup.Singleton.AddLobbyPlayers(players, _playerNum - 1, clientsAreJoining:true);
    }
    void PlayerLeft(List<int> playerIndices)
    {
        bool hostLeft = false;

        foreach (int i in playerIndices)
        {
            int pEntryIndex = HostIsNotPlaying ? i - 1 : i;

            if (i != 0) print("Player " + GameSetup.Singleton.GetLobbyPlayerEntry(pEntryIndex).tankSettings.name + " left");
            else
            {
                print("Host left");
                hostLeft = true;
            }

            //PEntryIndex will be -1 if the host left but they weren't playing
            if(pEntryIndex >= 0) GameSetup.Singleton.RemoveLobbyPlayer(pEntryIndex);

            if (i < _playerNum - 1) _playerNum--;
        }

        //Check if I'm the new host
        if (_myLobby.HostId == PlayerId && hostLeft) MigrateHostToMe();
    }
    void MigrateHostToMe()
    {
        print("I'm the new host!");
        isHost = true;

        //Update UI
        foreach (GameObject o in hostUI) o.SetActive(true);
        GameSetup.Singleton.HostMigratedToMe(_playerNum - 1);

        int maxPlayers = int.Parse(_myLobby.Data["MaxPlayers"].Value);

        //Allocate new server since Relay can't currently switch hosts
        StartCoroutine(RestartNetworkManager(async delegate
        {
            UpdateLobbyOptions updateLobbyOptions = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>()
            {
                {
                    "JoinCode", new DataObject(
                        visibility: DataObject.VisibilityOptions.Member,
                        value: await PPTRelay.Singleton.StartHostWithRelay(maxPlayers)) //Tell PPTRelay to allocate a server and return the join code
                }
            }
            };

            //Tell lobby members the join code has changed
            await LobbyService.Instance.UpdateLobbyAsync(_myLobby.Id, updateLobbyOptions);
        }));
    }
    void PlayerDataUpdated(Dictionary<int, Dictionary<string, ChangedOrRemovedLobbyValue<PlayerDataObject>>> data)
    {
        if (_myLobby == null) return;

        //print("Player data updated");
        for (int i = 0; i < _myLobby.Players.Count; i++)
        {
            int pIndex = HostIsNotPlaying ? i - 1 : i;

            //print("Pnum: " + _playerNum + " pindex: " + pIndex);
            if (pIndex == _playerNum - 1) continue;

            Dictionary<string, ChangedOrRemovedLobbyValue<PlayerDataObject>> dataChanged;
            if (data.TryGetValue(i, out dataChanged))
            {
                //print("Player " + (i)+" data updated");
                ChangedOrRemovedLobbyValue<PlayerDataObject> pDataValueChanged;
                if(dataChanged.TryGetValue("Name", out pDataValueChanged))
                {
                    if (pDataValueChanged.Changed)
                    {
                        print("Player " + pIndex + " changed their name");
                        GameSetup.Singleton.UpdateLobbyPlayerName(pIndex, pDataValueChanged.Value.Value);
                    }
                }
                if (dataChanged.TryGetValue("Team", out pDataValueChanged))
                {
                    if (pDataValueChanged.Changed)
                    {
                        print("Player " + pIndex + " changed their team");
                        GameSetup.Singleton.UpdateLobbyPlayerTeam(pIndex, int.Parse(pDataValueChanged.Value.Value));
                    }
                }
                if (dataChanged.TryGetValue("Appearence", out pDataValueChanged))
                {
                    if (pDataValueChanged.Changed)
                    {
                        print("Player " + pIndex + " changed their appearence");
                        GameSetup.Singleton.UpdateLobbyPlayerAppearence(pIndex, new TankAppearence(pDataValueChanged.Value.Value));
                    }
                }
            }
        }
    }
    private async void OnKickedFromLobby()
    {
        await _lobbyEvents.UnsubscribeAsync();

        if (isLeaving) //Client triggered the leave so don't notify them that they were kicked
        {
            isLeaving = false;
            return;
        }

        Debug.Log("You were kicked from the lobby!");

        ProfileSave.onProfileUpdated.RemoveListener(SendProfileUpdate);

        kickedPopup.SetActive(true);
        GameSetup.Singleton.RemoveAllLobbyPlayers(); //Reset UI
        StartCoroutine(
                PPTRelay.Singleton.Disconnect());
    }


    async void HostChangedLobbyTeams()
    {
        //print("Lobby teams changed!");

        string teamsStr = "";
        foreach(Team team in TeamsEditor.Singleton.Teams)
        {
            teamsStr += team.name + ";" + team.col.r + ";" + team.col.g + ";" + team.col.b + ";" + team.friendlyFire + ";";
        }

        var updateLobbyOptions = new UpdateLobbyOptions
        {
            Data = new Dictionary<string, DataObject>
            {
                {"Teams", new DataObject(DataObject.VisibilityOptions.Member, teamsStr) }
            }
        };

        try
        {
            _myLobby = await LobbyService.Instance.UpdateLobbyAsync(_myLobby.Id, updateLobbyOptions);
        } catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }
}
