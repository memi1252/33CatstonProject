using System;
using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class Enemy : NetworkBehaviour , IDamageable
{
    public float startingHealth;
    public float health;
    public bool dead;
    
    private NavMeshAgent agent;
    private Transform target;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        //target = GameObject.FindGameObjectWithTag("Player").transform;
        health = startingHealth;
        StartCoroutine(UpdatePat());
    }

    private void Update()
    {
        if (target == null)
        {
            target = GameObject.FindGameObjectWithTag("Player").transform;
            StopAllCoroutines();
            StartCoroutine(UpdatePat());
        }
    }

    IEnumerator UpdatePat()
    {
        float refreshRate = .25f; // 0.25초마다 경로 업데이트

        while (target != null)
        {
            Vector3 targetPosition = new Vector3(target.position.x, 0, target.position.z);
            if(!dead)
                agent.SetDestination(targetPosition);
            yield return new WaitForSeconds(refreshRate);
        }
    }
    
    public void TakeHit(float damage, RaycastHit hit)
    {
        health -= damage;
        if (health <= 0 && !dead)
        {
            Die();
        }
    }

    public void Die()
    {
        dead = true;
        Destroy(gameObject);
    }
}
