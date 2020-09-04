using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class Player : MonoBehaviour
{
    public float moveSpeed; // базовая скорость перемещения игрока
    float s; // текущая скорость игрока
    public float shootRange; // дистанция стрельбы
    public float rocketDamage; // текущий урон от оружия
    public float rocketSpeed; // скорость полета пули
    public float reloadingTime; // время перезарядки оружия (задержка между соседними атаками)
    public float shootSpreadCoeff; // коэффициент разброса пуль
    bool reloading;
    public float rotateSpeed; // скорость поворота
    public float maxEnergy; // максимальный запас энергии
    public float matrixEnergyDrop; // расход энергии на вход в матрицу
    public float energyGrowPerSec; // расход энергии в секунду в матрице
    public float freeSlowMoTime; // время перемещения без затрат энергии
    public float energyRecoveryPerSec; // восстановление энергии в секунду вне матрицы
    [HideInInspector] public float curEnergy; // текущий запас энергии
    float sPrev, sCur;
    public float maxHealthPoint; // максимальный запас здоровья
    public float curHealthPoint; // текущий запас здоровья
    [HideInInspector] public Transform healthPanel;
    [HideInInspector] public HealthPanel healthPanelScript;
    [HideInInspector] public Transform RangeZone;

    [HideInInspector] public float freeSlowMoTimer;

    Transform CamTarget;
    [HideInInspector] public Main main;
    Joystick joy;
    RaycastHit RChit;

    public Enemy myAim;
    public Enemy curEnemyShooting;

    public Color bodyColor;
    [HideInInspector] public MaterialPropertyBlock MPB;
    [HideInInspector] public MeshRenderer mr;

    NavMeshAgent agent;

    [HideInInspector] public bool inMatrix;

    [Space]
    public Color rocketColor;
    public float rocketSize;



    public void StartScene()
    {
        inMatrix = false;
        RangeZone.localScale = new Vector3(shootRange, shootRange, shootRange);

        MPB = new MaterialPropertyBlock();
        mr = GetComponentInChildren<MeshRenderer>();
        mr.GetPropertyBlock(MPB);
        MPB.SetColor("_Color", bodyColor);
        mr.SetPropertyBlock(MPB);

        CamTarget = GameObject.Find("CamTarget").transform;
        agent = GetComponent<NavMeshAgent>();
        // располагаем игрока в его стартовой позиции на сцене
        transform.position = main.playerSpawnPoint.position;
        transform.rotation = Quaternion.identity;
        s = 0;
        curEnergy = maxEnergy;
        curHealthPoint = maxHealthPoint;
        UIRefresh(curEnergy);

        joy = main.joy;

        freeSlowMoTimer = freeSlowMoTime;
        agent.enabled = true;
    }

    // обновление UI
    void UIRefresh(float curEnergy)
    {
        main.EnergySlider.fillAmount = curEnergy / maxEnergy;
        main.EnergyCount.text = curEnergy.ToString("F0");
    }

    private void Update()
    {
        if (main == null) return;

        if (healthPanel != null) healthPanel.position = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.5f);

        if (!main.readyToGo) return;

        myAim = null;
        sPrev = s;
        // определяем и задаем направление движения
        Vector3 direction = Vector3.forward * joy.Vertical + Vector3.right * joy.Horizontal;
        if (direction.magnitude == 0) s = 0;
        else s = moveSpeed;
        sCur = s;

        // определяем, хватает ли игроку энергии для начала движения
        if (s != 0)
        {
            if (sPrev == 0 && sCur > 0)
            {
                if (curEnergy >= matrixEnergyDrop)
                {
                    if (inMatrix == false)
                    {
                        // тратим энергию перманентно на активацию (опционально: если это не нужно, то matrixEnergyDrop установить рааным 0)
                        curEnergy -= matrixEnergyDrop;
                        UIRefresh(curEnergy);
                        freeSlowMoTimer = freeSlowMoTime;
                        inMatrix = true;
                        main.ToneMap.enabled = true;

                        curEnemyShooting = null;
                    }
                }
                else
                {
                    s = 0;
                }
            }
        }

        // включаем режим МАТРИЦА
        if (inMatrix)
        {
            main.curSlowerCoeff = main.matrixCoeff;
            freeSlowMoTimer -= Time.deltaTime;
            if (freeSlowMoTimer < 0) freeSlowMoTimer = 0;

            // тратим энергию посекундно (опционально: если это не нужно, то energyGrowPerSec установить равным 0)
            if (curEnergy > 0)
            {
                if (freeSlowMoTimer == 0) curEnergy -= energyGrowPerSec * Time.deltaTime;
            }
            else
            {
                curEnergy = 0;
                s = 0;
                // при достижении нулевой энергии
                //либо выходить из матрицы полностью (раскомментировать строку), 
                //либо оставаться в ней (всё замедленно), 
                //но без движения и восстановления энергии 
                //(т.е. игрок всё равно будет вынужден отпустить палец от экрана, 
                //зато у него будет время оценить обстановку)
                // П.С.: мне нравится второй вариант (строка закомментирована)
                //inMatrix = false; 
            }
            UIRefresh(curEnergy);
        }
        else main.curSlowerCoeff = 1;

        // двигаем и поворачиваем игрока в заданном направлении        
        if (s > 0 && inMatrix)
        {
            transform.position += direction.normalized * s * Time.deltaTime;
            if (Quaternion.LookRotation(direction) != Quaternion.identity) transform.rotation = Quaternion.LookRotation(direction);
        }

        FindNearestAim();

        // если игрок стоит (скорость передвижения == 0), то он долбит по ближайшему врагу
        // и восстанавливает энергию
        if (s == 0 && !inMatrix)
        {
            if (curEnergy < maxEnergy) curEnergy += energyRecoveryPerSec * Time.deltaTime;
            else curEnergy = maxEnergy;
            UIRefresh(curEnergy);

            if (myAim != null)
            {
                myAim.AimRing.SetActive(true);
                // если смотрим на врага, то стреляем в него
                Vector3 fwd = transform.forward; fwd.y = 0;
                Vector3 dir = myAim.transform.position - transform.position; dir.y = 0;
                if (Vector3.Angle(fwd, dir) <= 1f)
                {
                    if (!reloading)
                    {
                        curEnemyShooting = myAim;

                        // вытаскиваем из пула и настраиваем прожектайл 
                        Rocket rocket = main.rocketsPool.GetChild(0).GetComponent<Rocket>();
                        rocket.transform.parent = null;
                        rocket.transform.position = transform.position + 0.5f * Vector3.up;
                        rocket.startPoint = rocket.transform.position;
                        rocket.maxRange = shootRange;
                        rocket.MyShooterTag = tag;
                        rocket.flying = true;
                        rocket.speed = rocketSpeed;
                        rocket.damage = rocketDamage;

                        rocket.RocketParamsChanger(MPB, rocketColor, rocketSize);

                        Vector3 randomVector = new Vector3(Random.Range(-shootSpreadCoeff, +shootSpreadCoeff), 0, Random.Range(-shootSpreadCoeff, +shootSpreadCoeff));
                        Vector3 lastPoint = Vector3.zero;
                        lastPoint = transform.position + (myAim.transform.position - transform.position).normalized * shootRange + randomVector;
                        rocket.direction = lastPoint - transform.position;


                        //rocket.direction = myAim.transform.position - transform.position;

                        // "пережаряжаемся" (задержка между выстрелами)
                        StartCoroutine(Reloading(reloadingTime));
                    }
                }
                // иначе поворачиваемся на врага для стрельбы
                else
                {
                    transform.rotation = Quaternion.LookRotation(Vector3.RotateTowards(fwd, dir, rotateSpeed * Time.deltaTime, 0));
                }
            }
        }
    }

    public void FindNearestAim()
    {
        if (curEnemyShooting != null)
        {
            myAim = curEnemyShooting;
            return;
        }

        // определяем близжайщего видимого (не закрытого препятствиями) врага
        float nearestShootDist = shootRange;
        foreach (Enemy e in main.enemies)
        {
            e.AimRing.SetActive(false);
            if ((e.transform.position - transform.position).magnitude <= nearestShootDist && !Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.2f, e.transform.position - transform.position, out RChit, (e.transform.position - transform.position).magnitude, 1 << 9))
            {
                myAim = e;
                nearestShootDist = (e.transform.position - transform.position).magnitude;
            }
        }
    }

    // "перезарядка" оружия (задержка между выстрелами)
    IEnumerator Reloading(float reloadingTime)
    {
        reloading = true;
        yield return new WaitForSeconds(reloadingTime);
        reloading = false;
    }


    void LateUpdate()
    {
        // тянем камеру за игроком (только по вертикали)
        CamTarget.position = new Vector3(CamTarget.position.x, CamTarget.position.y, transform.position.z);
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Enemy")
        {            
            Enemy e = other.GetComponent<Enemy>();
            if (e.collDamage != 0)
            {
                main.BodyHitReaction(mr, MPB, bodyColor);

                curHealthPoint -= e.collDamage;
                healthPanelScript.HitFunction(curHealthPoint / maxHealthPoint, e.collDamage);

                if (curHealthPoint <= 0)
                {
                    main.PlayerDie(this);
                }
            }
        }
    }
}
