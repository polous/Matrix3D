﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Analytics;

public class VoidZone : MonoBehaviour
{
    public Transform fillPanel;
    public float damage;
    public float radius;
    public int duration; // продолжительность от начала каста до непосредственно взрыва (в секундах)
    public GameObject explosion;
    public bool isCasting;
    public Enemy Custer; // враг, который кастует данную войд зону
    float timer = 0;

    public Main main;

    void Update()
    {
        if (isCasting)
        {
            // прервем каст, если кастующего врага убили
            if (Custer.curHealthPoint <= 0)
            {
                explosion.SetActive(false);
                fillPanel.localScale = Vector3.zero;
                isCasting = false;
                timer = 0;
                transform.SetParent(main.voidZonesPool);
                return;
            }

            timer += Time.deltaTime * main.curSlowerCoeff;
            if (timer >= duration)
            {
                explosion.SetActive(true);
                isCasting = false;
                fillPanel.localScale = Vector3.zero;
                timer = 0;
                Player p = main.player;

                if ((p.transform.position - transform.position).magnitude <= radius)
                {                    
                    main.BodyHitReaction(p.mr, p.MPB, p.bodyColor);

                    p.curHealthPoint -= damage;
                    p.healthPanelScript.HitFunction(p.curHealthPoint / p.maxHealthPoint, damage);
                    if (p.curHealthPoint <= 0)
                    {
                        main.PlayerDie(p);
                    }
                }

                Invoke("GoToPool", 1.5f);
                return;
            }
            fillPanel.localScale += Vector3.one * 0.38f * main.curSlowerCoeff * Time.deltaTime / duration;
        }
    }

    void GoToPool()
    {
        explosion.SetActive(false);
        transform.SetParent(main.voidZonesPool);
    }
}
