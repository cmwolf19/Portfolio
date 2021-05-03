/* Battle_Manager.cs
 * The script responsible for handling the proceedings of battle.
 * Connor Wolf
 */
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Cinemachine;
using FMODUnity;
using UnityEngine.SceneManagement;

public class Battle_Manager : MonoBehaviour
{
    //Singleton
    public static Battle_Manager instance;
    public static bool GoToEnd;

    [Header("Arena Variables")]
    ///<summary>
    /// Length bounds of the arena.
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
    /// <summary>
    /// List of all active enemy instances in the battle.
    /// </summary>
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
    [HideInInspector] public List<Battle_Player_Manager> playerInstances;
    /// <summary>
    /// Layer mask of the range markers.
    /// </summary>
    public LayerMask rangeMarkerMask;
    /// <summary>
    /// Mask of all actor types on stage
    /// </summary>
    public LayerMask actorMask;
    /// <summary>
    /// Layer mask of the play field
    /// </summary>
    public LayerMask playMask;

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
    ///<summary>
    /// Star Hit Particle.
    /// </summary>
    public GameObject starParticle;
    /// <summary>
    /// Currently active camera.
    /// </summary>
    [HideInInspector] public CinemachineVirtualCamera liveCam;
    /// <summary>
    /// Plane used for properly raycasting onto an isographic map.
    /// </summary>
    private Plane m_isoPlane;
    
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
    [HideInInspector] public bool lockBattle;
    
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
    [HideInInspector] public Battle_Player_Manager selectedPlayer;
    /// <summary>
    /// The currently selected weapon.
    /// </summary>
    private Weapon selectedWeapon;
    private int turnTracker;
    public enum AttackDirection { Up, Down, Left, Right};
    private AttackDirection currentDirection;

    /// <summary>
    /// Enemy squad that is play. FOR DROPS.
    /// </summary>
    private EnemySquad curSquad;
    [HideInInspector] public Battle_Enemy actingEnemy;

    [Header("FMOD Events")]
    [EventRef] public string confirmSound;
    [EventRef] public string cancelSound;

    [Header("Test Variables")]
    public bool enableTest;
    public EnemySquad testSquad;
    public PlayableCharacter[] testCharacters;

    #region Unity Callbacks
    private void Awake()
    {
        instance = this;
        turnTracker = 0;
        //Create a plane directly in front of the camera for raycasting.
        m_isoPlane = new Plane(mainCam.gameObject.transform.forward, 2);
        liveCam = overheadCam;

        players = new List<PlayableCharacter>();
        playerInstances = new List<Battle_Player_Manager>();
        enemyInstances = new List<Battle_Enemy_Manager>();

        activeTurn = ActingParty.Player;

        if (Application.isEditor == false) enableTest = false;

        //Runs if the testing variables are active.
        if (enableTest)
        {
            StartBattle(testCharacters, testSquad);
            foreach (PlayableCharacter pc in testCharacters)
            {
                pc.FullHeal();
            }
        }
    }

    private void Update()
    {
        //Quickly deselect player.
        if (Input.GetMouseButton(1) && selectedPlayer != null && !lockBattle)
        {
            DeselectPlayer();

            FMOD.Studio.EventInstance cancelInstance = FMODUnity.RuntimeManager.CreateInstance(cancelSound);
            cancelInstance.start();
        }
    }
    #endregion

    void QuitNow()
    {
        Application.Quit();
    }

    #region Coroutines
    /// <summary>
    /// Player turn coroutine, runs when players are active.
    /// </summary>
    /// <returns></returns>
    public IEnumerator PlayerTurn()
    {
        if (playerInstances.Count == 0)
        {
            Battle_UI.instance.UpdateTurn("Game Over.");
            Invoke("QuitNow", 5);
            yield break;
        }
        bool turnOver = false;
        activeTurn = ActingParty.Player;
        //Refresh Battle_UI
        if (Battle_UI.instance != null && turnTracker != 0)
        {
            Battle_UI.instance.ToggleUI();
            Battle_UI.instance.UpdateTurn("Players Acting...");
        }
        //Refresh Battle Animations
        foreach (Battle_Player_Manager bpm in playerInstances)
        {
            bpm.ResetAnimation();
        }
        //Refresh Player's Tactics and Actions
        foreach (PlayableCharacter pc in players)
        {
            pc.RefreshTactics();
            pc.RefreshActions();
        }
        yield return new WaitForSeconds(1f);
        turnTracker++;

        //Main Player Loop
        while (activeTurn == ActingParty.Player)
        {
            turnOver = true;
            foreach(PlayableCharacter pc in players)
            {
                Battle_UI.instance.UpdatePlayer(pc);

                //If ANY character has actions, keep turn going.
                if (pc.curActions > 0)
                {
                    turnOver = false;
                    continue;
                }
            }
            if (turnOver) break;

            //If no active enemies are left, end the fight.
            if (enemyInstances.Count == 0)
            {
                EndBattle();
                yield break;
            }
            yield return new WaitForEndOfFrame();
        }
        
        StartCoroutine(EnemyTurn());
    }

    /// <summary>
    /// Enemy turn coroutine, runs when enemies are active.
    /// </summary>
    public IEnumerator EnemyTurn()
    {
        activeTurn = ActingParty.Enemy;

        //Refresh Battle UI and hide it.
        Battle_UI.instance.UpdateTurn("Enemies Acting...");
        Battle_UI.instance.ToggleUI();
        yield return new WaitForSeconds(1f);

        //Main Enemy Loop
        foreach (Battle_Enemy_Manager enemy in enemyInstances)
            {
                actingEnemy = enemy.enemy;
                enemy.TakeTurn();
                Debug.Log(enemy.name + " is now acting.");
                while (!enemy.acted) yield return new WaitForSeconds(.5f);
            }

        //If all enemies are dead, end battle.
        if (enemyInstances.Count == 0)
        {
            EndBattle();
            yield break;
        }

        //TODO: GAME OVER.
        //TODO: GAME OVER.
        //TODO: GAME OVER.
        //TODO: GAME OVER.

        yield return new WaitForEndOfFrame();
        StartCoroutine(PlayerTurn());
    }

    /// <summary>
    /// Space selection coroutine, runs when using tactics.
    /// </summary>
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
            //Raycast to isographic plane, use that as target.
            mousePos = Input.mousePosition;

            Ray ray = mainCam.ScreenPointToRay(mousePos);

            if (m_isoPlane.Raycast(ray, out float enter))
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

    /// <summary>
    /// Sets the direction of the player's attack.
    /// </summary>
    /// <returns></returns>
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

            if (m_isoPlane.Raycast(ray, out float enter))
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
                    currentDirection = AttackDirection.Left;
                    if (hitPoint.x > selectedPlayer.transform.position.x) currentDirection = AttackDirection.Right;
                    newY = 0;
                }
                else
                {
                    currentDirection = AttackDirection.Down;
                    if (hitPoint.y > selectedPlayer.transform.position.y) currentDirection = AttackDirection.Up;
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
    /// <summary>
    /// Initiates the battle with the given party and enemies.
    /// </summary>
    public void StartBattle(PlayableCharacter[] activeParty, EnemySquad squad)
    {
        curSquad = squad;
        for (int i = 0; i != activeParty.Length; i++)
        {
            Battle_Player_Manager tempPlayer =
                Instantiate(playerPrefab, playerSpawnPoints[i], Quaternion.identity)
                    .GetComponent<Battle_Player_Manager>();

            tempPlayer.character = activeParty[i];
            tempPlayer.name = tempPlayer.character.name;
            tempPlayer.SetGraphics();

            playerInstances.Add(tempPlayer);
            players.Add(tempPlayer.character);
        }

        int squadCount = 0;
        for (int i = 0; i != squad.enemyGroups.Length; i++)
        {
            for (int j = 0; j != squad.enemyGroups[i].count; j++)
            {
                Battle_Enemy_Manager tempEnemy = null;
                if (!squad.enemyGroups[i].enemy.specialEnemy)
                {
                    tempEnemy = Instantiate(enemyPrefab, enemySpawnPoints[squadCount], Quaternion.identity)
                    .GetComponent<Battle_Enemy_Manager>();
                } else
                {
                    tempEnemy = Instantiate(squad.enemyGroups[i].enemy.specialEnemyPrefab).GetComponent<Battle_Enemy_Manager>();
                }

                tempEnemy.enemy = squad.enemyGroups[i].enemy;
                tempEnemy.name = tempEnemy.enemy.name;
                tempEnemy.maxHealth = squad.enemyGroups[i].enemy.maxHealth;
                tempEnemy.curHealth = tempEnemy.maxHealth;
                tempEnemy.SetGraphics();

                squadCount++;
                enemyInstances.Add(tempEnemy);

                if (squadCount+1 > enemySpawnPoints.Length) break;
            }

            if (squadCount + 1 > enemySpawnPoints.Length) break;
        }

        StartCoroutine(PlayerTurn());
    }

    /// <summary>
    /// Removes the target gameobject from the arena. Destroys enemies.
    /// </summary>
    public void Remove(GameObject target)
    {
        if (target.GetComponent<Battle_Enemy_Manager>() != null)
        {
            Battle_Enemy_Manager selectedEnemy = target.GetComponent<Battle_Enemy_Manager>();
            foreach (Battle_Enemy_Manager enemy in enemyInstances)
            {
                if (enemy.curHealth == selectedEnemy.curHealth)
                {
                    enemyInstances.Remove(enemy);
                    break;
                }
            }
        }
        else
        if (target.GetComponent<Battle_Player_Manager>() != null)
        {
            Battle_Player_Manager selectedPlayer = target.GetComponent<Battle_Player_Manager>();
            foreach (Battle_Player_Manager player in playerInstances)
            {
                if (player.curHealth == selectedPlayer.curHealth)
                {
                    playerInstances.Remove(player);
                    break;
                }
            }
        }
    }

    public void EndBattle()
    {
        Battle_UI.instance.Victory(curSquad);
    }

    public void StartOverworld()
    {
        if (!GoToEnd) Overworld_Manager.instance.EndBattle();
        else SceneManager.LoadSceneAsync("Ending", LoadSceneMode.Single);
    }

    public void EndTurnNow()
    {
        DeselectPlayer();
        if (activeTurn == ActingParty.Player)
        {
            foreach(PlayableCharacter pc in players)
            {
                pc.curActions = 0;
            }
        }
    }
    #endregion

    #region Attack Functions
    /// <summary>
    /// Stops the attack and hits enemies if the attack was successful.
    /// </summary>
    /// <param name="success"></param>
    public void EndAttack(bool attackSucceeded)
    {
        lockBattle = false;
        Battle_UI.instance.ToggleUI();
        Battle_UI.instance.StopAttack();

        if (attackSucceeded)
        {
            foreach (Battle_Character target in Battle_RangeMarker.targets)
            {
                target.GetHit(selectedWeapon.damage);

                Destroy(Instantiate(selectedWeapon.battleEffect, target.transform.position+new Vector3(0,0.25f,0), selectedWeapon.battleEffect.transform.rotation), 0.5f);
                Destroy(Instantiate(starParticle, target.transform.position + new Vector3(0, 0.25f, 0), selectedWeapon.battleEffect.transform.rotation), 0.5f);

                target.Push(selectedWeapon.pushDistance, currentDirection);
                if (!selectedWeapon.multiTarget) break;
            }

            Battle_UI.instance.SpawnAction("NICE!", selectedPlayer.transform.position + Vector3.up*2);

            FMOD.Studio.EventInstance attackInstance = RuntimeManager.CreateInstance(selectedWeapon.usedSound);
            attackInstance.start();
        } else
        {
            Battle_UI.instance.SpawnAction("MISS...", selectedPlayer.transform.position + Vector3.up*2);
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
    public void StartAttack(Battle_Player_Manager player, Weapon weapon)
    {
        if (liveCam == player.focusCam)
        {
            player.focusCam.Priority--;
            overheadCam.Priority++;
            liveCam = overheadCam;
        }

        playerActing = true;
        player.playerActing = true;
        selectedWeapon = weapon;
        player.SetWeapon(selectedWeapon);
        Battle_RangeMarker.Clear();

        //Set Direction
        if (directionSelection != null) StopCoroutine(directionSelection);
        if (spaceSelection != null) StopCoroutine(spaceSelection);

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
        if (weapon.weaponType != Weapon.WeaponType.AOE)
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

                case Weapon.WeaponType.Wall:
                    {
                        Battle_RangeMarker.DrawWall(player.transform, weapon.range, weapon.thickness);
                        break;
                    }
            }
        } else
        {
            StartCoroutine(AoeAttack());
        }
    }

    /// <summary>
    /// Special coroutine for AOE attacks. Runs in place of StartAttack.
    /// </summary>
    public IEnumerator AoeAttack()
    {
        Vector3 mousePos = Input.mousePosition;
        Vector3 hitPoint = Vector3.negativeInfinity;
        Vector3 curAoePosition = Vector3.negativeInfinity;
        spaceSelection = StartCoroutine(SpaceSelection());

        while (playerActing && curAttack == null)
        {
            mousePos = Input.mousePosition;
            Ray ray = mainCam.ScreenPointToRay(mousePos);

            if (m_isoPlane.Raycast(ray, out float enter))
            {
                hitPoint = ray.GetPoint(enter);
            }

            if (hitPoint != Vector3.negativeInfinity)
            {
                if (Physics.Raycast(new Vector3(hitPoint.x, 3, (hitPoint.y + hitPoint.z)), Vector3.down, out RaycastHit hit, 100, playMask))
                {
                    hitPoint = hit.point;
                    hitPoint = Vector3Int.RoundToInt(hitPoint);
                }
            }
            
            curAoePosition = hitPoint;
            Battle_RangeMarker.DrawAoe(new Vector2(hitPoint.x, hitPoint.z), selectedWeapon.range);
            
            while (hitPoint == curAoePosition && hitPoint != null)
            {
                mousePos = Input.mousePosition;

                ray = mainCam.ScreenPointToRay(mousePos);

                if (m_isoPlane.Raycast(ray, out float newEnter))
                {
                    hitPoint = ray.GetPoint(newEnter);
                }

                if (hitPoint != Vector3.negativeInfinity)
                {
                    if (Physics.Raycast(new Vector3(hitPoint.x, 3, (hitPoint.y + hitPoint.z)), Vector3.down, out RaycastHit hit, 100, playMask))
                    {
                        hitPoint = hit.point;
                        hitPoint = Vector3Int.RoundToInt(hitPoint);
                    }
                }

                yield return new WaitForEndOfFrame();
            }
            Battle_RangeMarker.KillAll();
            yield return new WaitForEndOfFrame();
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
        Battle_UI.instance.ToggleUI();

        lockBattle = true;
        selectedPlayer.character.curMana -= selectedWeapon.cost;
        selectedPlayer.weapon.sprite = selectedWeapon.icon;

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
    public void SelectPlayer(Battle_Player_Manager newPlayer)
    {
        if (activeTurn != ActingParty.Player)
        { return; }

        FMOD.Studio.EventInstance selectInstance = RuntimeManager.CreateInstance(confirmSound);
        selectInstance.start();
        
        if (playerActing) return;
        if (newPlayer.playerActing) return;
        DeselectPlayer();
        selectedPlayer = newPlayer;
        selectedPlayer.Select();

        Battle_UI.instance.UpdateTactics(selectedPlayer.character, selectedPlayer);

        if (liveCam == overheadCam)
        {
            overheadCam.Priority--;
            newPlayer.focusCam.Priority++;
            liveCam = newPlayer.focusCam;
        }
    }
    
    /// <summary>
    /// Deselects a player. Destroys range markers, updates camera, and selected player.
    /// </summary>
    public void DeselectPlayer()
    {
        if (selectedPlayer == null) return;
        selectedPlayer.ResetAnimation();
        selectedPlayer.Deselect();
        
        playerActing = false;
        Battle_UI.instance.FadeDescription(0);
        Battle_UI.instance.FadeTactics(0);
        Battle_RangeMarker.KillAll();

        if (liveCam == selectedPlayer.focusCam)
        {
            selectedPlayer.focusCam.Priority--;
            overheadCam.Priority++;
            liveCam = overheadCam;
        }

        selectedPlayer = null;
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
                FMOD.Studio.EventInstance attackInstance = RuntimeManager.CreateInstance(selectedWeapon.usedSound);
                attackInstance.start();

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
        Vector2 playerPos = new Vector2(player.transform.position.x, player.transform.position.z);
        Battle_RangeMarker.DrawAoe(playerPos, player.character.speed);
        StartCoroutine(SpaceSelection());
    }
    #endregion

    public void UpdatePlayer(PlayableCharacter character)
    {
        Battle_UI.instance.UpdatePlayer(character);
    }

    public void AddTactic(Weapon addTactic)
    {
        foreach(Battle_Player_Manager player in playerInstances)
        {
            player.character.AddTactic(addTactic);
        }
    }

    public void RefreshActions()
    {
        foreach(Battle_Player_Manager player in playerInstances)
        {
            player.character.RefreshActions();
        }
    }

    public static GameObject CheckSpace(Vector2 space)
    {
        RaycastHit hit;
        Physics.Raycast(new Vector3(space.x, 3, space.y), Vector3.down, out hit, 3, instance.actorMask);
        if (hit.collider == null) return null;
        return hit.collider.gameObject;
    }
}
