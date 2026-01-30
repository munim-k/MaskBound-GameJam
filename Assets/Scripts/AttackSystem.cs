using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.Rendering;
using Pixeye.Unity;
using UnityEngine.VFX;

public class AttackSystem : MonoBehaviour
{
    #region Other Class
        MovementController _MovementController; 
        CameraController _CameraController;
        CursorLocker _CursorLocker;
        SwordDamagDealer _Sword;
    #endregion

    #region Inspector Variables

    #region Input

    [Header("- Player Input Values")]
        KeyCode AttackInput = KeyCode.JoystickButton2;
        KeyCode SkillInput = KeyCode.JoystickButton3;
        KeyCode EvadeInput = KeyCode.JoystickButton1;
        KeyCode GuardInput = KeyCode.JoystickButton4;

    #endregion

    #region Attack Values

 [Header("- Dash Attack Values")]
    [Foldout("- Attack Values")]
        public float DashAttackRange;
    [Foldout("- Attack Values")]
        public float DashAttackTime, DashAttDistance;

[Header("- Attack Effect Values")]
    [Foldout("- Attack Values")]
        public float AttackMaterialTimes;

    [Foldout("- Attack Values")] 
        public int[] SKillCharge;

 [Header("- Get Signal")]
    [Foldout("- Attack Values")]
        public bool isAttack = true;
        bool DashStart = false;
        bool IsGuard = false;
        bool StopDashAttack = false;

    //Get Correct direction
    Transform PlayerFwDir, CamFwDIr;
    Vector3 Target;

    #endregion

    #region Rolling Variables
    [Foldout("- Rolling Variables")]
        public float delayBetweenPresses = 0.25f;
        bool pressedFirstTime = false;

    [Foldout("- Rolling Variables")]
        public float lastPressedTime, EvadeTime, EvadeRange;

    #endregion

    // Animator && Collider Manager
    #region Hide Variables 

    // Animator Controller
    [Header("- Animator Controller")]
        Animator anim; 


    // Collider Manager
    [Header("- Collider Manager")]
        BoxCollider SwordCollider; //Sword
        List<BoxCollider> IceMagicColl = new List<BoxCollider> { }; //SKill 

    #endregion

    #region Effect Variables

    #region CamEffect

    [Foldout("- Camera Effect")]
        public float[] sCamTimesList;

    [Foldout("- Camera Effect")]
        public List<GameObject> skillCamList;
        bool camSwitching = false;

    // Shake Cam
    Animator camAni; 
    bool shakeCD = false;

    #endregion

    #region VolumeEffect

    [Foldout("- Volume Effect")]
        public Volume DashVolume, DashAttackVolume;

    #endregion

    #region MeshRender & MaterialManager

    [Header("- MeshRender Manager")]
    [Foldout("- MeshRender & Material Manager")]
        public MeshRenderer swordMesh;

    [Foldout("- MeshRender & Material Manager")]
        public SkinnedMeshRenderer PlayerBody, PlayerHair;

    [Header("- Material Manager")]
    [Foldout("- MeshRender & Material Manager")]
        public Material[] PlayerNormalMaterialList;

    [Foldout("- MeshRender & Material Manager")]
        public Material[] PlayerHairNorMaterialList, 
               SwordNorMaterialList, AttacMaterialList, AttackHairMaterialList, AttackSwordMaterialList;

    #endregion

    #region Particle System
     [Foldout("- Particle System Variables")]
        public GameObject[] SwordParSysList;
        GameObject SwordParSys;

    [Foldout("- Particle System Variables")]
        public List<GameObject> SlashEffectList = new List<GameObject>();

    #endregion

    #endregion

    #endregion


    void Start()
    {
        #region Get Normal Variable
        //Get Variable
        _MovementController = GetComponent<MovementController>();
        _CameraController = GameObject.Find("CamHolder").GetComponent<CameraController>();
        _CursorLocker = GameObject.Find("EventSystem").GetComponent<CursorLocker>();

        anim = GetComponent<Animator>();
        camAni = GameObject.Find("CamPivot").GetComponent<Animator>();

        #endregion
    }

    void Update()
    {
        if (!_CursorLocker.isLocked)
            return;

        GetInput();
        AniamtorController();

        Attack();
        LockOnTarget();
        SkillCharging();

        Evade();
        Guard();

        VolumeController();
    }


    // Input Controller
    void GetInput() 
    {
        AttackInput = KeyCode.JoystickButton2;
        AttackInput = _MovementController.inputType == 1 ? AttackInput : KeyCode.Mouse0;

        SkillInput = KeyCode.JoystickButton3;
        SkillInput = _MovementController.inputType == 1 ? SkillInput : KeyCode.Q;

        EvadeInput = KeyCode.JoystickButton1;
        EvadeInput = _MovementController.inputType == 1 ? EvadeInput : KeyCode.E;

        GuardInput = KeyCode.JoystickButton4;
        GuardInput = _MovementController.inputType == 1 ? GuardInput : KeyCode.LeftControl;
    }

    // Animator Controller
    void AniamtorController() 
    {
        anim.SetInteger("Skill-A", SKillCharge[0]);
        anim.SetBool("IsGuard", IsGuard);
    }


    void LockOnTarget()
    {
        if (!anim.GetCurrentAnimatorStateInfo(1).IsName("New State") || anim.GetCurrentAnimatorStateInfo(0).IsName("Evade"))
            transform.LookAt(new Vector3(Target.x, transform.position.y, Target.z));
    }


    #region Attack Function

    // Attack Func
    void Attack()
    {

        #region Target Direction

        CamFwDIr.position = PlayerFwDir.position;

        Target = new Vector3(CamFwDIr.position.x, transform.position.y, CamFwDIr.position.z);
        Target = _CameraController.lockOn ? GameObject.Find("MonsterTargetLocator").transform.position : Target;

        #endregion


        #region Get Input

        if ( Input.GetKeyDown(AttackInput)) 
        {
            // Return Varible
            _MovementController.stopMove = true;
            StopDashAttack = false;
            shakeCD = false;
            for (int i = 0; i < IceMagicColl.Count; i++) 
                IceMagicColl[i].enabled = true;

            // MaterialChange
            MaterialChange(0);

            // Action
            anim.SetTrigger("Attack");
        }

        #endregion


        #region Dash Attack
        //Dash Attack
        if (anim.GetCurrentAnimatorStateInfo(1).IsName("DashAttack") && !StopDashAttack)
        {
            DashStart = true;

            if (_camControll.lockOn)
                transform.DOMove(Target, DashAttackTime).SetId<Tween>("DashTargetAttack");

            else if(_camControll.saveClosestEnemy != null && Vector3.Distance(this.transform.position, _camControll.saveClosestEnemy.transform.position) < DashAttDistance)
                transform.DOMove(_camControll.saveClosestEnemy.transform.position, DashAttackTime).SetId<Tween>("DashTargetAttack");
           
            else
                transform.DOMove(transform.position + transform.forward * DashAttackRange, DashAttackTime).SetId<Tween>("DashAttack");
          
        }

        #endregion


        #region Cancel Event

        if (DashStart && Vector3.Distance(this.transform.position, Target) < 1.55f) //Input.anyKey && DashStart && isAttack || 
        {
            stopDashAttack();
        }

        if(Input.anyKey && !camSwitching)
        {
            CamSwitch(100);
        }

        #endregion


        #region Get Hit Singal

        if (isAttack)
        {
            if (!shakeCD)
            {
                camAni.Play("AttackCamMovement");
                shakeCD = true;
            }
            DashStart = false;
            SwordCollider.enabled = false;
            for (int i = 0; i < IceMagicColl.Count; i++)
                IceMagicColl[i].enabled = false;

            isAttack = false;
        }

        #endregion

    }

    void SkillCharging() 
    {
        if (!anim.GetCurrentAnimatorStateInfo(1).IsName("skillA-Iku"))
        {
            if (Input.GetKey(SkillInput))
                SKillCharge[0] += 1;
        }

        if (Input.GetKeyUp(SkillInput) || anim.GetCurrentAnimatorStateInfo(1).IsName("skillA-Iku"))
        {
            SKillCharge[0] = 0;
            StartCoroutine(SwordEffChargeHold(0));
        }

    }


    #endregion


    // Evade Func
    void Evade() 
    {
        if (Input.GetKeyDown(EvadeInput) && anim.GetBool("IsSprinting"))
        {
            anim.SetTrigger("isEvade");

        if (anim.GetCurrentAnimatorStateInfo(0).IsName("Evade"))
        {
            transform.DOMove(transform.position + transform.forward * EvadeRange, EvadeTime).SetId<Tween>("EvadeMove");
        }

    }
   }



    #region Effect Function

    void VolumeController()
    {
        // Dash Volume Effect
        DashVolume.enabled = anim.GetBool("IsSprinting");
    }

    void MaterialChange(int i)
    {
        switch (i)
        {
            case 0: // Combo Attack

                PlayerBody.materials = AttacMaterialList;
                PlayerHair.materials = AttackHairMaterialList;
                swordMesh.materials = AttackSwordMaterialList;
                CancelInvoke("returnMaterial");
                Invoke("returnMaterial", AttackMaterialTimes);
                break;
        }
    }


    public void returnMaterial()
    {
        PlayerBody.materials = PlayerNormalMaterialList;
        PlayerHair.materials = PlayerHairNorMaterialList;
        swordMesh.materials = SwordNorMaterialList;
        CancelInvoke("returnMaterial");
    }

    #endregion


    #region Animation Trigger Func

    public void stopDashAttack()
    {
        StopDashAttack = true;
        DashStart = false;
        DOTween.PauseAll();
    }

    public void CanDealDamage(int i) 
    {
        switch(i)
        {
            case 0:
                _Sword.CanDealDamage = true;
                break;
            case 1:
                _Sword.CanDealDamage = false;
                break;
        }
    }

    public void IceDamage(int i)
    {
        switch (i)
        {
            case 0:
                _Sword.iceAttack = true;
                break;
            case 1:
                _Sword.iceAttack = false;
                break;
        }
    }

    public IEnumerator CamSwitch(int i)
    {
      if (_CameraController.lockOn)
        switch (i)
        {
            case 100:
                camSwitching = false;
                skillCamList[0].SetActive(false);
                _CameraController.Cam.SetActive(true);
                break;


            case 0:
                camSwitching = true;
                skillCamList[i].SetActive(true);
                _CameraController.Cam.SetActive(false);
                yield return new WaitForSeconds(sCamTimesList[0]);
                
                camSwitching = false;
                _CameraController.Cam.SetActive(true);
                skillCamList[0].SetActive(false);
                break;

        }
    }

    public IEnumerator SwordEffectTrigger(float i)
    {
        StopCoroutine(SwordEffChargeHold(i));
        StopCoroutine(SwordEffectTrigger(i));
        

        ParticleSystem[] SowrdFa = SwordParSys.GetComponentsInChildren<ParticleSystem>();

        foreach (ParticleSystem child in SowrdFa) 
        {
            child.Clear();
            child.Play();
        }

        yield return new WaitForSeconds(i);

    }

    public IEnumerator SwordEffChargeHold(float i)
    {
        StopCoroutine(SwordEffChargeHold(i));
        StopCoroutine(SwordEffectTrigger(i));

        List<ParticleSystem> SwordPS = new List<ParticleSystem>();
        SwordPS.Add(SwordParSys.GetComponentInChildren<ParticleSystem>());

        switch (i) 
        {
            case 0:

                foreach (var ps in SwordPS)
                {
                    ps.Play();
                }

                yield return new WaitForSeconds(1.5f);
                SwordParSys.SetActive(false);

                break;
            case 1:

                SwordParSys.SetActive(true);

                yield return new WaitForSeconds(0.5f);

                foreach (var ps in SwordPS)
                {
                    ps.Pause();
                }

                break;
        }

       
    }

    public IEnumerator SlashEffectFuncd(int i)
    {
        SlashEffectList[i].SetActive(false);
        SlashEffectList[i].SetActive(true);

        yield return new WaitForSeconds(1.5f);
        SlashEffectList[i].SetActive(false);
    }

    #endregion


}

