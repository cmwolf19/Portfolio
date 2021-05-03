using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Overworld_Manager : MonoBehaviour
{
    public static Overworld_Manager instance;
    public int battleSceneIndex;
    public PlayableCharacter[] activeParty;
    [HideInInspector] public GameObject player;
    public GameObject overworldItemPrefab;
    public LayerMask terrainMask;
    private Scene overworldScene;
    private EnemySquad curSquad;
    private GameObject overworldCam;
    private GameObject curEnemy;
    [HideInInspector] public bool activeOverworld;
    public Canvas overworldCanvas;
    public GameObject sceneTransition;
    private GameObject curTransition;
    public List<GameObject> overworldObjects;
    public CanvasGroup cinematicBlack;
    public CanvasGroup wideScreen;
    public bool paused;
    public string pauseString;
    private bool canPause;

    public List<Item> consumableItems;

    private Coroutine overlapRoutine;

    private void Awake()
    {
        transform.parent = null;
        
        instance = this;
        StartCoroutine(Fade(0));
        Refresh();
        canPause = true;
    }

    public void Widescreen(bool state)
    {
        bool activeWidescreen = (wideScreen.alpha > 0);
        if (state == activeWidescreen) return;

        if (state == true)
        {
            StopCoroutine(FadeWide(1));
            StopCoroutine(FadeWide(0));
            StartCoroutine(FadeWide(1));
        } else
        {
            StopCoroutine(FadeWide(0));
            StopCoroutine(FadeWide(1));
            StartCoroutine(FadeWide(0));
        }
    }

    private IEnumerator FadeWide(float val)
    {
        while (wideScreen.alpha != val)
        {
            wideScreen.alpha = Mathf.MoveTowards(wideScreen.alpha, val, 0.05f);
            yield return new WaitForEndOfFrame();
        }
    }

    public void Refresh()
    {
        if (overlapRoutine != null) StopCoroutine(overlapRoutine);
        overworldScene = SceneManager.GetActiveScene();
        activeOverworld = true;
        overworldCam = Camera.main.gameObject;
        player = FindObjectOfType<Overworld_Player>().gameObject;
       Overworld_Door.player = player.GetComponent<Overworld_Player>();

        List<Animator> animators3D = new List<Animator>();
        Animator[] tempAnims = GameObject.FindObjectsOfType<Animator>();

        foreach (Animator anim in tempAnims)
        {
            if (anim.gameObject.layer == 10)
            {
                animators3D.Add(anim);
            }
        }

        foreach (Animator anim in animators3D)
        {
            anim.SetFloat("Offset", UnityEngine.Random.Range(0, 1f));
        }

        overlapRoutine = StartCoroutine(CheckForOverlap());
    }

    public void SendPlayerTo(Vector3 pos)
    {
        player.transform.position = pos;
    }

    public void StartBattle(EnemySquad squad, GameObject callingEnemy)
    {
        //Set the cur squad to be the passed squad.
        //Load the battle scene.
        //Pass the cur squad into the battle and instantiate it.
        //Set the battle scene to be active.
        //Pause the overworld scene, disable input to it.
        curEnemy = callingEnemy;
        curSquad = squad;
        curTransition = Instantiate(sceneTransition, overworldCanvas.transform);
        curTransition.transform.SetAsLastSibling();
        Invoke("WaitForTransitionIn", 1.5f);
        activeOverworld = false;
        canPause = false;
        StopCoroutine(overlapRoutine);
    }

    private void WaitForTransitionIn()
    {
        SceneManager.LoadSceneAsync(battleSceneIndex, LoadSceneMode.Additive);
        SceneManager.sceneLoaded += PassBattleParameters;
        foreach (GameObject obj in overworldObjects)
        {
            obj.SetActive(false);
        }
    }

    private void PassBattleParameters(Scene scene, LoadSceneMode mode)
    {
        if (battleSceneIndex != 8)
        {
            overworldCam.SetActive(false);
            SceneManager.SetActiveScene(scene);
            Battle_Manager.instance.StartBattle(activeParty, curSquad);
        }
        SceneManager.sceneLoaded -= PassBattleParameters;
        curTransition.GetComponent<Animator>().SetTrigger("Uncover");
        Destroy(curTransition, 1.5f);
    }

    public void EndBattle()
    {
        SceneManager.SetActiveScene(overworldScene);
        SceneManager.sceneUnloaded += ResetCamera;
        curTransition = Instantiate(sceneTransition, overworldCanvas.transform);
        Invoke("WaitForTransitionOut", 1.5f);
        activeOverworld = true;
        overlapRoutine = StartCoroutine(CheckForOverlap());
        Destroy(curEnemy);
        canPause = true;
    }

    private void WaitForTransitionOut()
    {
        SceneManager.UnloadSceneAsync(battleSceneIndex);
        foreach (GameObject obj in overworldObjects)
        {
            obj.SetActive(true);
        }
    }

    private void ResetCamera(Scene scene)
    {
        curTransition.GetComponent<Animator>().SetTrigger("Uncover");
        Destroy(curTransition, 1.5f);
        overworldCam.SetActive(true);
        SceneManager.sceneUnloaded -= ResetCamera;
    }

    public void UpdateBattleIndex(int newIndex)
    {
        battleSceneIndex = newIndex;
    }

    public IEnumerator CheckForOverlap()
    {
        List<Renderer> hitObjects = new List<Renderer>();
        while (true)
        {
            if (!player.activeSelf && !Camera.main.gameObject.activeSelf)
            {

            }
            yield return new WaitForFixedUpdate();

            foreach (Renderer rend in hitObjects)
            {
                rend.material.color = new Color(rend.material.color.r, rend.material.color.g, rend.material.color.b, 1);
            }

            hitObjects.Clear();

            
            Vector3 cameraTarget = Camera.main.WorldToScreenPoint(player.transform.position);
            Ray targetRay = Camera.main.ScreenPointToRay(cameraTarget);
            Debug.DrawLine(targetRay.origin, player.transform.position, Color.green, 1f);
            RaycastHit[] hits;
            hits = Physics.SphereCastAll(targetRay, 1f, 10, terrainMask, QueryTriggerInteraction.Collide);
            if (hits.Length > 0)
            {
                foreach (RaycastHit hit in hits)
                {
                    Debug.Log("Hit " + hit.transform.gameObject.name);
                    Transform target = hit.transform;
                    Renderer rend = target.GetComponent<Renderer>();
                    rend.material.color = new Color(rend.material.color.r, rend.material.color.g, rend.material.color.b, 0);
                    if (!hitObjects.Contains(rend)) hitObjects.Add(rend);
                }
            }


        }
    }

    public IEnumerator Fade(float levelOfFade)
    {
        yield return new WaitForFixedUpdate();

        while (true)
        {
            //Debug.Log("Target: " + levelOfFade.ToString() + ", Current: " + curColor.a.ToString() + ", " + cinematicBlack.name);
            if (cinematicBlack.alpha == levelOfFade)
            {
                StopCoroutine(Fade(levelOfFade));
                yield break;
            }

            cinematicBlack.alpha = Mathf.MoveTowards(cinematicBlack.alpha, levelOfFade, 0.05f);
            yield return new WaitForEndOfFrame();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && canPause && !DialogController.instance.dialogObject.activeSelf)
        {
            if (!paused)
            {
                SceneManager.LoadSceneAsync(pauseString, LoadSceneMode.Additive);
                paused = true;
                activeOverworld = false;
            } else
            {
                SceneManager.UnloadSceneAsync(pauseString);
                paused = false;
                activeOverworld = true;
            }
        }
    }

    public void AddItem(Item item)
    {
        if (!consumableItems.Contains(item))
        {
            consumableItems.Add(item);
        }

        GameObject obj = Instantiate(overworldItemPrefab, Overworld_Player.self.transform.position, overworldItemPrefab.transform.rotation);

       

        obj.GetComponent<Overworld_Item>().receivedItem = item;
        
    }
}