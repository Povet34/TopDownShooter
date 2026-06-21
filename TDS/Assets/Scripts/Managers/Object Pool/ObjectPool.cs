using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TDS.Core;

public class ObjectPool : MonoBehaviour, IObjectPoolService
{
    public static ObjectPool instance;

    [SerializeField] private int poolSize = 10;

    private Dictionary<GameObject, Queue<GameObject>> poolDictionary = 
        new Dictionary<GameObject, Queue<GameObject>>();


    [Header("To Initialize")]
    [SerializeField] private GameObject weaponPickup;
    [SerializeField] private GameObject ammoPickup;
    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        GameServices.Registry.Register<IObjectPoolService>(this);
    }

    private void Start()
    {
        // 단독 씬(부트 프리팹)에서도 안전하도록 null-guard. 풀은 GetObject에서 지연 초기화도 됨.
        if (weaponPickup != null) InitializeNewPool(weaponPickup);
        if (ammoPickup != null) InitializeNewPool(ammoPickup);
    }

    public GameObject GetObject(GameObject prefab,Transform target)
    {
        if (poolDictionary.ContainsKey(prefab) == false)
        {
            InitializeNewPool(prefab);
        }

        if (poolDictionary[prefab].Count == 0)
            CreateNewObject(prefab); // if all objects of this type are in uise, create a new one.

        GameObject objectToGet = poolDictionary[prefab].Dequeue();

        objectToGet.transform.position = target.position;
        objectToGet.transform.parent = null;

        objectToGet.SetActive(true);

        return objectToGet;
    }

    public void ReturnObject(GameObject objectToReturn, float delay = .001f)
    {
        StartCoroutine(DelayReturn(delay, objectToReturn));
    }

    private IEnumerator DelayReturn(float delay,GameObject objectToReturn)
    {
        yield return new WaitForSeconds(delay);

        ReturnToPool(objectToReturn);
    }

    private void ReturnToPool(GameObject objectToReturn)
    {
        if (objectToReturn == null)
            return;

        var pooled = objectToReturn.GetComponent<PooledObject>();
        GameObject originalPrefab = pooled != null ? pooled.originalPrefab : null;

        objectToReturn.SetActive(false);
        objectToReturn.transform.parent = transform;

        // 다른 풀 인스턴스(이전 씬/테스트)에서 온 객체면 이 풀에 해당 큐가 없을 수 있음 → 비활성만 하고 끝.
        if (originalPrefab == null || poolDictionary.ContainsKey(originalPrefab) == false)
            return;

        poolDictionary[originalPrefab].Enqueue(objectToReturn);
    }

    private void InitializeNewPool(GameObject prefab)
    {
        poolDictionary[prefab] = new Queue<GameObject>();

        for (int i = 0; i < poolSize; i++)
        {
            CreateNewObject(prefab);
        }
    }

    private void CreateNewObject(GameObject prefab)
    {
        GameObject newObject = Instantiate(prefab, transform);
        newObject.AddComponent<PooledObject>().originalPrefab = prefab;
        newObject.SetActive(false);

        poolDictionary[prefab].Enqueue(newObject);
    }
}
