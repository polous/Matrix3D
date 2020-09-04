using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.UI;

public class VoidZone : MonoBehaviour
{
    public Transform fillPanel;
    public float damage;
    public float radius;
    public float duration; // продолжительность от начала каста до непосредственно взрыва (в секундах)
    public GameObject explosion;
    public bool isCasting;
    public Enemy Custer; // враг, который кастует данную войд зону
    float timer = 0;
    public Transform castEffect;

    public Main main;

    public LineRenderer lr;

    public void VZShowRadius()
    {
        float ThetaScale = 0.02f;
        int Size = (int)((1f / ThetaScale) + 1f);
        float theta = 0;

        lr.positionCount = Size;
        for (int i = 0; i < Size; i++)
        {
            theta += (2.0f * Mathf.PI * ThetaScale);
            float x = radius * Mathf.Cos(theta);
            float z = radius * Mathf.Sin(theta);
            lr.SetPosition(i, new Vector3(x + transform.position.x, 0.1f, z + transform.position.z));
        }
    }

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
                Custer.lr.enabled = false;
                timer = 0;
                transform.SetParent(main.voidZonesPool);
                castEffect.SetParent(main.voidZoneCastEffectsPool);
                return;
            }

            timer += Time.deltaTime * main.curSlowerCoeff;
            if (timer >= duration)
            {
                explosion.SetActive(true);
                lr.enabled = false;
                Custer.lr.enabled = false;
                isCasting = false;
                castEffect.SetParent(main.voidZoneCastEffectsPool);
                fillPanel.localScale = Vector3.zero;
                timer = 0;
                Player p = main.player;

                if (p != null)
                {
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
                }

                Invoke("GoToPool", 1.5f);
                return;
            }
            fillPanel.localScale += Vector3.one * main.curSlowerCoeff * Time.deltaTime / duration;
        }
    }

    void GoToPool()
    {
        lr.enabled = true;
        explosion.SetActive(false);
        transform.SetParent(main.voidZonesPool);
    }
}
