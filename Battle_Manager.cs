/*
 * Battle_Manager.cs
 * 
 * The script responsible for handling the proceedings of battle.
 * 
 */
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Cinemachine;

public class Battle_Manager : MonoBehaviour
{
    public static Battle_Manager instance;

    [Header("Arena Variables")]
    ///<summary>
    ///Length bounds of the arena.
    ///</summary>
    public Vector2Int lengthBounds;
    /// <summary>
    /// Width bounds of the arena.
    /// </summary>
    public Vector2Int widthBounds;
    /// <summary>
    /// Possible spawn points for the players in the arena.
    /// </summary>
    public Vector3[] playerSpawnPoints;
    /// <summary>
    /// Possible spawn points for the enemies in the arena.
    /// </summary>
    public Vector3[] enemySpawnPoints;

    //public List<Battle_Enemy> enemies;
    private List<Battle_Enemy_Manager> enemyInstances;

    [Header("Reference Variables")]
    ///<summary>
    /// Main camera of the scene.
    ///</summary>
    public Camera mainCam;
    /// <summary>
    ///  Overhead camera of the arena.
    /// </summary>
    public CinemachineVirtualCamera overheadCam;
    /// <summary>
    /// Catalog of the attacks in the game.
    /// </summary>
    public Attack_Catalog attacks;
    /// <summary>
    /// List of active characters in the scene.
    /// </summary>
    [HideInInspector] public List<PlayableCharacter> players;
    ///<summary>
    /// List of active player instances
    /// </summary>
    private List<Battle_Player_Manager> playerInstances;
    /// <summary>
    /// Layer mask of the range markers.
    /// </summary>
    public LayerMask rangeMarkerMask;


    [Header("Template Variables")]
    ///<summary> 
    ///Prefab of the range markers.
    ///</summary>
    public GameObject rangeMarker;

    /// <summary>
    /// Prefab of the player.
    /// </summary>
    public GameObject playerPrefab;

    ///<summary>
    /// Prefab of the enemy.
    /// </summary>
    public GameObject enemyPrefab;

    /// <summary>
    /// Currently active camera.
    /// </summary>
    [HideInInspector] public CinemachineVirtualCamera liveCam;

    /// <summary>
    /// Plane used for properly raycasting onto an isographic map.
    /// </summary>
    private Plane m_plane;
    
    /// <summary>
    /// Coroutine currently running for selecting a range marker.
    /// </summary>
    private Coroutine spaceSelection;

    /// <summary>
    /// Coroutine currently running for updating the attack's direction.
    /// </summary>
    private Coroutine directionSelection;

    public enum ActingParty {Player, Enemy, NPC, Other};
    [HideInInspector] public ActingParty activeTurn;
    
    /// <summary>
    /// Checks if the player is currently using a tactic.
    /// </summary>
    private bool playerActing;
    /// <summary>
    /// The current attack minigame.
    /// </summary>
    private iAttack curAttack;
    /// <summary>
    /// The currently selected player.
    /// </summary>
    private Battle_Player_Manager selectedPlayer;
    /// <summary>
    /// The currently selected weapon.
    /// </summary>
    private Weapon selectedWeapon;

    [HideInInspector] public Battle_Enemy actingEnemy;

    [Header("Test Variables")]
    public EnemySquad testSquad;
    public PlayableCharacter[] testCharacters;
    
    #region Unity Callbacks
    private void Awake()
    {
        instance = this;
        playerActing = false;
        m_plane = new Plane(mainCam.gameObject.transform.forward, 2);
        liveCam = overheadCam;

        playerInstances = new List<Battle_Player_Manager>();
        enemyInstances = new List<Battle_Enemy_Manager>();

        activeTurn = ActingParty.Player;

        //for (int i = 0; i != players.Length; i++)
        //{
        //    Battle_Player_Manager tempPlayer = 
        //        Instantiate(playerPrefab, playerSpawnPoints[i], Quaternion.identity)
        //            .GetComponent<Battle_Player_Manager>();

        //    tempPlayer.character = players[i];
        //    tempPlayer.name = tempPlayer.character.name;
        //}

        //for (int i = 0; i != enemies.Capacity; i++)
        //{
        //    Battle_Enemy_Manager tempEnemy =
        //        Instantiate(enemyPrefab, enemySpawnPoints[i], Quaternion.identity)
        //            .GetComponent<Battle_Enemy_Manager>();

        //    tempEnemy.enemy = enemies[i];
        //    tempEnemy.name = tempEnemy.enemy.name;

        //    enemyInstances.Add(tempEnemy);
        //}

        //StartCoroutine(PlayerTurn());
    }

    private void Update()
    {
        if (Input.GetMouseButton(1))
        {
            DeselectPlayer();
        }

        if (Input.GetKey(KeyCode.LeftShift))
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                if (Overworld_Manager.instance != null) Overworld_Manager.instance.EndBattle();
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                StartBattle(testCharacters, testSquad);
            }
        }
    }
    #endregion

    #region Coroutines
    public IEnumerator PlayerTurn()
    {
        bool turnOver = false;
        activeTurn = ActingParty.Player;
        if (Battle_UI.instance != null)
        {
            Battle_UI.instance.ToggleUI();
            Battle_UI.instance.UpdateTurn("Player Turn");
        }

        yield return new WaitForSeconds(1f);
        foreach (PlayableCharacter pc in players)
        {
            pc.RefreshTactics();
            pc.RefreshActions();
        }

        foreach(Battle_Player_Manager bpm in playerInstances)
        {
            if (bpm.guarding) bpm.ResetAnimation();
        }

        while (activeTurn == ActingParty.Player)
        {
            turnOver = true;
            foreach(PlayableCharacter pc in players)
            {
                if (pc.curActions > 0)
                {
                    turnOver = false;
                    break;
                }
            }

            if (turnOver) break;
            yield return new WaitForEndOfFrame();
        }
        
        StartCoroutine(EnemyTurn());
    }

    public IEnumerator EnemyTurn()
    {
        activeTurn = ActingParty.Enemy;
        Battle_UI.instance.UpdateTurn("Enemy Turn");
        Battle_UI.instance.ToggleUI();
        yield return new WaitForSeconds(1f);

        foreach (Battle_Enemy_Manager enemy in enemyInstances)
            {
                actingEnemy = enemy.enemy;
                enemy.TakeTurn();
                Debug.Log(enemy.name + " is now acting.");
                while (!enemy.acted)
                {
                    yield return new WaitForEndOfFrame();
                }
            }

        yield return new WaitForEndOfFrame();
        StartCoroutine(PlayerTurn());
    }

    public IEnumerator SpaceSelection()
    {
        Vector3 mousePos = Input.mousePosition;
        Vector3 hitPoint = Vector3.negativeInfinity;

        if (Battle_UI.instance != null)
        {
            if (selectedWeapon != null)
                Battle_UI.instance.FadeDescription(0.5f);
        }


        while (true)
        {
            mousePos = Input.mousePosition;

            Ray ray = mainCam.ScreenPointToRay(mousePos);

            if (m_plane.Raycast(ray, out float enter))
            {
                hitPoint = ray.GetPoint(enter);
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (hitPoint != Vector3.negativeInfinity)
                {
                    if (Physics.Raycast(new Vector3(hitPoint.x, 3, (hitPoint.y + hitPoint.z)), Vector3.down, out RaycastHit hit, 100, rangeMarkerMask))
                    {
                        if (hit.collider.GetComponent<Battle_RangeMarker>() != null)
                        {
                            SelectSpace(hit.collider.GetComponent<Battle_RangeMarker>());
                            spaceSelection = null;
                            directionSelection = null;
                            yield break;
                        }
                    }
                }
            }
            
            yield return null;
        }
    }

    public IEnumerator SetDirection()
    {
        Vector3 mousePos = Input.mousePosition;
        Vector3 hitPoint = Vector3.negativeInfinity;
        int newX;
        int newY;

        while (playerActing && curAttack == null)
        {
            mousePos = Input.mousePosition;

            Ray ray = mainCam.ScreenPointToRay(mousePos);

            if (m_plane.Raycast(ray, out float enter))
            {
                hitPoint = ray.GetPoint(enter);
            }

            if (hitPoint != Vector3.negativeInfinity)
            {
                newX = (int)Mathf.Sign(hitPoint.x - selectedPlayer.transform.position.x);
                newY = (int)Mathf.Sign((hitPoint.y + hitPoint.z) - selectedPlayer.transform.position.z);

                if (Mathf.Abs(hitPoint.x - selectedPlayer.transform.position.x) >
                    Mathf.Abs((hitPoint.y + hitPoint.z) - selectedPlayer.transform.position.z))
                {
                    newY = 0;
                }
                else
                {
                    newX = 0;
                }

                if (Battle_RangeMarker.direction.x != newX || Battle_RangeMarker.direction.y != newY)
                {
                    Battle_RangeMarker.direction.x = newX;
                    Battle_RangeMarker.direction.y = newY;
                    UpdateAttack();
                    directionSelection = null;
                    yield break;
                }

                yield return new WaitForEndOfFrame();
            }
        }
    }
    #endregion

    #region Transitions Functions
    public void StartBattle(PlayableCharacter[] activeParty, EnemySquad squad)
    {
        for (int i = 0; i != activeParty.Length; i++)
        {
            Battle_Player_Manager tempPlayer =
                Instantiate(playerPrefab, playerSpawnPoints[i], Quaternion.identity)
                    .GetComponent<Battle_Player_Manager>();

            tempPlayer.character = activeParty[i];
            tempPlayer.name = tempPlayer.character.name;

            playerInstances.Add(tempPlayer);
            players.Add(tempPlayer.character);
        }

        int squadCount = 0;
        for (int i = 0; i != squad.enemyGroups.Length; i++)
        {
            for (int j = 0; j != squad.enemyGroups[i].count; j++)
            {
                Battle_Enemy_Manager tempEnemy = Instantiate(enemyPrefab, enemySpawnPoints[squadCount], Quaternion.identity)
                .GetComponent<Battle_Enemy_Manager>();

                tempEnemy.enemy = squad.enemyGroups[i].enemy;
                tempEnemy.name = tempEnemy.enemy.name;

                squadCount++;
                enemyInstances.Add(tempEnemy);

                if (squadCount+1 > enemySpawnPoints.Length) break;
            }

            if (squadCount + 1 > enemySpawnPoints.Length) break;
        }

        StartCoroutine(PlayerTurn());
    }
    #endregion

    #region Attack Functions
    /// <summary>
    /// Stops the attack and hits enemies if the attack was successful.
    /// </summary>
    /// <param name="success"></param>
    public void EndAttack(bool attackSucceeded)
    {
        Battle_UI.instance.StopAttack();

        if (attackSucceeded)
        {
            foreach (Battle_Character target in Battle_RangeMarker.targets)
            {
                target.GetHit(selectedWeapon.damage);
            }
        }

        Battle_RangeMarker.targets.Clear();

        selectedPlayer.Action();
        selectedPlayer.character.curActions--;
        selectedWeapon.used = true;

        selectedWeapon = null;
        DeselectPlayer();
    }

    /// <summary>
    /// Starts the process of attack with a weapon. Does NOT start the attack.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="weapon"></param>
    public void StartAttack(Battle_Player_Manager player, Weapon weapon)
    {
        playerActing = true;
        player.playerActing = true;
        selectedWeapon = weapon;
        Battle_RangeMarker.Clear();

        //Set Direction
        if (directionSelection != null) StopCoroutine(directionSelection);
        if (spaceSelection != null) StopCoroutine(spaceSelection);

        directionSelection = StartCoroutine(SetDirection());
        spaceSelection = StartCoroutine(SpaceSelection());

        if (selectedWeapon.specialTactic == true)
        {
            if (selectedWeapon.name.Equals("Move"))
            {
                StartMove(player);
                directionSelection = null;
                spaceSelection = null;
            }
            if (selectedWeapon.name.Equals("Defend"))
            {
                SelectSpace(null);
            }
        }
        else
        {
            directionSelection = StartCoroutine(SetDirection());
            spaceSelection = StartCoroutine(SpaceSelection());

            switch (weapon.weaponType)
            {
                case Weapon.WeaponType.Line:
                    {
                        Battle_RangeMarker.DrawLine(player.transform, weapon.range);
                        break;
                    }

                case Weapon.WeaponType.AOE:
                    {
                        Battle_RangeMarker.DrawAoe(player.transform, weapon.range);
                        break;
                    }

                case Weapon.WeaponType.Wall:
                    {
                        Battle_RangeMarker.DrawWall(player.transform, weapon.range, weapon.thickness);
                        break;
                    }
            }
        }
    }

    /// <summary>
    /// Updates attack based on changed direction.
    /// </summary>
    public void UpdateAttack()
    {
        Battle_RangeMarker.KillAll();
        Battle_RangeMarker.Clear();

        StartAttack(selectedPlayer, selectedWeapon);
    }

    /// <summary>
    /// Spawns the selected attack prefab and starts it after a brief delay.
    /// </summary>
    public void Attack()
    {
        Battle_RangeMarker.KillAll();
        if (directionSelection != null) StopCoroutine(directionSelection);
        if (spaceSelection != null) StopCoroutine(spaceSelection);

        if (Battle_UI.instance != null)
        {
            Battle_UI.instance.FadeDescription(0);
        }

        if (curAttack == null)
        {
            curAttack = Battle_UI.instance.SpawnAttack(FindAttackPrefab(selectedWeapon),
                selectedPlayer.gameObject).GetComponent<iAttack>();

            if (selectedWeapon.specialTactic == false) selectedPlayer.ChooseTactic();

            Invoke("TriggerAttack", 2f); //delay so attack doesn't immediately start
        }
    }

    /// <summary>
    /// Begins the attack minigame.
    /// </summary>
    public void TriggerAttack()
    {
        curAttack.StartAttack();
        curAttack = null;
    }

    /// <summary>
    /// Selects a weapon and pushes its data to the UI panel.
    /// </summary>
    /// <param name="weapon"></param>
    public void PreviewWeapon(Weapon weapon)
    {
        selectedWeapon = weapon;
        if (Battle_UI.instance != null)
        {
            if (selectedWeapon != null)
            {
                Battle_UI.instance.FadeDescription(1);
                Battle_UI.instance.UpdateDescription(weapon);
            }
            else
            {
                Battle_UI.instance.FadeDescription(0);
            }
        }
    }

    /// <summary>
    /// Checks the catalog for an attack using the given weapon's type.
    /// </summary>
    /// <param name="weapon"></param>
    /// <returns></returns>
    public GameObject FindAttackPrefab(Weapon weapon)
    {
        foreach (Attack attack in attacks.attacks)
        {
            if (attack.attack == selectedWeapon.attackType)
            {
                if (attack.prefab == null) Debug.LogWarning("No prefab exists for attack type " + attack.attack.ToString());
                else
                    return attack.prefab;
            }
        }
        return null;
    }
    #endregion

    #region Selection Functions
    /// <summary>
    /// Selects a player. Updating camera and selected player.
    /// </summary>
    /// <param name="newPlayer"></param>
    public void SelectPlayer(Battle_Player_Manager newPlayer)
    {
        if (activeTurn != ActingParty.Player)
        { return; }

        //if (newPlayer == selectedPlayer)
        //{
        //    DeselectPlayer();
        //    return;
        //}

        if (playerActing) return;
        if (newPlayer.playerActing) return;
        DeselectPlayer();
        selectedPlayer = newPlayer;
        selectedPlayer.Select();

        liveCam.Priority--;
        newPlayer.focusCam.Priority++;
        liveCam = newPlayer.focusCam;
    }
    
    /// <summary>
    /// Deselects a player. Destroys range markers, updates camera, and selected player.
    /// </summary>
    public void DeselectPlayer()
    {
        if (selectedPlayer == null) return;
        selectedPlayer.ResetAnimation();
        selectedPlayer.Deselect();
        selectedPlayer = null;
        playerActing = false;
        if (Battle_UI.instance != null) Battle_UI.instance.FadeDescription(0);
        Battle_RangeMarker.KillAll();


        liveCam.Priority--;
        overheadCam.Priority++;
        liveCam = overheadCam;

        StopCoroutine(SetDirection());
    }

    /// <summary>
    /// Selects a battle range marker. Changes based on selected weapon.
    /// </summary>
    /// <param name="newPosition"></param>
    public void SelectSpace(Battle_RangeMarker newPosition)
    {
        if (directionSelection != null) StopCoroutine(directionSelection);
        if (spaceSelection != null) StopCoroutine(spaceSelection);

        if (Battle_UI.instance != null)
        {
            Battle_UI.instance.FadeDescription(0);
        }

        if (selectedWeapon.specialTactic == true)
        {
            if (selectedPlayer != null) //TODO: Add other special tactic capabilities
            {
                if (selectedWeapon.name.Equals("Move"))
                    if (newPosition.myTarget == null)
                    {
                        selectedPlayer.MoveTo(newPosition);
                        selectedPlayer.character.curActions--;
                        DeselectPlayer();
                    }
                if (selectedWeapon.name.Equals("Defend"))
                {
                    selectedPlayer.character.curActions = 0;
                    DeselectPlayer();
                }
            }
        }
        else
        {
            Attack();
        }
    }

    /// <summary>
    /// Spawns movement tactic. TEMP.
    /// </summary>
    /// <param name="player"></param>
    public void StartMove(Battle_Player_Manager player)
    {
        Battle_RangeMarker.DrawAoe(player.transform, player.character.speed);
        StartCoroutine(SpaceSelection());
    }
    #endregion

    public void UpdatePlayer(PlayableCharacter character)
    {
        Battle_UI.instance.UpdatePlayer(character);
    }
}
