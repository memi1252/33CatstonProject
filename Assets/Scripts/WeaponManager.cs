using System;
using Fusion;
using UnityEngine;

public class WeaponManager : NetworkBehaviour
{
    public static WeaponManager Instance { get; private set; }

    public GameObject weaponUI;
    public GameObject weaponSelectPrefab;
    public Transform weaponSelectPanel;
    
    [Header("무기종류")] 
    public WeaponScriptableObject[] weaponSOs;

    public bool isWeaponSelectActive = false;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        weaponUI.SetActive(false);
    }

    public override void Spawned()
    {
        base.Spawned();
    }

    public override void Render()
    {
        base.Render();
    }

    public override void FixedUpdateNetwork()
    {
        if (Input.GetKeyDown(KeyCode.M) && Runner.IsSceneAuthority)
        {
            WeaponSelect(); // 무기 선택
        }
    }

    public void WeaponSelect()
    {
        if (isWeaponSelectActive)
        {
            return;
        }

        isWeaponSelectActive = true;
        
        
    }
}
