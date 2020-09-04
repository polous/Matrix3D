using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public enum moveType
{
    Random,
    Follow,
    Twister
}

public class Enemy : MonoBehaviour
{
    public float moveSpeed; // скорость перемещения
    public float shootRange; // дистанция атаки
    public float rotateSpeed; // скорость поворота
    public float movingTime; // время в пути (после него идет переопределение пути)

    public int rocketsCountPerShoot; // количество пуль выпускаемое за один выстрел (если > 1 - дробовик)
    public float shootSpreadCoeff; // коэффициент разброса пуль

    public float rocketDamage; // текущий урон от оружия
    public float rocketSpeed; // скорость полета пули
    public float reloadingTime; // время перезарядки оружия (задержка между соседними атаками в секундах)

    [Space]
    public float maxHealthPoint; // максимальный запас здоровья
    [HideInInspector] public float curHealthPoint; // текущий запас здоровья

    [Space]
    public float voidZoneCastRange; // максимальная дистанция кастования войд зоны
    public float voidZoneDamage; // урон от войд зоны
    public float voidZoneRadius; // радиус войд зоны
    public float voidZoneDuration; // продолжительность от начала каста до непосредственно взрыва (в секундах)
    public float voidZoneReloadingTime; // время перезарядки войд зоны (задержка между соседними кастами в секундах)

    public float twisterAngle; // угловой сектор, в рамках которого враг-Twister будет вращаться
    //bool rotating;
    float curAng, targAng;
    int rotateDir;

    [HideInInspector] public Transform healthPanel;
    [HideInInspector] public HealthPanel healthPanelScript;
    [HideInInspector] public GameObject AimRing;

    [HideInInspector] public Main main;

    public moveType MT;

    RaycastHit RChit;
    NavMeshHit NMhit;
    NavMeshPath path;
    float pathDist;
    Vector3 targetDir;

    bool moving;
    Vector3 fwd, dir;
    float timerForReloading;
    float timerForVoidZoneReloading;
    float timerForVoidZoneCasting;

    int i;

    public Color bodyColor;
    [HideInInspector] public MaterialPropertyBlock MPB;
    [HideInInspector] public MeshRenderer mr;

    [Space]
    public Color rocketColor;
    public float rocketSize;

    [HideInInspector] public LineRenderer lr;
    [HideInInspector] public Transform Throwpoint;

    [Space]
    public float collDamage;


    public void StartScene()
    {
        MPB = new MaterialPropertyBlock();
        mr = GetComponentInChildren<MeshRenderer>();
        mr.GetPropertyBlock(MPB);
        MPB.SetColor("_Color", bodyColor);
        mr.SetPropertyBlock(MPB);
        Throwpoint = transform.Find("Throwpoint");
        lr = Throwpoint.GetComponent<LineRenderer>();

        curHealthPoint = maxHealthPoint;

        path = new NavMeshPath();

        timerForVoidZoneReloading = Random.Range(0, voidZoneReloadingTime); // рандомный таймер для войд зоны (чтобы на старте все враги не начинали одновременный каст)
        timerForReloading = reloadingTime;
        timerForVoidZoneCasting = voidZoneDuration;

        if (rocketsCountPerShoot == 0) rocketsCountPerShoot = 1;

        curAng = 0;
        targAng = twisterAngle / 2f;
        rotateDir = 1;
    }


    float ThrowVelocityCalc(float g, float ang, float x, float y)
    {
        float angRad = ang * Mathf.PI / 180f;
        float v2 = (g * x * x) / (2 * (y - Mathf.Tan(angRad) * x) * Mathf.Pow(Mathf.Cos(angRad), 2));
        float v = Mathf.Sqrt(Mathf.Abs(v2));
        return v;
    }


    public void ShowTrajectory(Vector3 origin, Vector3 speed)
    {
        lr.enabled = true;

        Vector3[] points = new Vector3[100];
        lr.positionCount = points.Length;

        for (int i = 0; i < points.Length; i++)
        {
            float time = i * 0.1f;

            points[i] = origin + speed * time + Physics.gravity * time * time / 2f;

            if (points[i].y < 0.1f)
            {
                lr.positionCount = i + 1;
                break;
            }
        }

        lr.SetPositions(points);
    }

    void ShowVoidZoneTraectory(Vector3 VZPos, float Ang)
    {
        float velocity;
        float ThrowDistX, ThrowDistY;

        Vector3 FromTo = VZPos - Throwpoint.position;
        Vector3 FromToXZ = new Vector3(FromTo.x, 0f, FromTo.z);

        ThrowDistX = FromToXZ.magnitude;
        ThrowDistY = FromTo.y;

        Throwpoint.rotation = Quaternion.LookRotation(FromToXZ);

        Throwpoint.localEulerAngles = new Vector3(-Ang, Throwpoint.localEulerAngles.y, Throwpoint.localEulerAngles.z);
        velocity = ThrowVelocityCalc(Physics.gravity.y, Ang, ThrowDistX, ThrowDistY);

        ShowTrajectory(Throwpoint.position, velocity * Throwpoint.forward);
    }


    // Получение случайной точки на Navmesh
    void GetRandomPoint(Vector3 center, float maxDistance)
    {
        for (int c = 0; c < 100; c++)
        {
            // случайная точка внутри окружности, расположенной в center с радиусом maxDistance
            Vector3 randomPos = new Vector3(Random.Range(center.x - maxDistance, center.x + maxDistance), 0, Random.Range(center.z - maxDistance, center.z + maxDistance));
            // вычисляем путь до randomPos по Navmesh сетке
            NavMesh.CalculatePath(transform.position, randomPos, NavMesh.AllAreas, path);
            // если путь построен
            if (path.status == NavMeshPathStatus.PathComplete)
            {
                // вычисляем длину пути
                if (path.corners.Length >= 2)
                {
                    for (int i = 0; i < path.corners.Length - 1; i++)
                    {
                        pathDist += (path.corners[i + 1] - path.corners[i]).magnitude;
                    }
                }
                // если длина пути достаточная
                if (pathDist >= maxDistance * 2f / 3f) return;
            }
        }
        path.ClearCorners();
    }


    void Update()
    {
        if (main == null) return;

        if (healthPanel != null) healthPanel.position = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.5f);

        if (!main.readyToGo) return;

        if (MT == moveType.Twister)
        {
            if (main.player != null)
            {
                // кастуем войд зону
                if (voidZoneReloadingTime > 0) // исключаем из расчета тех врагов, кто вообще не кастер войд зон
                {
                    VoidZoneCasting();

                    // пока враг кастует войд зону, он больше ничем не занимается
                    if (timerForVoidZoneCasting < voidZoneDuration) return;
                }

                float step = rotateSpeed * Time.deltaTime * main.curSlowerCoeff * 33;
                if (curAng + step >= targAng)
                {
                    if (rotateDir == 1) rotateDir = -1;
                    else rotateDir = 1;
                    curAng = 0;
                    targAng = twisterAngle;
                }
                else
                {
                    curAng += step;

                    if (rotateDir == 1)
                    {
                        transform.Rotate(Vector3.up, step);
                    }
                    else
                    {
                        transform.Rotate(-Vector3.up, step);
                    }
                }

                timerForReloading += Time.deltaTime * main.curSlowerCoeff;
                if (timerForReloading >= reloadingTime)
                {
                    RocketShooting(2);

                    timerForReloading = 0;
                }


                #region
                //if (!rotating)
                //{
                //    if (Vector3.Angle(transform.forward, main.player.transform.position - transform.position) > 0)
                //    {
                //        rotating = true;

                //        Vector3 curDir = main.player.transform.position - transform.position; curDir.y = 0f;
                //        Vector3 fwd = transform.forward; fwd.y = 0;
                //        targAng = Vector3.Angle(fwd, curDir);
                //        curAng = 0f;
                //        rotateDir = TypeOfTurn(curDir); // определяем, в какую сторону он будет поворачиваться - влево или вправо
                //    }
                //    else
                //    {
                //        print("shoot");
                //    }
                //}
                //else
                //{
                //    float step = rotateSpeed * Time.deltaTime * main.curSlowerCoeff;
                //    if (curAng + step > targAng)
                //    {
                //        // Докручиваем до нашего угла
                //        transform.rotation = Quaternion.LookRotation(main.player.transform.position - transform.position);
                //        rotating = false;
                //    }
                //    else
                //    {
                //        curAng += step;

                //        if (rotateDir == 1)
                //        {
                //            transform.Rotate(Vector3.up, step);
                //        }
                //        else
                //        {
                //            transform.Rotate(-Vector3.up, step);
                //        }
                //    }
                //}
                #endregion
            }
        }
        else
        {
            if (main.player != null)
            {
                // кастуем войд зону
                if (voidZoneReloadingTime > 0) // исключаем из расчета тех врагов, кто вообще не кастер войд зон
                {
                    VoidZoneCasting();

                    // пока враг кастует войд зону, он больше ничем не занимается
                    if (timerForVoidZoneCasting < voidZoneDuration) return;
                }

                fwd = transform.forward; fwd.y = 0;
                dir = main.player.transform.position - transform.position; dir.y = 0;
                if ((main.player.transform.position - transform.position).magnitude <= shootRange && !Physics.SphereCast(transform.position + Vector3.up * 0.5f, 0.2f, main.player.transform.position - transform.position, out RChit, (main.player.transform.position - transform.position).magnitude, 1 << 9))
                {
                    // поворачиваемся а потом стреляем/бьем игрока
                    timerForReloading += Time.deltaTime * main.curSlowerCoeff;
                    if (Vector3.Angle(fwd, dir) <= 1f)
                    {
                        if (timerForReloading >= reloadingTime)
                        {
                            RocketShooting(1);

                            timerForReloading = 0;
                        }
                    }
                    else
                    {
                        transform.rotation = Quaternion.LookRotation(Vector3.RotateTowards(fwd, dir, rotateSpeed * Time.deltaTime * main.curSlowerCoeff, 0));
                    }
                }
                else
                {
                    if (MT == moveType.Random)
                    {
                        if (!moving && moveSpeed > 0)
                        {
                            GetRandomPoint(transform.position, 8f);
                            i = 1;

                            StartCoroutine(Moving(movingTime));
                        }
                        else
                        {
                            if (path.corners.Length > 1)
                            {
                                if (i != path.corners.Length)
                                {
                                    transform.position = Vector3.MoveTowards(transform.position, path.corners[i], Time.deltaTime * moveSpeed * main.curSlowerCoeff);
                                    targetDir = path.corners[i] - transform.position; targetDir.y = 0;
                                    transform.rotation = Quaternion.LookRotation(Vector3.RotateTowards(fwd, targetDir, rotateSpeed * Time.deltaTime * main.curSlowerCoeff, 0));

                                    if ((path.corners[i] - transform.position).magnitude <= 0.01f) i++;
                                }
                                else
                                {
                                    moving = false;
                                }
                            }
                        }
                    }

                    else if (MT == moveType.Follow)
                    {
                        if (!moving && moveSpeed > 0)
                        {
                            NavMesh.CalculatePath(transform.position, main.player.transform.position, NavMesh.AllAreas, path);
                            i = 1;

                            StartCoroutine(Moving(movingTime));
                        }
                        else
                        {
                            if (i != path.corners.Length)
                            {
                                transform.position = Vector3.MoveTowards(transform.position, path.corners[i], Time.deltaTime * moveSpeed * main.curSlowerCoeff);
                                targetDir = path.corners[i] - transform.position; targetDir.y = 0;
                                transform.rotation = Quaternion.LookRotation(Vector3.RotateTowards(fwd, targetDir, rotateSpeed * Time.deltaTime * main.curSlowerCoeff, 0));
                                //if (transform.position != path.corners[i]) transform.rotation = Quaternion.LookRotation(targetDir);

                                if ((path.corners[i] - transform.position).magnitude <= 0.01f) i++;
                            }
                            else
                            {
                                moving = false;
                            }
                        }
                    }
                }
            }
        }        
    }

    private void RocketShooting(int type) // type = 1 - стрельба в направлении игрока, 2 - стрельба в направлении взгляда
    {
        for (int i = 0; i < rocketsCountPerShoot; i++)
        {
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
            if(type == 1) lastPoint = transform.position + (main.player.transform.position - transform.position).normalized * shootRange + randomVector;
            if(type == 2) lastPoint = transform.position + transform.forward * shootRange + randomVector;
            Vector3 direction = lastPoint - transform.position;

            rocket.direction = direction;
        }
    }

    private void VoidZoneCasting()
    {
        timerForVoidZoneReloading += Time.deltaTime * main.curSlowerCoeff;
        timerForVoidZoneCasting += Time.deltaTime * main.curSlowerCoeff;

        if (timerForVoidZoneReloading >= voidZoneReloadingTime)
        {
            VoidZone voidZone = main.voidZonesPool.GetChild(0).GetComponent<VoidZone>();
            voidZone.transform.parent = null;
            if ((main.player.transform.position - transform.position).magnitude <= voidZoneCastRange)
            {
                voidZone.transform.position = main.player.transform.position;
            }
            else
            {
                GetRandomPoint(transform.position, voidZoneCastRange);
                if (path.corners.Length > 1) voidZone.transform.position = path.corners.Last();
            }

            if(voidZoneCastRange != 0) ShowVoidZoneTraectory(voidZone.transform.position, 50f);

            voidZone.damage = voidZoneDamage;
            voidZone.radius = voidZoneRadius;
            voidZone.transform.localScale = Vector3.one * voidZoneRadius;
            voidZone.duration = voidZoneDuration;
            voidZone.isCasting = true;
            voidZone.Custer = this;

            voidZone.VZShowRadius();

            Transform vzce = main.voidZoneCastEffectsPool.GetChild(0);
            vzce.transform.parent = null;
            vzce.transform.position = transform.position;
            voidZone.castEffect = vzce;

            timerForVoidZoneReloading = 0;
            timerForVoidZoneCasting = 0;
        }
    }

    IEnumerator Moving(float movingTime)
    {
        moving = true;
        yield return new WaitForSeconds(movingTime);
        moving = false;
    }

    // ФУНКЦИЯ ОПРЕДЕЛЕНИЯ НАПРАВЛЕНИЯ ПОВОРОТА (ВЛЕВО ИЛИ ВПРАВО)
    int TypeOfTurn(Vector3 dir)
    {
        Quaternion b = Quaternion.LookRotation(dir);
        Quaternion a = transform.rotation;
        float _a = a.eulerAngles.y;
        float _b = b.eulerAngles.y;

        if (_a >= 180f && _b >= 180f)
        {
            _b = _b - _a;
            if (_b >= 0) return 1;
            else return -1;
        }
        else if (_a <= 180f && _b <= 180f)
        {
            _b = _b - _a;
            if (_b >= 0) return 1;
            else return -1;
        }
        else if (_a >= 180f && _b <= 180f)
        {
            _b = _b - _a + 180f;
            if (_b <= 0) return 1;
            else return -1;
        }
        else if (_a <= 180f && _b >= 180f)
        {
            _b = _b - _a - 180f;
            if (_b <= 0) return 1;
            else return -1;
        }
        else
            return 1;
    }
}
